using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ARHerb.Camera;
using ARHerb.Network;
using ARHerb.Data;

namespace ARHerb.UI
{
    /// <summary>
    /// Coordinates the main UGUI Canvas UI elements, the camera capture lifecycle,
    /// and triggers backend request flows to display results.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Backend Connections")]
        [SerializeField] private BackendClient backendClient;
        [SerializeField] private InputField backendUrlInput;

        [Header("Mode & Language Settings")]
        [SerializeField] private Dropdown modeDropdown;
        [SerializeField] private string defaultLanguage = "pl"; // Language to query (e.g., pl or en)

        [Header("Camera & Preview Elements")]
        [SerializeField] private RawImage cameraPreviewUI;
        [Tooltip("If checked, utilizes the TestImageCaptureProvider fallback in Editor instead of WebCamTexture.")]
        [SerializeField] private bool useMockTestImageInEditor = false;

        [Header("Result Panel UI Elements")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text commonNameText;
        [SerializeField] private Text scientificNameText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text funFactText;
        [SerializeField] private Text edibilityText;
        [SerializeField] private Text statusMessageText; // Used for Loading / Error messages

        [Header("Buttons")]
        [SerializeField] private Button scanButton;

        private ICameraCaptureProvider activeCaptureProvider;

        private void Start()
        {
            // Set up backend URL input field with default client setting
            if (backendUrlInput != null && backendClient != null)
            {
                backendUrlInput.text = backendClient.GetBackendUrl();
                backendUrlInput.onEndEdit.AddListener(backendClient.SetBackendUrl);
            }

            // Setup Scan button click listener
            if (scanButton != null)
            {
                scanButton.onClick.AddListener(OnScanButtonClicked);
            }

            // Hide result panel on start
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            SetStatusText("Gotowy do skanowania");

            InitializeCameraProvider();
        }

        /// <summary>
        /// Selects and instantiates the correct camera provider based on current environment.
        /// </summary>
        private void InitializeCameraProvider()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (useMockTestImageInEditor)
            {
                // Fallback A: TestImageCaptureProvider (pre-selected Texture2D asset)
                activeCaptureProvider = gameObject.AddComponent<TestImageCaptureProvider>();
                Debug.Log("[UIManager] Initialized TestImageCaptureProvider for Editor/PC.");
            }
            else
            {
                // Fallback B: WebCamTexture preview
                activeCaptureProvider = gameObject.AddComponent<EditorWebcamCaptureProvider>();
                Debug.Log("[UIManager] Initialized EditorWebcamCaptureProvider for Editor/PC.");
            }
#else
            // Production C: AR Foundation camera stream
            activeCaptureProvider = gameObject.AddComponent<ARFoundationCaptureProvider>();
            Debug.Log("[UIManager] Initialized ARFoundationCaptureProvider for Mobile Device.");
#endif

            if (activeCaptureProvider != null)
            {
                activeCaptureProvider.Initialize(cameraPreviewUI);
            }
        }

        private void OnScanButtonClicked()
        {
            if (activeCaptureProvider == null)
            {
                SetStatusText("Błąd: Brak dostawcy aparatu.");
                return;
            }

            SetStatusText("Pobieranie klatki z aparatu...");
            scanButton.interactable = false;

            // Capture the image from the active provider (Editor webcam or AR Foundation camera)
            activeCaptureProvider.CaptureFrame(jpegBytes =>
            {
                if (jpegBytes == null || jpegBytes.Length == 0)
                {
                    SetStatusText("Błąd: Nie udało się przechwycić obrazu.");
                    scanButton.interactable = true;
                    return;
                }

                string selectedMode = GetSelectedMode();
                SetStatusText("Przesyłanie obrazu do serwera...");

                // Call /api/identify endpoint
                backendClient.IdentifySpecimen(
                    jpegBytes,
                    selectedMode,
                    defaultLanguage,
                    onSuccess: scanResult =>
                    {
                        ProcessIdentifyResult(scanResult, selectedMode);
                    },
                    onFailure: error =>
                    {
                        SetStatusText($"Błąd serwera: {error}");
                        scanButton.interactable = true;
                    }
                );
            });
        }

        /// <summary>
        /// Processes the result returned from /api/identify.
        /// If the mode is plants, it automatically triggers /api/enrich to fetch description and fun facts.
        /// </summary>
        private void ProcessIdentifyResult(ScanResult result, string mode)
        {
            if (result == null || result.results == null || result.results.Count == 0)
            {
                SetStatusText("Nie rozpoznano obiektu. Spróbuj ponownie.");
                scanButton.interactable = true;
                return;
            }

            var bestCandidate = result.results[0];
            string scientificName = bestCandidate.species?.scientificNameWithoutAuthor ?? result.bestMatch;
            
            // Get first common name or fall back to scientific name
            string commonName = (bestCandidate.species?.commonNames != null && bestCandidate.species.commonNames.Count > 0)
                ? bestCandidate.species.commonNames[0]
                : scientificName;

            float confidenceScore = bestCandidate.score;

            // If the mode is "plants", the identify endpoint returns enrichment as null.
            // We must call /api/enrich dynamically to get Gemini enrichment (matching the web frontend design).
            if (mode.ToLower() == "plants")
            {
                SetStatusText($"Zidentyfikowano: {commonName}. Pobieranie szczegółów...");
                
                List<string> commonNames = bestCandidate.species?.commonNames ?? new List<string>();

                backendClient.EnrichPlant(
                    scientificName,
                    commonNames,
                    defaultLanguage,
                    onSuccess: enrichRes =>
                    {
                        DisplayResultUI(commonName, scientificName, confidenceScore, enrichRes?.enrichment);
                        SetStatusText("Skanowanie zakończone pomyślnie.");
                        scanButton.interactable = true;
                    },
                    onFailure: error =>
                    {
                        // Even if enrichment fails, we show the identification name and score.
                        DisplayResultUI(commonName, scientificName, confidenceScore, null);
                        SetStatusText($"Rozpoznano, ale nie pobrano ciekawostek: {error}");
                        scanButton.interactable = true;
                    }
                );
            }
            else
            {
                // In other modes (mushrooms, insects, stones), the backend directly packages the enrichment in the identify response.
                DisplayResultUI(commonName, scientificName, confidenceScore, result.enrichment);
                SetStatusText("Skanowanie zakończone pomyślnie.");
                scanButton.interactable = true;
            }
        }

        private void DisplayResultUI(string commonName, string scientificName, float score, EnrichmentData enrichment)
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

            if (commonNameText != null) commonNameText.text = commonName;
            if (scientificNameText != null) scientificNameText.text = scientificName;
            if (scoreText != null) scoreText.text = $"Prawdopodobieństwo: {score:P1}"; // Format as percentage e.g. 85.0%

            if (enrichment != null)
            {
                if (descriptionText != null) descriptionText.text = enrichment.description;
                if (funFactText != null) funFactText.text = enrichment.funFact;
                if (edibilityText != null)
                {
                    string edibilityLabel = string.IsNullOrEmpty(enrichment.edibleNote) 
                        ? GetFriendlyEdibilityStatus(enrichment.edibleStatus) 
                        : $"{GetFriendlyEdibilityStatus(enrichment.edibleStatus)} - {enrichment.edibleNote}";
                    edibilityText.text = edibilityLabel;
                }
            }
            else
            {
                if (descriptionText != null) descriptionText.text = "Brak opisu.";
                if (funFactText != null) funFactText.text = "Brak ciekawostki.";
                if (edibilityText != null) edibilityText.text = "Status spożywczy: brak danych.";
            }
        }

        private string GetFriendlyEdibilityStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return "Brak danych";
            switch (status.ToLower())
            {
                case "edible": return "Jadalny / Bezpieczny";
                case "toxic": return "Trujący / Niebezpieczny";
                case "both": return "Częściowo jadalny / Warunkowo";
                case "unknown": return "Nieznany / Brak danych";
                default: return status;
            }
        }

        private string GetSelectedMode()
        {
            if (modeDropdown == null) return "plants";
            string text = modeDropdown.options[modeDropdown.value].text.ToLower();
            
            // Map Polish translations to matching backend categories
            if (text.Contains("roślin") || text.Contains("plants")) return "plants";
            if (text.Contains("grzyb") || text.Contains("mushrooms")) return "mushrooms";
            if (text.Contains("owad") || text.Contains("insects")) return "insects";
            if (text.Contains("kamien") || text.Contains("skal") || text.Contains("stone")) return "stones";
            
            return "plants";
        }

        private void SetStatusText(string message)
        {
            if (statusMessageText != null)
            {
                statusMessageText.text = message;
                Debug.Log($"[UIManager] Status: {message}");
            }
        }

        private void OnDestroy()
        {
            if (activeCaptureProvider != null)
            {
                activeCaptureProvider.Release();
            }
        }
    }
}
