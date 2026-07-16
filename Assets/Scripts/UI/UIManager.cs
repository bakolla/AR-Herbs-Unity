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
        [SerializeField] private RawImage thumbnailPreviewUI;
#if UNITY_EDITOR || UNITY_STANDALONE
        [Tooltip("If checked, utilizes the TestImageCaptureProvider fallback in Editor instead of WebCamTexture.")]
        [SerializeField] private bool useMockTestImageInEditor = false;
        [Tooltip("Drag a Texture2D asset here to serve as the mock camera feed when testing in the Editor without a webcam.")]
        [SerializeField] private Texture2D mockTestImage;
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
        [SerializeField] private Text debugText; // Displays AR diagnostics on screen

        [Header("Buttons")]
        [SerializeField] private Button scanButton;

        [Header("AR / Editor Environment Roots")]
        [SerializeField] private GameObject arMobileRoot;
        [SerializeField] private GameObject pcCamera;

        private ICameraCaptureProvider activeCaptureProvider;

        private void Start()
        {
            StartCoroutine(UpdateDiagnosticsRoutine());
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

            // Load and save selected language
            defaultLanguage = PlayerPrefs.GetString("SavedLanguage", "pl");
            PlayerPrefs.SetString("SavedLanguage", defaultLanguage);

            // Load selected mode index and setup listener
            if (modeDropdown != null)
            {
                int savedMode = PlayerPrefs.GetInt("SavedMode", 0);
                modeDropdown.value = savedMode;
                modeDropdown.onValueChanged.RemoveAllListeners();
                modeDropdown.onValueChanged.AddListener(OnModeChanged);
            }

            // Setup Scan button click listener
            if (scanButton != null)
            {
                scanButton.onClick.AddListener(OnScanButtonClicked);
            }

            // Hide result panel and thumbnail on start
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
            if (thumbnailPreviewUI != null)
            {
                thumbnailPreviewUI.gameObject.SetActive(false);
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

        private void OnModeChanged(int newIndex)
        {
            PlayerPrefs.SetInt("SavedMode", newIndex);
            PlayerPrefs.Save();
            Debug.Log($"[UIManager] Saved selected mode index: {newIndex}");
        }

        /// <summary>
        /// Selects and instantiates the correct camera provider based on current environment.
        /// </summary>
        private void InitializeCameraProvider()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            Debug.Log("Editor Mobile Preview Mode");
            Debug.Log($"[EditorPreview] Current Game View aspect: {(float)Screen.width / Screen.height:F3} (Target: 1080x2340 = 0.462)");
            Debug.Log("Editor mode: using webcam fallback");
            if (arMobileRoot != null) arMobileRoot.SetActive(false);
            if (pcCamera != null) pcCamera.SetActive(true);

            if (useMockTestImageInEditor)
            {
                // Fallback A: TestImageCaptureProvider (pre-selected Texture2D asset)
                var testProvider = gameObject.AddComponent<TestImageCaptureProvider>();
                testProvider.SetTestImage(mockTestImage);
                activeCaptureProvider = testProvider;
                Debug.Log("[UIManager] Initialized TestImageCaptureProvider for Editor/PC.");
            }
            else
            {
                // Fallback B: WebCamTexture preview
                activeCaptureProvider = gameObject.AddComponent<EditorWebcamCaptureProvider>();
                Debug.Log("[UIManager] Initialized EditorWebcamCaptureProvider for Editor/PC.");
            }
#else
            if (arMobileRoot != null)
            {
                arMobileRoot.SetActive(false);
            }
            if (pcCamera != null)
            {
                pcCamera.SetActive(true);
            }

            if (WebCamTexture.devices == null || WebCamTexture.devices.Length == 0)
            {
                SetStatusText("No camera found on Android device.", StatusType.Error);
                Debug.LogError("[UIManager] No camera found on Android device.");
                return;
            }

            activeCaptureProvider = gameObject.AddComponent<MobileWebcamCaptureProvider>();
            Debug.Log("[UIManager] Initialized MobileWebcamCaptureProvider for Android MVP.");
#endif

            if (activeCaptureProvider != null)
            {
                activeCaptureProvider.Initialize(cameraPreviewUI);
            }
        }

        public enum StatusType { Ready, Loading, Success, Error }

        private bool isScanning = false;

        private void OnScanButtonClicked()
        {
            if (isScanning) return;
            isScanning = true;

            if (activeCaptureProvider == null)
            {
                SetStatusText("Nie udało się pobrać obrazu z kamery.", StatusType.Error);
                isScanning = false;
                return;
            }

            // Hide old result panel immediately at the start of a new scan
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
            if (thumbnailPreviewUI != null)
            {
                thumbnailPreviewUI.gameObject.SetActive(false);
            }

            SetStatusText("Capturing image...", StatusType.Loading);
            SetScanButtonState(false, "CZEKAJ...");

            // Capture the image from the active provider (Editor webcam or mobile webcam)
            activeCaptureProvider.CaptureFrame(jpegBytes =>
            {
                if (jpegBytes == null || jpegBytes.Length == 0)
                {
                    SetStatusText("Nie udało się pobrać obrazu z kamery.", StatusType.Error);
                    SetScanButtonState(true, "SCAN");
                    isScanning = false;
                    return;
                }

                // Show dynamic thumbnail preview
                if (thumbnailPreviewUI != null)
                {
                    Texture2D thumbTex = new Texture2D(2, 2);
                    if (thumbTex.LoadImage(jpegBytes))
                    {
                        if (thumbnailPreviewUI.texture != null && thumbnailPreviewUI.texture is Texture2D dynamicTex)
                        {
                            Destroy(dynamicTex);
                        }
                        thumbnailPreviewUI.texture = thumbTex;
                        thumbnailPreviewUI.gameObject.SetActive(true);
                    }
                }

                string selectedMode = GetSelectedMode();
                SetStatusText("Sending to backend...", StatusType.Loading);

                // Call /api/identify endpoint
                backendClient.IdentifySpecimen(
                    jpegBytes,
                    selectedMode,
                    defaultLanguage,
                    onSuccess: scanResult =>
                    {
                        SetStatusText("Analyzing...", StatusType.Loading);
                        ProcessIdentifyResult(scanResult, selectedMode);
                    },
                    onFailure: error =>
                    {
                        Debug.LogError($"[UIManager] Backend Error: {error}");
                        if (Application.internetReachability == NetworkReachability.NotReachable)
                        {
                            SetStatusText("Brak połączenia z internetem.", StatusType.Error);
                        }
                        else
                        {
                            SetStatusText("Nie można połączyć się z backendem.", StatusType.Error);
                        }
                        SetScanButtonState(true, "SCAN");
                        isScanning = false;
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
                SetStatusText("Nie znaleziono pasującego obiektu. Spróbuj bliżej i w lepszym świetle.", StatusType.Error);
                SetScanButtonState(true, "SCAN");
                isScanning = false;
                return;
            }

            var bestCandidate = result.results[0];
            string scientificName = bestCandidate.species?.scientificNameWithoutAuthor ?? result.bestMatch;
            
            // Get first common name or fall back to scientific name
            string commonName = (bestCandidate.species?.commonNames != null && bestCandidate.species.commonNames.Count > 0)
                ? bestCandidate.species.commonNames[0]
                : scientificName;

            float confidenceScore = bestCandidate.score;

            // Immediately display the identification result on the UI
            DisplayResultUI(commonName, scientificName, confidenceScore, null);

            if (mode.ToLower() == "plants")
            {
                SetStatusText("Loading details...", StatusType.Loading);
                
                // Show temporary "Loading details..." string in enrichment fields
                if (descriptionText != null) descriptionText.text = "Loading details...";
                if (funFactText != null) funFactText.text = "Loading details...";
                if (edibilityText != null) edibilityText.text = "Status spożywczy: Loading details...";

                List<string> commonNames = bestCandidate.species?.commonNames ?? new List<string>();

                backendClient.EnrichPlant(
                    scientificName,
                    commonNames,
                    defaultLanguage,
                    onSuccess: enrichRes =>
                    {
                        DisplayResultUI(commonName, scientificName, confidenceScore, enrichRes?.enrichment);
                        SetStatusText("Done", StatusType.Success);
                        SetScanButtonState(true, "SCAN");
                        isScanning = false;
                    },
                    onFailure: error =>
                    {
                        Debug.LogError($"[UIManager] Enrichment Failure: {error}");
                        // Keep basic result but display a friendly error/description
                        DisplayResultUI(commonName, scientificName, confidenceScore, null);
                        if (descriptionText != null) descriptionText.text = "Nie udało się pobrać szczegółów AI.";
                        SetStatusText("Done", StatusType.Success);
                        SetScanButtonState(true, "SCAN");
                        isScanning = false;
                    }
                );
            }
            else
            {
                // In other modes (mushrooms, insects, stones), the backend directly packages the enrichment.
                DisplayResultUI(commonName, scientificName, confidenceScore, result.enrichment);
                SetStatusText("Done", StatusType.Success);
                SetScanButtonState(true, "SCAN");
                isScanning = false;
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
                if (descriptionText != null) descriptionText.text = "Brak szczegółowych informacji.";
                if (funFactText != null) funFactText.text = "Brak ciekawostki.";
                if (edibilityText != null) edibilityText.text = "Status spożywczy: Brak danych.";
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

        private System.Collections.IEnumerator UpdateDiagnosticsRoutine()
        {
            while (true)
            {
                RunARDiagnostics();
                yield return new WaitForSeconds(1.0f);
            }
        }

        private void RunARDiagnostics()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== AR DIAGNOSTICS ===");
            
            try
            {
                try
                {
                    bool hasCameraPerm = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
                    sb.AppendLine($"Camera Perm: {(hasCameraPerm ? "GRANTED" : "DENIED")}");
                }
                catch (Exception ex) { sb.AppendLine($"Perm Err: {ex.Message}"); }
                
                var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                sb.AppendLine($"URP Pipeline: {(pipeline != null ? pipeline.name : "Built-in")}");
                
                if (arMobileRoot == null)
                {
                    sb.AppendLine("arMobileRoot: NULL");
                }
                else
                {
                    sb.AppendLine($"arMobileRoot: Active={arMobileRoot.activeSelf}, InHierarchy={arMobileRoot.activeInHierarchy}");
                }
                
                var allSession = Resources.FindObjectsOfTypeAll<UnityEngine.XR.ARFoundation.ARSession>();
                sb.AppendLine($"ARSessions found: {allSession.Length}");
                foreach (var s in allSession)
                {
                    sb.AppendLine($" - {s.gameObject.name}: Active={s.gameObject.activeSelf}, En={s.enabled}, State={UnityEngine.XR.ARFoundation.ARSession.state}");
                }
                
                var allCameraMgr = Resources.FindObjectsOfTypeAll<UnityEngine.XR.ARFoundation.ARCameraManager>();
                sb.AppendLine($"ARCameraMgrs found: {allCameraMgr.Length}");
                foreach (var c in allCameraMgr)
                {
                    sb.AppendLine($" - {c.gameObject.name}: Active={c.gameObject.activeSelf}, En={c.enabled}");
                }
                
                var allCamBg = Resources.FindObjectsOfTypeAll<UnityEngine.XR.ARFoundation.ARCameraBackground>();
                sb.AppendLine($"ARCamBgs found: {allCamBg.Length}");
                foreach (var bg in allCamBg)
                {
                    sb.AppendLine($" - {bg.gameObject.name}: Active={bg.gameObject.activeSelf}, En={bg.enabled}");
                }

                if (pipeline != null)
                {
                    try
                    {
                        var pipelineAsset = pipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                        if (pipelineAsset != null)
                        {
                            var property = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset).GetProperty("scriptableRendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                            UnityEngine.Rendering.Universal.ScriptableRendererData[] rendererDataArray = null;
                            if (property == null)
                            {
                                var field = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                                if (field != null)
                                {
                                    rendererDataArray = field.GetValue(pipelineAsset) as UnityEngine.Rendering.Universal.ScriptableRendererData[];
                                }
                            }
                            else
                            {
                                rendererDataArray = property.GetValue(pipelineAsset) as UnityEngine.Rendering.Universal.ScriptableRendererData[];
                            }

                            if (rendererDataArray != null)
                            {
                                sb.AppendLine($"URP Renderers: {rendererDataArray.Length}");
                                for (int i = 0; i < rendererDataArray.Length; i++)
                                {
                                    var rData = rendererDataArray[i];
                                    if (rData == null) continue;
                                    sb.AppendLine($" R[{i}]: {rData.name}");
                                    if (rData.rendererFeatures != null)
                                    {
                                        foreach (var feat in rData.rendererFeatures)
                                        {
                                            if (feat == null) continue;
                                            sb.AppendLine($"  - Feat: {feat.name}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"URP check err: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Fatal Diag Err: {ex.Message}");
                sb.AppendLine(ex.StackTrace);
            }

            string diagString = sb.ToString();
            Debug.Log(diagString);
            if (debugText != null)
            {
                debugText.text = diagString;
                debugText.gameObject.SetActive(false);
                if (debugText.transform.parent != null)
                {
                    debugText.transform.parent.gameObject.SetActive(false);
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
