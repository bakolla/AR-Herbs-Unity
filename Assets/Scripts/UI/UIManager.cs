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
#if UNITY_EDITOR || UNITY_STANDALONE
        [Tooltip("If checked, utilizes the TestImageCaptureProvider fallback in Editor instead of WebCamTexture.")]
        [SerializeField] private bool useMockTestImageInEditor = false;
#endif

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

        [Header("AR / Editor Environment Roots")]
        [SerializeField] private GameObject arMobileRoot;
        [SerializeField] private GameObject pcCamera;

        private ICameraCaptureProvider activeCaptureProvider;

        private void Start()
        {
            // Load backend URL from PlayerPrefs or client settings
            string savedUrl = PlayerPrefs.GetString("SavedBackendUrl", "");
            if (string.IsNullOrEmpty(savedUrl))
            {
                savedUrl = backendClient != null ? backendClient.GetBackendUrl() : "http://localhost:3001";
            }

            // In Editor mode, default url should be http://localhost:3001 if no saved url exists
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(PlayerPrefs.GetString("SavedBackendUrl", "")))
            {
                savedUrl = "http://localhost:3001";
            }
#endif

            if (backendClient != null)
            {
                backendClient.SetBackendUrl(savedUrl);
            }

            if (backendUrlInput != null)
            {
                backendUrlInput.text = savedUrl;
                backendUrlInput.onEndEdit.RemoveAllListeners();
                backendUrlInput.onEndEdit.AddListener(OnBackendUrlChanged);
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

            SetStatusText("Gotowy do skanowania", StatusType.Ready);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Request Android Camera permission on startup asynchronously
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                SetStatusText("Waiting for camera permission...", StatusType.Loading);
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                StartCoroutine(WaitForCameraPermission());
            }
            else
            {
                InitializeCameraProvider();
            }
#else
            InitializeCameraProvider();
#endif
            CheckBackendUrlWarning(savedUrl);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private System.Collections.IEnumerator WaitForCameraPermission()
        {
            float timeout = 15f; // Wait up to 15 seconds
            float elapsed = 0f;
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                SetStatusText("Gotowy do skanowania", StatusType.Ready);
                InitializeCameraProvider();
            }
            else
            {
                SetStatusText("Błąd: Wymagane uprawnienie do aparatu.", StatusType.Error);
            }
        }
#endif

        private void OnBackendUrlChanged(string newUrl)
        {
            PlayerPrefs.SetString("SavedBackendUrl", newUrl);
            PlayerPrefs.Save();
            if (backendClient != null)
            {
                backendClient.SetBackendUrl(newUrl);
            }
            CheckBackendUrlWarning(newUrl);
        }

        private void CheckBackendUrlWarning(string url)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (url.Contains("localhost") || url.Contains("127.0.0.1") || url.StartsWith("http://"))
            {
                SetStatusText("Warning: Use a public HTTPS backend URL such as Pinggy/ngrok, not localhost.", StatusType.Error);
            }
#endif
        }

        /// <summary>
        /// Selects and instantiates the correct camera provider based on current environment.
        /// </summary>
        private void InitializeCameraProvider()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            Debug.Log("Editor mode: using webcam fallback");
            if (arMobileRoot != null) arMobileRoot.SetActive(false);
            if (pcCamera != null) pcCamera.SetActive(true);

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
#elif UNITY_ANDROID
            Debug.Log("Android mode: using AR Foundation");
            if (arMobileRoot != null) arMobileRoot.SetActive(true);
            if (pcCamera != null) pcCamera.SetActive(false);

            // Production C: AR Foundation camera stream
            activeCaptureProvider = gameObject.AddComponent<ARFoundationCaptureProvider>();
            Debug.Log("[UIManager] Initialized ARFoundationCaptureProvider for Mobile Device.");
#else
            Debug.Log("Android mode: using AR Foundation");
            if (arMobileRoot != null) arMobileRoot.SetActive(true);
            if (pcCamera != null) pcCamera.SetActive(false);

            activeCaptureProvider = gameObject.AddComponent<ARFoundationCaptureProvider>();
#endif

            if (activeCaptureProvider != null)
            {
                activeCaptureProvider.Initialize(cameraPreviewUI);
            }
        }

        public enum StatusType { Ready, Loading, Success, Error }

        private void OnScanButtonClicked()
        {
            if (activeCaptureProvider == null)
            {
                SetStatusText("Błąd: Brak dostawcy aparatu.", StatusType.Error);
                return;
            }

            // Hide old result panel immediately at the start of a new scan
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            SetStatusText("Aparat: Przechwytywanie kadru...", StatusType.Loading);
            SetScanButtonState(false, "CZEKAJ...");

            // Capture the image from the active provider (Editor webcam or AR Foundation camera)
            activeCaptureProvider.CaptureFrame(jpegBytes =>
            {
                if (jpegBytes == null || jpegBytes.Length == 0)
                {
                    SetStatusText("Błąd: Nie udało się zapisać zdjęcia z aparatu.", StatusType.Error);
                    SetScanButtonState(true, "SCAN");
                    return;
                }

                string selectedMode = GetSelectedMode();
                SetStatusText("AI: Analiza i rozpoznawanie...", StatusType.Loading);

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
                        SetStatusText("Błąd: Serwer nie odpowiedział. Sprawdź połączenie.", StatusType.Error);
                        SetScanButtonState(true, "SCAN");
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
                SetStatusText("Nie znaleziono dopasowania. Spróbuj zbliżyć aparat.", StatusType.Error);
                SetScanButtonState(true, "SCAN");
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
            // We must call /api/enrich dynamically to get Gemini enrichment.
            if (mode.ToLower() == "plants")
            {
                SetStatusText("Gemini AI: Pobieranie ciekawostek i opisu...", StatusType.Loading);
                
                List<string> commonNames = bestCandidate.species?.commonNames ?? new List<string>();

                backendClient.EnrichPlant(
                    scientificName,
                    commonNames,
                    defaultLanguage,
                    onSuccess: enrichRes =>
                    {
                        DisplayResultUI(commonName, scientificName, confidenceScore, enrichRes?.enrichment);
                        SetStatusText("Rozpoznano pomyślnie!", StatusType.Success);
                        SetScanButtonState(true, "SCAN");
                    },
                    onFailure: error =>
                    {
                        // Even if enrichment fails, we show the identification name and score.
                        DisplayResultUI(commonName, scientificName, confidenceScore, null);
                        SetStatusText($"Zidentyfikowano (brak opisu AI): {error}", StatusType.Success);
                        SetScanButtonState(true, "SCAN");
                    }
                );
            }
            else
            {
                // In other modes (mushrooms, insects, stones), the backend directly packages the enrichment.
                DisplayResultUI(commonName, scientificName, confidenceScore, result.enrichment);
                SetStatusText("Rozpoznano pomyślnie!", StatusType.Success);
                SetScanButtonState(true, "SCAN");
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

        private void SetStatusText(string message, StatusType type)
        {
            if (statusMessageText != null)
            {
                statusMessageText.text = message;
                
                switch (type)
                {
                    case StatusType.Ready:
                        statusMessageText.color = Color.white;
                        break;
                    case StatusType.Loading:
                        statusMessageText.color = new Color(0.2f, 0.6f, 1f); // Soft cyan/blue
                        break;
                    case StatusType.Success:
                        statusMessageText.color = new Color(0.18f, 0.8f, 0.44f); // Emerald green
                        break;
                    case StatusType.Error:
                        statusMessageText.color = new Color(0.9f, 0.3f, 0.3f); // Coral red
                        break;
                }
                
                Debug.Log($"[UIManager] Status ({type}): {message}");
            }
        }

        private void SetScanButtonState(bool interactable, string buttonText)
        {
            if (scanButton != null)
            {
                scanButton.interactable = interactable;
                Text txt = scanButton.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.text = buttonText;
                }
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
