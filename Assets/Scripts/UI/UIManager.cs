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

        [Header("Result Location Elements")]
        [SerializeField] private Text resultGpsLabel;
        [SerializeField] private Button resultOpenInMapsButton;
        [SerializeField] private Text resultDebugText;

        private bool currentScanHasLocation = false;
        private float currentScanLat = 0f;
        private float currentScanLng = 0f;
        private string currentCommonName = "";
        private float lastLanguageDropdownCloseTime = 0f;

        [Header("Zoom UI Elements")]
        [SerializeField] private Button zoom1xButton;
        [SerializeField] private Button zoom2xButton;
        [SerializeField] private GameObject scanningFrame;

        [Header("Flashlight & Camera UI Elements")]
        [SerializeField] private Button flashlightButton;
        [SerializeField] private Button switchCameraButton;
        [SerializeField] private Dropdown cameraDropdown;

        [Header("Localization UI Elements")]
        [SerializeField] private Canvas languageOverlayCanvas;
        [SerializeField] private Dropdown languageDropdown;
        [SerializeField] private Text titleText;
        [SerializeField] private Text historyButtonText;
        [SerializeField] private Text historyTitleText;
        [SerializeField] private Text clearHistoryButtonText;
        [SerializeField] private Button scanButton;
        [SerializeField] private Button galleryButton;
        [SerializeField] private Button statusButton;
        [SerializeField] private Button closeResultButton;

        [Header("History UI Elements")]
        [SerializeField] private Canvas historyOverlayCanvas;
        [SerializeField] private Button historyButton;
        [SerializeField] private GameObject historyPanel;
        [SerializeField] private Button closeHistoryButton;
        [SerializeField] private Button clearHistoryButton;
        [SerializeField] private Transform historyContentContainer;
        [SerializeField] private Text historyDebugText;

        [Header("AR / Editor Environment Roots")]
        [SerializeField] private GameObject arMobileRoot;
        [SerializeField] private GameObject pcCamera;

        private ICameraCaptureProvider activeCaptureProvider;
        private byte[] lastCapturedJpegBytes;
        private bool openedResultFromHistory = false;

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

            // Load and setup language dropdown
            defaultLanguage = PlayerPrefs.GetString("SavedLanguage", "pl");
            int savedLangIndex = PlayerPrefs.GetInt("SavedLanguageIndex", defaultLanguage == "en" ? 1 : (defaultLanguage == "el" ? 2 : 0));
            
            if (languageDropdown != null)
            {
                languageDropdown.value = savedLangIndex;
                languageDropdown.onValueChanged.RemoveAllListeners();
                languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
            }

            ARHerb.Localization.LocalizationManager.CurrentLanguage = defaultLanguage;

            // Load selected mode index and setup listener
            if (modeDropdown != null)
            {
                int savedMode = PlayerPrefs.GetInt("SavedMode", 0);
                modeDropdown.value = savedMode;
                modeDropdown.onValueChanged.RemoveAllListeners();
                modeDropdown.onValueChanged.AddListener(OnModeChanged);
                SetupModeButtonTiles();
            }

            // Guarantee all UI buttons (Scan, Gallery, History, Maps, Zoom) exist and are setup
            EnsureAllButtonsSetup();
            EnsureSettingsPanelSetup();
            EnsureLanguageDropdownVisible();
            EnsureZoomButtonSetup();
            EnsureCameraControlBarSetup();
            AttachButtonIcons();
            SetScanningFrameVisible(true);

            Debug.Log($"[History] HistoryButton assigned = {(historyButton != null)}");
            Debug.Log($"[History] HistoryPanel assigned = {(historyPanel != null)}");
            Debug.Log($"[History] HistoryContentContainer assigned = {(historyContentContainer != null)}");

            if (scanButton != null)
            {
                scanButton.onClick.RemoveAllListeners();
                scanButton.onClick.AddListener(OnScanButtonClicked);
            }
            if (statusButton != null)
            {
                statusButton.onClick.RemoveAllListeners();
                statusButton.onClick.AddListener(ResetToMainScanningView);
            }
            if (closeResultButton != null)
            {
                closeResultButton.onClick.RemoveAllListeners();
                closeResultButton.onClick.AddListener(OnCloseResultButtonClicked);
            }
            EnsureResultOpenInMapsButtonSetup();

            if (closeHistoryButton != null)
            {
                closeHistoryButton.onClick.RemoveAllListeners();
                closeHistoryButton.onClick.AddListener(OnCloseHistoryButtonClicked);
            }
            if (clearHistoryButton != null)
            {
                clearHistoryButton.onClick.RemoveAllListeners();
                clearHistoryButton.onClick.AddListener(OnClearHistoryButtonClicked);
            }
            if (historyPanel != null) historyPanel.SetActive(false);

            // Hide result panel and thumbnail on start
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
            if (thumbnailPreviewUI != null)
            {
                thumbnailPreviewUI.gameObject.SetActive(false);
            }

            ApplyLanguageTranslations();

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

        public void SetupModeButtonTiles()
        {
            if (modeDropdown == null) return;
            Transform container = modeDropdown.transform;
            string[] buttonNames = new string[] { "ModeAutoButton", "ModePlantsButton", "ModeMushroomsButton", "ModeStonesButton", "ModeInsectsButton" };
            int[] modeValues = new int[] { 0, 0, 1, 3, 2 };

            for (int i = 0; i < buttonNames.Length; i++)
            {
                int tileIndex = i;
                int modeValue = modeValues[i];
                Transform btnTr = container.Find(buttonNames[i]);
                if (btnTr != null)
                {
                    Button btn = btnTr.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => {
                            SelectModeTile(tileIndex, modeValue);
                        });
                    }
                }
            }

            int savedTile = PlayerPrefs.GetInt("SavedTileIndex", 0);
            UpdateModeTileVisuals(savedTile);
        }

        public void SelectModeTile(int tileIndex, int modeValue)
        {
            if (modeDropdown != null)
            {
                modeDropdown.value = modeValue;
            }
            PlayerPrefs.SetInt("SavedMode", modeValue);
            PlayerPrefs.SetInt("SavedTileIndex", tileIndex);
            PlayerPrefs.Save();
            UpdateModeTileVisuals(tileIndex);
        }

        public void UpdateModeTileVisuals(int activeTileIndex)
        {
            if (modeDropdown == null) return;
            Transform container = modeDropdown.transform;
            string[] buttonNames = new string[] { "ModeAutoButton", "ModePlantsButton", "ModeMushroomsButton", "ModeStonesButton", "ModeInsectsButton" };
            string[] langKeys = new string[] { "mode_auto", "mode_plants", "mode_mushrooms", "mode_stones", "mode_insects" };

            for (int i = 0; i < buttonNames.Length; i++)
            {
                Transform btnTr = container.Find(buttonNames[i]);
                if (btnTr != null)
                {
                    Image bg = btnTr.GetComponent<Image>();
                    if (bg != null)
                    {
                        bg.color = (i == activeTileIndex) 
                            ? new Color(0.18f, 0.80f, 0.44f, 1.0f) 
                            : new Color(1.0f, 1.0f, 1.0f, 1.0f);
                    }

                    Transform iconTr = btnTr.Find("Icon");
                    if (iconTr != null)
                    {
                        Image iconImg = iconTr.GetComponent<Image>();
                        if (iconImg != null)
                        {
                            iconImg.color = (i == activeTileIndex) ? Color.white : new Color(0.15f, 0.18f, 0.22f, 1.0f);
                        }
                    }

                    Transform textTr = btnTr.Find("Text");
                    if (textTr != null)
                    {
                        Text txt = textTr.GetComponent<Text>();
                        if (txt != null)
                        {
                            txt.text = ARHerb.Localization.LocalizationManager.Get(langKeys[i]);
                            txt.color = (i == activeTileIndex) ? Color.white : new Color(0.15f, 0.18f, 0.22f, 1.0f);
                        }
                    }
                }
            }
        }

        private void OnLanguageChanged(int index)
        {
            string langCode = "pl";
            if (index == 1) langCode = "en";
            else if (index == 2) langCode = "el";

            defaultLanguage = langCode;
            ARHerb.Localization.LocalizationManager.CurrentLanguage = langCode;
            PlayerPrefs.SetString("SavedLanguage", langCode);
            PlayerPrefs.SetInt("SavedLanguageIndex", index);
            PlayerPrefs.Save();

            ApplyLanguageTranslations();
            Debug.Log($"[UIManager] Saved selected language: {langCode} (index {index})");
        }

        private void ApplyLanguageTranslations()
        {
            if (titleText != null) titleText.text = ARHerb.Localization.LocalizationManager.Get("app_title");
            if (historyButtonText != null) historyButtonText.text = ARHerb.Localization.LocalizationManager.Get("btn_history");
            if (historyTitleText != null) historyTitleText.text = ARHerb.Localization.LocalizationManager.Get("history_title");
            if (clearHistoryButtonText != null) clearHistoryButtonText.text = ARHerb.Localization.LocalizationManager.Get("history_clear_btn");
            if (resultOpenInMapsButton != null)
            {
                Text btnTxt = resultOpenInMapsButton.GetComponentInChildren<Text>();
                if (btnTxt != null) btnTxt.text = ARHerb.Localization.LocalizationManager.Get("btn_open_maps");
            }

            // Refresh Mode Tile Button Texts
            if (modeDropdown != null)
            {
                Transform container = modeDropdown.transform;
                string[] buttonNames = new string[] { "ModeAutoButton", "ModePlantsButton", "ModeMushroomsButton", "ModeStonesButton", "ModeInsectsButton" };
                string[] langKeys = new string[] { "mode_auto", "mode_plants", "mode_mushrooms", "mode_stones", "mode_insects" };

                for (int i = 0; i < buttonNames.Length; i++)
                {
                    Transform btnTr = container.Find(buttonNames[i]);
                    if (btnTr != null)
                    {
                        Transform textTr = btnTr.Find("Text");
                        if (textTr != null)
                        {
                            Text txt = textTr.GetComponent<Text>();
                            if (txt != null)
                            {
                                txt.text = ARHerb.Localization.LocalizationManager.Get(langKeys[i]);
                            }
                        }
                    }
                }
            }

            if (modeDropdown != null && modeDropdown.options != null && modeDropdown.options.Count >= 4)
            {
                modeDropdown.options[0].text = ARHerb.Localization.LocalizationManager.Get("mode_plants");
                modeDropdown.options[1].text = ARHerb.Localization.LocalizationManager.Get("mode_mushrooms");
                modeDropdown.options[2].text = ARHerb.Localization.LocalizationManager.Get("mode_insects");
                modeDropdown.options[3].text = ARHerb.Localization.LocalizationManager.Get("mode_stones");
                modeDropdown.RefreshShownValue();
            }

            if (!isScanning)
            {
                SetScanButtonState(true, ARHerb.Localization.LocalizationManager.Get("btn_scan"));
                SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_ready"), StatusType.Ready);
            }

            // If Result Panel is currently open, dynamically refresh AI enrichment description in the new language
            if (resultPanel != null && resultPanel.activeSelf && !string.IsNullOrEmpty(currentCommonName))
            {
                EnsureResultOpenInMapsButtonSetup();
                if (backendClient != null)
                {
                    string sciName = (scientificNameText != null) ? scientificNameText.text : "";
                    string queryName = string.IsNullOrEmpty(sciName) ? currentCommonName : sciName;
                    backendClient.EnrichPlant(queryName, null, defaultLanguage, 
                        onSuccess: enrichRes => {
                            DisplayResultUI(currentCommonName, sciName, 0.95f, enrichRes?.enrichment);
                        },
                        onFailure: err => Debug.LogWarning($"[UIManager] Enrichment language update error: {err}")
                    );
                }
            }
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
                if (activeCaptureProvider is MobileWebcamCaptureProvider mobileProv)
                {
                    mobileProv.OnZoomChanged += (z) => UpdateZoomButtonUI();
                }
                EnsureZoomButtonSetup();
                EnsureCameraControlBarSetup();
            }
        }

        public enum StatusType { Ready, Loading, Success, Error }

        private bool isScanning = false;

        private void OnScanButtonClicked()
        {
            if (isScanning) return;
            isScanning = true;
            openedResultFromHistory = false;

            if (activeCaptureProvider == null)
            {
                SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_camera_error"), StatusType.Error);
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
            SetScanningFrameVisible(false);

            SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_capturing"), StatusType.Loading);
            SetScanButtonState(false, ARHerb.Localization.LocalizationManager.Get("btn_wait"));

            // Start 15s watchdog to guarantee UI never gets stuck in isScanning = true
            StopCoroutine("ScanTimeoutWatchdog");
            StartCoroutine("ScanTimeoutWatchdog");

            // Capture the image from the active provider (Editor webcam or mobile webcam)
            activeCaptureProvider.CaptureFrame(jpegBytes =>
            {
                if (jpegBytes == null || jpegBytes.Length == 0)
                {
                    StopCoroutine("ScanTimeoutWatchdog");
                    SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_camera_error"), StatusType.Error);
                    SetScanButtonState(true, ARHerb.Localization.LocalizationManager.Get("btn_scan"));
                    isScanning = false;
                    return;
                }

                lastCapturedJpegBytes = jpegBytes;

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
                SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_sending"), StatusType.Loading);

                // Call /api/identify endpoint
                backendClient.IdentifySpecimen(
                    jpegBytes,
                    selectedMode,
                    defaultLanguage,
                    onSuccess: scanResult =>
                    {
                        StopCoroutine("ScanTimeoutWatchdog");
                        SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_analyzing"), StatusType.Loading);
                        ProcessIdentifyResult(scanResult, selectedMode);
                    },
                    onFailure: error =>
                    {
                        StopCoroutine("ScanTimeoutWatchdog");
                        Debug.LogError($"[UIManager] Backend Error: {error}");
                        if (!string.IsNullOrEmpty(error) && (error.Contains("No matching") || error.Contains("404")))
                        {
                            SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_no_match"), StatusType.Error);
                        }
                        else if (Application.internetReachability == NetworkReachability.NotReachable)
                        {
                            SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_no_internet"), StatusType.Error);
                        }
                        else
                        {
                            SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_backend_error"), StatusType.Error);
                        }
                        SetScanButtonState(true, ARHerb.Localization.LocalizationManager.Get("btn_scan"));
                        isScanning = false;
                    }
                );
            });
        }

        private System.Collections.IEnumerator ScanTimeoutWatchdog()
        {
            yield return new WaitForSeconds(15f);
            if (isScanning)
            {
                Debug.LogWarning("[UIManager] Scan operation timed out after 15s. Unlocking scan button.");
                SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_backend_error"), StatusType.Error);
                SetScanButtonState(true, ARHerb.Localization.LocalizationManager.Get("btn_scan"));
                isScanning = false;
            }
        }

        private void OnGalleryButtonClicked()
        {
            if (isScanning) return;

            if (ARHerb.Utils.GalleryImagePicker.Instance == null)
            {
                gameObject.AddComponent<ARHerb.Utils.GalleryImagePicker>();
            }

            ARHerb.Utils.GalleryImagePicker.Instance.PickImage(jpegBytes =>
            {
                if (jpegBytes != null && jpegBytes.Length > 0)
                {
                    ProcessGalleryImage(jpegBytes);
                }
            });
        }

        private void ProcessGalleryImage(byte[] jpegBytes)
        {
            isScanning = true;
            openedResultFromHistory = false;

            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
            if (thumbnailPreviewUI != null)
            {
                thumbnailPreviewUI.gameObject.SetActive(false);
            }
            SetScanningFrameVisible(false);

            lastCapturedJpegBytes = jpegBytes;

            // Show selected gallery image preview
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
            SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_sending"), StatusType.Loading);
            SetScanButtonState(false, ARHerb.Localization.LocalizationManager.Get("btn_wait"));

            // Call /api/identify with selected gallery image bytes
            backendClient.IdentifySpecimen(
                jpegBytes,
                selectedMode,
                defaultLanguage,
                onSuccess: scanResult =>
                {
                    SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_analyzing"), StatusType.Loading);
                    ProcessIdentifyResult(scanResult, selectedMode);
                },
                onFailure: error =>
                {
                    Debug.LogError($"[UIManager] Backend Error (Gallery): {error}");
                    if (!string.IsNullOrEmpty(error) && (error.Contains("No matching") || error.Contains("404")))
                    {
                        SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_no_match"), StatusType.Error);
                    }
                    else if (Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_no_internet"), StatusType.Error);
                    }
                    else
                    {
                        SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_backend_error"), StatusType.Error);
                    }
                    SetScanButtonState(true, ARHerb.Localization.LocalizationManager.Get("btn_scan"));
                    isScanning = false;
                }
            );
        }

        /// <summary>
        /// Processes the result returned from /api/identify.
        /// If the mode is plants, it automatically triggers /api/enrich to fetch description and fun facts.
        /// Saves successful scans to local history.
        /// </summary>
        private void ProcessIdentifyResult(ScanResult result, string mode)
        {
            if (result == null || result.results == null || result.results.Count == 0)
            {
                SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_no_match"), StatusType.Error);
                SetScanButtonState(true, ARHerb.Localization.LocalizationManager.Get("btn_scan"));
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
                SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_loading_details"), StatusType.Loading);
                
                string loadingStr = ARHerb.Localization.LocalizationManager.Get("status_loading_details");
                if (descriptionText != null) descriptionText.text = loadingStr;
                if (funFactText != null) funFactText.text = loadingStr;
                if (edibilityText != null) edibilityText.text = $"{ARHerb.Localization.LocalizationManager.Get("edibility_label")}: {loadingStr}";

                List<string> commonNames = bestCandidate.species?.commonNames ?? new List<string>();

                backendClient.EnrichPlant(
                    scientificName,
                    commonNames,
                    defaultLanguage,
                    onSuccess: enrichRes =>
                    {
                        DisplayResultUI(commonName, scientificName, confidenceScore, enrichRes?.enrichment);
                        
                        // Save to local history with GPS location
                        SaveScanWithGPS(
                            commonName,
                            scientificName,
                            mode,
                            confidenceScore,
                            enrichRes?.enrichment?.description,
                            enrichRes?.enrichment?.funFact,
                            enrichRes?.enrichment?.edibleStatus,
                            enrichRes?.enrichment?.edibleNote,
                            lastCapturedJpegBytes
                        );

                        SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_done_return"), StatusType.Success);
                        SetScanButtonState(true, ARHerb.Localization.LocalizationManager.Get("btn_scan"));
                        isScanning = false;
                    },
                    onFailure: error =>
                    {
                        Debug.LogError($"[UIManager] Enrichment Failure: {error}");
                        DisplayResultUI(commonName, scientificName, confidenceScore, null);
                        if (descriptionText != null) descriptionText.text = ARHerb.Localization.LocalizationManager.Get("status_ai_error");

                        // Save basic scan to local history with GPS location
                        SaveScanWithGPS(
                            commonName,
                            scientificName,
                            mode,
                            confidenceScore,
                            ARHerb.Localization.LocalizationManager.Get("status_ai_error"),
                            null,
                            null,
                            null,
                            lastCapturedJpegBytes
                        );

                        SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_done_return"), StatusType.Success);
                        SetScanButtonState(true, ARHerb.Localization.LocalizationManager.Get("btn_scan"));
                        isScanning = false;
                    }
                );
            }
            else
            {
                // In other modes (mushrooms, insects, stones), the backend directly packages the enrichment.
                DisplayResultUI(commonName, scientificName, confidenceScore, result.enrichment);
                
                // Save to local history with GPS location
                SaveScanWithGPS(
                    commonName,
                    scientificName,
                    mode,
                    confidenceScore,
                    result.enrichment?.description,
                    result.enrichment?.funFact,
                    result.enrichment?.edibleStatus,
                    result.enrichment?.edibleNote,
                    lastCapturedJpegBytes
                );

                SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_done_return"), StatusType.Success);
                SetScanButtonState(true, ARHerb.Localization.LocalizationManager.Get("btn_scan"));
                isScanning = false;
            }
        }

        private void SaveScanWithGPS(
            string commonName,
            string scientificName,
            string mode,
            float score,
            string description,
            string funFact,
            string edibleStatus,
            string edibleNote,
            byte[] jpegBytes)
        {
            if (ARHerb.Location.GPSLocationManager.Instance == null)
            {
                gameObject.AddComponent<ARHerb.Location.GPSLocationManager>();
            }

            StartCoroutine(ARHerb.Location.GPSLocationManager.Instance.FetchLocationCoroutine((hasLoc, lat, lng) =>
            {
                currentScanHasLocation = hasLoc;
                currentScanLat = lat;
                currentScanLng = lng;

                if (resultGpsLabel != null)
                {
                    string latStr = lat.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                    string lngStr = lng.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                    resultGpsLabel.text = hasLoc 
                        ? $"📍 GPS: {latStr}, {lngStr}" 
                        : ARHerb.Localization.LocalizationManager.Get("location_no_data");
                }
                if (resultOpenInMapsButton != null)
                {
                    resultOpenInMapsButton.gameObject.SetActive(true);
                }

                ARHerb.History.ScanHistoryManager.SaveScan(
                    commonName,
                    scientificName,
                    mode,
                    score,
                    description,
                    funFact,
                    edibleStatus,
                    edibleNote,
                    jpegBytes,
                    hasLoc,
                    lat,
                    lng
                );
            }, 5f));
        }

        public void ResetToMainScanningView()
        {
            openedResultFromHistory = false;
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
            if (thumbnailPreviewUI != null)
            {
                thumbnailPreviewUI.gameObject.SetActive(false);
            }
            if (historyPanel != null)
            {
                historyPanel.SetActive(false);
            }
            SetScanningFrameVisible(true);
            SetStatusText("Gotowy do skanowania", StatusType.Ready);
            SetScanButtonState(true, "SCAN");
            isScanning = false;
        }

        private void OnCloseResultButtonClicked()
        {
            if (openedResultFromHistory)
            {
                openedResultFromHistory = false;
                if (resultPanel != null) resultPanel.SetActive(false);
                if (thumbnailPreviewUI != null) thumbnailPreviewUI.gameObject.SetActive(false);
                if (historyPanel != null)
                {
                    historyPanel.SetActive(true);
                    RefreshHistoryUI();
                }
            }
            else
            {
                ResetToMainScanningView();
            }
        }

        private GameObject runtimeHistoryOverlayCanvasGo;

        private void OnHistoryButtonClicked()
        {
            Debug.Log("[History] Button clicked");
            Debug.Log("[History] OnHistoryButtonClicked called");

            SetStatusText("History opened", StatusType.Loading);
            SetScanningFrameVisible(false);

            OpenRuntimeHistoryOverlay();
        }

        private void OpenRuntimeHistoryOverlay()
        {
            if (runtimeHistoryOverlayCanvasGo != null)
            {
                Destroy(runtimeHistoryOverlayCanvasGo);
            }

            Debug.Log("[HistoryRuntime] Creating runtime overlay");

            // 1. Create Canvas (Sorting Order 32767)
            runtimeHistoryOverlayCanvasGo = new GameObject("RuntimeHistoryOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = runtimeHistoryOverlayCanvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;

            CanvasScaler scaler = runtimeHistoryOverlayCanvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            scaler.matchWidthOrHeight = 0.5f;

            Debug.Log("[HistoryRuntime] Overlay canvas sortingOrder = 32767");

            // 2. Create Panel (Full Screen)
            GameObject panelGo = new GameObject("RuntimeHistoryPanel", typeof(Image), typeof(CanvasGroup));
            panelGo.transform.SetParent(runtimeHistoryOverlayCanvasGo.transform, false);

            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.localScale = Vector3.one;

            Image bgImage = panelGo.GetComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.95f); // Black 0.95 alpha
            bgImage.raycastTarget = true;

            CanvasGroup cg = panelGo.GetComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            // 3. Debug Text (Top Center)
            GameObject debugGo = new GameObject("DebugText", typeof(Text));
            debugGo.transform.SetParent(panelGo.transform, false);
            RectTransform debugRect = debugGo.GetComponent<RectTransform>();
            debugRect.anchorMin = new Vector2(0.05f, 0.92f);
            debugRect.anchorMax = new Vector2(0.95f, 0.98f);
            debugRect.sizeDelta = Vector2.zero;

            Text debugTxt = debugGo.GetComponent<Text>();
            debugTxt.text = "";
            debugGo.SetActive(false);

            // 4. Title Text
            GameObject titleGo = new GameObject("TitleText", typeof(Text));
            titleGo.transform.SetParent(panelGo.transform, false);
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.85f);
            titleRect.anchorMax = new Vector2(0.75f, 0.91f);
            titleRect.sizeDelta = Vector2.zero;

            Text titleTxt = titleGo.GetComponent<Text>();
            titleTxt.text = ARHerb.Localization.LocalizationManager.Get("history_title");
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.fontSize = 28;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleLeft;
            titleTxt.color = Color.white;
            titleTxt.raycastTarget = false;

            // 5. Giant Close Button (Top Right)
            GameObject closeGo = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panelGo.transform, false);
            RectTransform closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.78f, 0.85f);
            closeRect.anchorMax = new Vector2(0.95f, 0.91f);
            closeRect.sizeDelta = Vector2.zero;

            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.85f, 0.15f, 0.15f, 1f);
            closeImg.raycastTarget = true;

            GameObject closeTxtGo = new GameObject("Text", typeof(Text));
            closeTxtGo.transform.SetParent(closeGo.transform, false);
            RectTransform closeTxtRect = closeTxtGo.GetComponent<RectTransform>();
            closeTxtRect.anchorMin = Vector2.zero;
            closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.sizeDelta = Vector2.zero;

            Text closeTxt = closeTxtGo.GetComponent<Text>();
            closeTxt.text = "✕";
            closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeTxt.fontSize = 32;
            closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.color = Color.white;
            closeTxt.raycastTarget = false;

            Button closeBtn = closeGo.GetComponent<Button>();
            closeBtn.onClick.AddListener(() =>
            {
                Debug.Log("[History] Close clicked");
                if (runtimeHistoryOverlayCanvasGo != null)
                {
                    Destroy(runtimeHistoryOverlayCanvasGo);
                }
                SetScanningFrameVisible(true);
            });

            // 6. Content Container (Scroll View)
            GameObject scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(panelGo.transform, false);
            RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.05f, 0.05f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.82f);
            scrollRect.sizeDelta = Vector2.zero;

            Image scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(0.08f, 0.09f, 0.12f, 0.8f);

            ScrollRect sRect = scrollGo.GetComponent<ScrollRect>();
            sRect.horizontal = false;
            sRect.vertical = true;

            GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            RectTransform viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportGo.GetComponent<Image>().color = Color.white;
            Mask mask = viewportGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;
            sRect.viewport = viewportRect;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            RectTransform contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(10, 10, 10, 10);

            ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sRect.content = contentRect;

            // Load and Render Items
            var historyData = ARHerb.History.ScanHistoryManager.LoadHistory();
            int count = (historyData != null && historyData.items != null) ? historyData.items.Count : 0;
            Debug.Log($"[HistoryRuntime] Saved scans count = {count}");
            Debug.Log($"[HistoryRuntime] Overlay active = {runtimeHistoryOverlayCanvasGo.activeInHierarchy}");

            if (count == 0)
            {
                GameObject emptyGo = new GameObject("EmptyText", typeof(Text), typeof(LayoutElement));
                emptyGo.transform.SetParent(contentGo.transform, false);
                Text emptyTxt = emptyGo.GetComponent<Text>();
                emptyTxt.text = "Brak zapisanych skanów w historii.";
                emptyTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                emptyTxt.fontSize = 20;
                emptyTxt.alignment = TextAnchor.MiddleCenter;
                emptyTxt.color = new Color(0.7f, 0.7f, 0.7f);
                emptyTxt.fontStyle = FontStyle.Italic;
                emptyTxt.raycastTarget = false;

                LayoutElement le = emptyGo.GetComponent<LayoutElement>();
                le.preferredHeight = 120f;
            }
            else
            {
                foreach (var item in historyData.items)
                {
                    CreateRuntimeHistoryCard(contentGo.transform, item);
                }
            }
        }

        private void CreateRuntimeHistoryCard(Transform container, ARHerb.History.HistoryItem item)
        {
            GameObject cardGo = new GameObject($"Item_{item.id}", typeof(Image), typeof(Button), typeof(LayoutElement));
            cardGo.transform.SetParent(container, false);

            Image cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.14f, 0.16f, 0.22f, 0.95f);
            cardImg.raycastTarget = true;

            LayoutElement le = cardGo.GetComponent<LayoutElement>();
            le.preferredHeight = 90f;

            // Card click listener to open main result panel for this specimen
            Button cardBtn = cardGo.GetComponent<Button>();
            cardBtn.onClick.AddListener(() =>
            {
                string reqId = System.Guid.NewGuid().ToString().Substring(0, 8);
                string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");

                Debug.Log($"[REQ-{reqId}] [{timestamp}] STAGE 1: History item clicked - ID: '{item.id}', CommonName: '{item.commonName}', ScientificName: '{item.scientificName}'");
                Debug.Log($"[REQ-{reqId}] [{timestamp}] STAGE 2: Current selected language: '{defaultLanguage}'");
                Debug.Log($"[REQ-{reqId}] [{timestamp}] STAGE 3: Saved original history item text - CommonName: '{item.commonName}', Description: '{item.description}'");

                // 1. Close history overlay canvas
                if (runtimeHistoryOverlayCanvasGo != null)
                {
                    Destroy(runtimeHistoryOverlayCanvasGo);
                }

                // 2. Set GPS location data
                currentScanHasLocation = item.hasLocation;
                currentScanLat = item.latitude;
                currentScanLng = item.longitude;
                openedResultFromHistory = true;

                // 3. Immediately display ResultPanel with loading placeholders in active language
                string loadingPlaceholder = ARHerb.Localization.LocalizationManager.Get("status_loading_details");
                EnrichmentData loadingEnrichment = new EnrichmentData
                {
                    commonName = item.commonName,
                    description = loadingPlaceholder,
                    funFact = loadingPlaceholder,
                    edibleStatus = item.edibleStatus,
                    edibleNote = loadingPlaceholder
                };

                DisplayResultUI(item.commonName, item.scientificName, item.score, loadingEnrichment, reqId);

                // 4. Request translated/enriched content from backend in current active language with 15s timeout
                if (backendClient != null)
                {
                    string queryName = !string.IsNullOrEmpty(item.scientificName) ? item.scientificName : item.commonName;
                    Debug.Log($"[REQ-{reqId}] [{timestamp}] STAGE 4: EnrichPlant called for history item. Query: '{queryName}', Language: '{defaultLanguage}'");

                    bool enrichmentCompleted = false;

                    backendClient.EnrichPlant(queryName, null, defaultLanguage,
                        onSuccess: enrichRes => {
                            if (enrichmentCompleted) return;
                            enrichmentCompleted = true;

                            string resTimestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
                            Debug.Log($"[REQ-{reqId}] [{resTimestamp}] STAGE 5: HTTP 200 Success received from backend");
                            if (enrichRes != null && enrichRes.enrichment != null)
                            {
                                Debug.Log($"[REQ-{reqId}] [{resTimestamp}] STAGE 6: Parsed backend response - Description: '{enrichRes.enrichment.description}', FunFact: '{enrichRes.enrichment.funFact}'");

                                string finalCommonName = !string.IsNullOrEmpty(enrichRes.enrichment.commonName) 
                                    ? enrichRes.enrichment.commonName 
                                    : item.commonName;

                                DisplayResultUI(finalCommonName, item.scientificName, item.score, enrichRes.enrichment, reqId);
                            }
                        },
                        onFailure: err => {
                            if (enrichmentCompleted) return;
                            enrichmentCompleted = true;

                            string errTimestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
                            Debug.LogWarning($"[REQ-{reqId}] [{errTimestamp}] STAGE 5: HTTP Failure: {err}");

                            // Fallback to saved history item data on failure
                            EnrichmentData fallbackEnrichment = new EnrichmentData
                            {
                                commonName = item.commonName,
                                description = item.description,
                                funFact = item.funFact,
                                edibleStatus = item.edibleStatus,
                                edibleNote = item.edibleNote
                            };

                            DisplayResultUI(item.commonName, item.scientificName, item.score, fallbackEnrichment, reqId);
                            ShowWarningToast(ARHerb.Localization.LocalizationManager.Get("status_backend_error"));
                        }
                    );

                    StartCoroutine(EnrichTimeoutFallback(15f, reqId, () => {
                        if (!enrichmentCompleted)
                        {
                            enrichmentCompleted = true;
                            Debug.LogWarning($"[REQ-{reqId}] STAGE 5: 15-second timeout reached. Falling back to saved history data.");
                            EnrichmentData fallbackEnrichment = new EnrichmentData
                            {
                                commonName = item.commonName,
                                description = item.description,
                                funFact = item.funFact,
                                edibleStatus = item.edibleStatus,
                                edibleNote = item.edibleNote
                            };

                            DisplayResultUI(item.commonName, item.scientificName, item.score, fallbackEnrichment, reqId);
                            ShowWarningToast(ARHerb.Localization.LocalizationManager.Get("status_backend_error"));
                        }
                    }));
                }
                else
                {
                    // Fallback immediately if backendClient is missing
                    EnrichmentData fallbackEnrichment = new EnrichmentData
                    {
                        commonName = item.commonName,
                        description = item.description,
                        funFact = item.funFact,
                        edibleStatus = item.edibleStatus,
                        edibleNote = item.edibleNote
                    };
                    DisplayResultUI(item.commonName, item.scientificName, item.score, fallbackEnrichment, reqId);
                }

                // 5. Load and show thumbnail preview image if available
                if (!string.IsNullOrEmpty(item.thumbnailPath) && System.IO.File.Exists(item.thumbnailPath))
                {
                    try
                    {
                        byte[] bytes = System.IO.File.ReadAllBytes(item.thumbnailPath);
                        Texture2D tex = new Texture2D(2, 2);
                        if (tex.LoadImage(bytes) && thumbnailPreviewUI != null)
                        {
                            if (thumbnailPreviewUI.texture != null && thumbnailPreviewUI.texture is Texture2D dynamicTex)
                            {
                                Destroy(dynamicTex);
                            }
                            thumbnailPreviewUI.texture = tex;
                            thumbnailPreviewUI.gameObject.SetActive(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UIManager] Error loading item thumbnail from history: {ex.Message}");
                    }
                }

                // 6. Ensure result panel is active and brought to front
                if (resultPanel != null)
                {
                    resultPanel.SetActive(true);
                    resultPanel.transform.SetAsLastSibling();
                }

                SetStatusText($"Wczytano z historii: {item.commonName}", StatusType.Success);
            });

            // Text info
            GameObject infoGo = new GameObject("InfoText", typeof(Text));
            infoGo.transform.SetParent(cardGo.transform, false);
            RectTransform infoRect = infoGo.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0.04f, 0.05f);
            infoRect.anchorMax = new Vector2(0.60f, 0.95f);
            infoRect.sizeDelta = Vector2.zero;

            Text infoTxt = infoGo.GetComponent<Text>();
            string noGpsLabel = ARHerb.Localization.LocalizationManager.Get("no_gps");
            string gpsInfo = item.hasLocation 
                ? $"📍 GPS: {item.latitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, {item.longitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}"
                : $"📍 {noGpsLabel}";

            string modeKey = string.IsNullOrEmpty(item.mode) ? "mode_auto" : $"mode_{item.mode.ToLower()}";
            string translatedMode = ARHerb.Localization.LocalizationManager.Get(modeKey).ToUpper();

            infoTxt.text = $"<b>{item.commonName}</b>\n<i>{item.scientificName}</i>\n<color=#80C0FF>{translatedMode} • {gpsInfo}</color>";
            infoTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            infoTxt.fontSize = 13;
            infoTxt.color = Color.white;
            infoTxt.alignment = TextAnchor.MiddleLeft;
            infoTxt.raycastTarget = false;

            // Maps Button inside card
            GameObject mapsBtnGo = new GameObject("MapsButton", typeof(Image), typeof(Button));
            mapsBtnGo.transform.SetParent(cardGo.transform, false);
            RectTransform mapsRect = mapsBtnGo.GetComponent<RectTransform>();
            mapsRect.anchorMin = new Vector2(0.63f, 0.15f);
            mapsRect.anchorMax = new Vector2(0.96f, 0.85f);
            mapsRect.sizeDelta = Vector2.zero;

            Image mImg = mapsBtnGo.GetComponent<Image>();
            bool hasLoc = item.hasLocation;
            mImg.color = hasLoc ? new Color(0.12f, 0.45f, 0.9f, 1.0f) : new Color(0.3f, 0.35f, 0.4f, 0.8f);

            GameObject mTxtGo = new GameObject("Text", typeof(Text));
            mTxtGo.transform.SetParent(mapsBtnGo.transform, false);
            RectTransform mTxtRect = mTxtGo.GetComponent<RectTransform>();
            mTxtRect.anchorMin = Vector2.zero;
            mTxtRect.anchorMax = Vector2.one;
            mTxtRect.sizeDelta = Vector2.zero;

            Text mTxt = mTxtGo.GetComponent<Text>();
            mTxt.text = hasLoc ? ARHerb.Localization.LocalizationManager.Get("btn_open_maps") : noGpsLabel;
            mTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mTxt.fontSize = 11;
            mTxt.alignment = TextAnchor.MiddleCenter;
            mTxt.color = Color.white;
            mTxt.fontStyle = FontStyle.Bold;
            mTxt.raycastTarget = false;

            Button mBtn = mapsBtnGo.GetComponent<Button>();
            mBtn.interactable = hasLoc;
            if (hasLoc)
            {
                float lat = item.latitude;
                float lng = item.longitude;
                mBtn.onClick.AddListener(() =>
                {
                    Debug.Log("[Maps] opened from HistoryPanel");
                    Debug.Log($"[Maps] hasLocation = {hasLoc}");
                    Debug.Log($"[Maps] latitude = {lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    Debug.Log($"[Maps] longitude = {lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

                    ARHerb.Location.GPSLocationManager.OpenInGoogleMaps(lat, lng);
                });
            }
        }

        private System.Collections.IEnumerator EnrichTimeoutFallback(float seconds, string reqId, System.Action onTimeout)
        {
            yield return new WaitForSeconds(seconds);
            onTimeout?.Invoke();
        }

        private void OnCloseHistoryButtonClicked()
        {
            Debug.Log("[History] Close clicked");
            if (historyPanel != null)
            {
                historyPanel.SetActive(false);
            }
            if (historyOverlayCanvas != null)
            {
                historyOverlayCanvas.gameObject.SetActive(false);
            }
        }

        private void OnClearHistoryButtonClicked()
        {
            ARHerb.History.ScanHistoryManager.ClearHistory();
            RefreshHistoryUI();
            SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_cleared_history"), StatusType.Ready);
        }

        private void RefreshHistoryUI()
        {
            if (historyContentContainer == null) return;

            foreach (Transform child in historyContentContainer)
            {
                Destroy(child.gameObject);
            }

            var historyData = ARHerb.History.ScanHistoryManager.LoadHistory();
            int count = (historyData != null && historyData.items != null) ? historyData.items.Count : 0;
            Debug.Log($"[History] saved item count = {count}");

            if (historyData == null || historyData.items == null || historyData.items.Count == 0)
            {
                GameObject emptyGo = new GameObject("EmptyHistoryText", typeof(Text), typeof(LayoutElement));
                emptyGo.transform.SetParent(historyContentContainer, false);
                Text emptyTxt = emptyGo.GetComponent<Text>();
                emptyTxt.text = ARHerb.Localization.LocalizationManager.Get("history_empty") ?? "No scan history yet.";
                emptyTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                emptyTxt.fontSize = 15;
                emptyTxt.alignment = TextAnchor.MiddleCenter;
                emptyTxt.color = new Color(0.7f, 0.7f, 0.7f);
                emptyTxt.fontStyle = FontStyle.Italic;
                emptyTxt.raycastTarget = false;
                
                LayoutElement le = emptyGo.GetComponent<LayoutElement>();
                le.preferredHeight = 100f;
                return;
            }

            foreach (var item in historyData.items)
            {
                CreateHistoryItemUI(item);
            }
        }

        private void CreateHistoryItemUI(ARHerb.History.HistoryItem item)
        {
            GameObject cardGo = new GameObject($"HistoryItem_{item.id}", typeof(Image), typeof(Button), typeof(LayoutElement));
            cardGo.transform.SetParent(historyContentContainer, false);

            Image cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

            LayoutElement le = cardGo.GetComponent<LayoutElement>();
            le.preferredHeight = 85f;

            // Thumbnail
            GameObject thumbGo = new GameObject("Thumb", typeof(RawImage));
            thumbGo.transform.SetParent(cardGo.transform, false);
            RectTransform thumbRect = thumbGo.GetComponent<RectTransform>();
            thumbRect.anchorMin = new Vector2(0.02f, 0.1f);
            thumbRect.anchorMax = new Vector2(0.22f, 0.9f);
            thumbRect.sizeDelta = Vector2.zero;

            RawImage rawImg = thumbGo.GetComponent<RawImage>();
            rawImg.color = Color.white;

            if (!string.IsNullOrEmpty(item.thumbnailPath) && System.IO.File.Exists(item.thumbnailPath))
            {
                try
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(item.thumbnailPath);
                    Texture2D tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                    {
                        rawImg.texture = tex;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UIManager] Error loading history thumbnail: {ex.Message}");
                }
            }

            // Info container
            GameObject infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(cardGo.transform, false);
            RectTransform infoRect = infoGo.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0.25f, 0.05f);
            infoRect.anchorMax = new Vector2(0.95f, 0.95f);
            infoRect.sizeDelta = Vector2.zero;

            // Common name
            GameObject commGo = new GameObject("CommonName", typeof(Text));
            commGo.transform.SetParent(infoGo.transform, false);
            RectTransform commRect = commGo.GetComponent<RectTransform>();
            commRect.anchorMin = new Vector2(0f, 0.6f);
            commRect.anchorMax = new Vector2(1f, 1f);
            commRect.sizeDelta = Vector2.zero;
            Text commTxt = commGo.GetComponent<Text>();
            commTxt.text = item.commonName;
            commTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            commTxt.fontSize = 15;
            commTxt.fontStyle = FontStyle.Bold;
            commTxt.color = new Color(0.18f, 0.8f, 0.44f);
            commTxt.alignment = TextAnchor.MiddleLeft;

            // Scientific name
            GameObject sciGo = new GameObject("SciName", typeof(Text));
            sciGo.transform.SetParent(infoGo.transform, false);
            RectTransform sciRect = sciGo.GetComponent<RectTransform>();
            sciRect.anchorMin = new Vector2(0f, 0.3f);
            sciRect.anchorMax = new Vector2(1f, 0.6f);
            sciRect.sizeDelta = Vector2.zero;
            Text sciTxt = sciGo.GetComponent<Text>();
            sciTxt.text = item.scientificName;
            sciTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sciTxt.fontSize = 13;
            sciTxt.fontStyle = FontStyle.Italic;
            sciTxt.color = new Color(0.8f, 0.8f, 0.8f);
            sciTxt.alignment = TextAnchor.MiddleLeft;

            // Meta (mode, timestamp, score)
            GameObject metaGo = new GameObject("Meta", typeof(Text));
            metaGo.transform.SetParent(infoGo.transform, false);
            RectTransform metaRect = metaGo.GetComponent<RectTransform>();
            metaRect.anchorMin = new Vector2(0f, 0f);
            metaRect.anchorMax = new Vector2(1f, 0.3f);
            metaRect.sizeDelta = Vector2.zero;
            Text metaTxt = metaGo.GetComponent<Text>();
            metaTxt.text = $"{item.mode.ToUpper()} • {item.timestamp} • {item.score:P0}";
            metaTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            metaTxt.fontSize = 11;
            metaTxt.color = new Color(0.5f, 0.8f, 1f);
            metaTxt.alignment = TextAnchor.MiddleLeft;

            infoRect.anchorMax = new Vector2(0.68f, 0.95f);

            GameObject mapsBtnGo = new GameObject("MapsButton", typeof(Image), typeof(Button));
            mapsBtnGo.transform.SetParent(cardGo.transform, false);
            RectTransform mapsRect = mapsBtnGo.GetComponent<RectTransform>();
            mapsRect.anchorMin = new Vector2(0.70f, 0.18f);
            mapsRect.anchorMax = new Vector2(0.98f, 0.82f);
            mapsRect.sizeDelta = Vector2.zero;

            Image mapsImg = mapsBtnGo.GetComponent<Image>();
            bool itemHasLoc = item.hasLocation;
            float lat = item.latitude;
            float lng = item.longitude;

            mapsImg.color = itemHasLoc ? new Color(0.18f, 0.55f, 0.95f, 0.95f) : new Color(0.3f, 0.35f, 0.4f, 0.8f);

            GameObject mapsTxtGo = new GameObject("Text", typeof(Text));
            mapsTxtGo.transform.SetParent(mapsBtnGo.transform, false);
            RectTransform mapsTxtRect = mapsTxtGo.GetComponent<RectTransform>();
            mapsTxtRect.anchorMin = Vector2.zero;
            mapsTxtRect.anchorMax = Vector2.one;
            mapsTxtRect.sizeDelta = Vector2.zero;

            Text mapsTxt = mapsTxtGo.GetComponent<Text>();
            mapsTxt.text = itemHasLoc ? ARHerb.Localization.LocalizationManager.Get("btn_open_maps") : "Brak GPS";
            mapsTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mapsTxt.fontSize = 10;
            mapsTxt.alignment = TextAnchor.MiddleCenter;
            mapsTxt.color = Color.white;
            mapsTxt.fontStyle = FontStyle.Bold;
            mapsTxt.raycastTarget = false;

            Button mapsBtn = mapsBtnGo.GetComponent<Button>();
            mapsBtn.interactable = itemHasLoc;
            mapsBtn.onClick.RemoveAllListeners();

            if (itemHasLoc)
            {
                mapsBtn.onClick.AddListener(() =>
                {
                    Debug.Log("[Maps] opened from HistoryPanel");
                    Debug.Log($"[Maps] hasLocation = {itemHasLoc}");
                    Debug.Log($"[Maps] latitude = {lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    Debug.Log($"[Maps] longitude = {lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

                    ARHerb.Location.GPSLocationManager.OpenInGoogleMaps(lat, lng);
                });
            }

            // Button click listener
            Button cardBtn = cardGo.GetComponent<Button>();
            cardBtn.onClick.AddListener(() =>
            {
                openedResultFromHistory = true;
                currentScanHasLocation = item.hasLocation;
                currentScanLat = item.latitude;
                currentScanLng = item.longitude;

                if (historyPanel != null) historyPanel.SetActive(false);

                EnrichmentData enrichment = new EnrichmentData
                {
                    description = item.description,
                    funFact = item.funFact,
                    edibleStatus = item.edibleStatus,
                    edibleNote = item.edibleNote
                };

                DisplayResultUI(item.commonName, item.scientificName, item.score, enrichment);

                if (!string.IsNullOrEmpty(item.thumbnailPath) && System.IO.File.Exists(item.thumbnailPath))
                {
                    try
                    {
                        byte[] bytes = System.IO.File.ReadAllBytes(item.thumbnailPath);
                        Texture2D tex = new Texture2D(2, 2);
                        if (tex.LoadImage(bytes) && thumbnailPreviewUI != null)
                        {
                            if (thumbnailPreviewUI.texture != null && thumbnailPreviewUI.texture is Texture2D dynamicTex)
                            {
                                Destroy(dynamicTex);
                            }
                            thumbnailPreviewUI.texture = tex;
                            thumbnailPreviewUI.gameObject.SetActive(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UIManager] Error loading item thumbnail: {ex.Message}");
                    }
                }

                SetStatusText($"Wczytano z historii: {item.commonName}", StatusType.Success);
            });
        }

        private void DisplayResultUI(string commonName, string scientificName, float score, EnrichmentData enrichment, string reqId = "DIRECT")
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
            Debug.Log($"[REQ-{reqId}] [{timestamp}] STAGE 7/8: DisplayResultUI invoked for CommonName: '{commonName}' (Lang: '{defaultLanguage}')");

            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
                resultPanel.transform.SetAsLastSibling();

                RectTransform resRt = resultPanel.GetComponent<RectTransform>();
                if (resRt != null)
                {
                    resRt.anchorMin = Vector2.zero;
                    resRt.anchorMax = Vector2.one;
                    resRt.offsetMin = Vector2.zero;
                    resRt.offsetMax = Vector2.zero;
                }

                Image resImg = resultPanel.GetComponent<Image>();
                if (resImg != null)
                {
                    resImg.color = new Color(0.07f, 0.08f, 0.11f, 1.0f); // Solid 100% opaque dark background
                }
            }
            SetScanningFrameVisible(false);

            if (enrichment != null && !string.IsNullOrEmpty(enrichment.commonName))
            {
                commonName = enrichment.commonName;
            }

            currentCommonName = commonName;

            if (commonNameText != null)
            {
                commonNameText.text = commonName;
                Debug.Log($"[REQ-{reqId}] [{timestamp}] STAGE 7/8: commonNameText (ID:{commonNameText.GetInstanceID()}) assigned: '{commonName}'");
            }
            if (scientificNameText != null)
            {
                scientificNameText.text = scientificName;
                Debug.Log($"[REQ-{reqId}] [{timestamp}] STAGE 7/8: scientificNameText (ID:{scientificNameText.GetInstanceID()}) assigned: '{scientificName}'");
            }
            if (scoreText != null)
            {
                scoreText.text = $"{ARHerb.Localization.LocalizationManager.Get("score_label")}: {score:P1}";
            }

            if (resultGpsLabel != null)
            {
                string latStr = currentScanLat.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                string lngStr = currentScanLng.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                resultGpsLabel.text = currentScanHasLocation 
                    ? $"📍 GPS: {latStr}, {lngStr}" 
                    : ARHerb.Localization.LocalizationManager.Get("location_no_data");
            }

            EnsureResultOpenInMapsButtonSetup();
            EnsureResultPanelSwipeDismissSetup();

            if (enrichment != null)
            {
                if (descriptionText != null)
                {
                    descriptionText.text = enrichment.description;
                    Debug.Log($"[REQ-{reqId}] [{timestamp}] STAGE 7/8: descriptionText (ID:{descriptionText.GetInstanceID()}) assigned: '{enrichment.description}'");
                }
                if (funFactText != null)
                {
                    funFactText.text = enrichment.funFact;
                    Debug.Log($"[REQ-{reqId}] [{timestamp}] STAGE 7/8: funFactText (ID:{funFactText.GetInstanceID()}) assigned: '{enrichment.funFact}'");
                }
                if (edibilityText != null)
                {
                    string statusStr = GetFriendlyEdibilityStatus(enrichment.edibleStatus);
                    string edibilityLabel = string.IsNullOrEmpty(enrichment.edibleNote) 
                        ? $"{ARHerb.Localization.LocalizationManager.Get("edibility_label")}: {statusStr}" 
                        : $"{ARHerb.Localization.LocalizationManager.Get("edibility_label")}: {statusStr} - {enrichment.edibleNote}";
                    edibilityText.text = edibilityLabel;
                    Debug.Log($"[REQ-{reqId}] [{timestamp}] STAGE 7/8: edibilityText (ID:{edibilityText.GetInstanceID()}) assigned: '{edibilityLabel}'");
                }
            }
            else
            {
                string noData = ARHerb.Localization.LocalizationManager.Get("edibility_no_data");
                if (descriptionText != null) descriptionText.text = noData;
                if (funFactText != null) funFactText.text = noData;
                if (edibilityText != null) edibilityText.text = $"{ARHerb.Localization.LocalizationManager.Get("edibility_label")}: {noData}";
            }

            // Localize any static section header texts in ResultPanel
            foreach (Text t in resultPanel.GetComponentsInChildren<Text>(true))
            {
                if (t.name.Contains("DescriptionTitle") || t.name.Contains("DescriptionHeader"))
                    t.text = ARHerb.Localization.LocalizationManager.Get("description_label");
                else if (t.name.Contains("FunFactTitle") || t.name.Contains("FunFactHeader"))
                    t.text = ARHerb.Localization.LocalizationManager.Get("fun_fact_label");
                else if (t.name.Contains("EdibilityTitle") || t.name.Contains("EdibilityHeader"))
                    t.text = ARHerb.Localization.LocalizationManager.Get("edibility_label");
                else if (t.name.Contains("ScoreTitle") || t.name.Contains("ScoreHeader"))
                    t.text = ARHerb.Localization.LocalizationManager.Get("score_label");
            }
        }

        private string GetFriendlyEdibilityStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return ARHerb.Localization.LocalizationManager.Get("edibility_no_data");
            switch (status.ToLower())
            {
                case "edible": return ARHerb.Localization.LocalizationManager.Get("edibility_edible");
                case "toxic": return ARHerb.Localization.LocalizationManager.Get("edibility_toxic");
                case "both": return ARHerb.Localization.LocalizationManager.Get("edibility_both");
                case "unknown": return ARHerb.Localization.LocalizationManager.Get("edibility_unknown");
                default: return status;
            }
        }

        private void OpenResultInMaps()
        {
            Debug.Log("[Maps] opened from ResultPanel");
            Debug.Log($"[Maps] hasLocation = {currentScanHasLocation}");
            Debug.Log($"[Maps] latitude = {currentScanLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            Debug.Log($"[Maps] longitude = {currentScanLng.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

            if (currentScanHasLocation)
            {
                ARHerb.Location.GPSLocationManager.OpenInGoogleMaps(currentScanLat, currentScanLng);
            }
            else
            {
                Debug.LogWarning("[Maps] Cannot open maps: currentScanHasLocation is false.");
            }
        }

        private void EnsureResultPanelSwipeDismissSetup()
        {
            if (resultPanel == null) return;

            ResultPanelSwipeDismiss swipeComponent = resultPanel.GetComponent<ResultPanelSwipeDismiss>();
            if (swipeComponent == null)
            {
                swipeComponent = resultPanel.AddComponent<ResultPanelSwipeDismiss>();
            }

            swipeComponent.OnDismissed = () => {
                Debug.Log("[UIManager] ResultPanel swiped down to dismiss");
                SetScanningFrameVisible(true);
                SetStatusText(ARHerb.Localization.LocalizationManager.Get("status_ready"), StatusType.Ready);
                openedResultFromHistory = false;
            };

            // Top Drag Pill indicator (—)
            Transform existingPill = resultPanel.transform.Find("DragPillIndicator");
            if (existingPill == null)
            {
                GameObject pillGo = new GameObject("DragPillIndicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                pillGo.transform.SetParent(resultPanel.transform, false);

                RectTransform pillRt = pillGo.GetComponent<RectTransform>();
                pillRt.anchorMin = new Vector2(0.5f, 0.975f);
                pillRt.anchorMax = new Vector2(0.5f, 0.975f);
                pillRt.pivot = new Vector2(0.5f, 0.5f);
                pillRt.sizeDelta = new Vector2(90f, 8f);
                pillRt.anchoredPosition = Vector2.zero;

                Image pillImg = pillGo.GetComponent<Image>();
                pillImg.color = new Color(1f, 1f, 1f, 0.45f);
                pillImg.raycastTarget = false;

                pillGo.transform.SetAsLastSibling();
            }
        }

        private void EnsureResultOpenInMapsButtonSetup()
        {
            Debug.Log("[MapsButton] ShowResult called");
            Debug.Log($"[MapsButton] UIManager reference assigned = {(resultOpenInMapsButton != null)}");

            if (resultPanel == null)
            {
                Debug.LogError("[MapsButton] ResultPanel is missing!");
                return;
            }

            // 1. Ensure ResultGpsLabel exists & active
            if (resultGpsLabel == null)
            {
                Transform locTr = resultPanel.transform.Find("ResultGpsLabel");
                if (locTr == null) locTr = resultPanel.transform.Find("ResultLocationText");
                if (locTr != null) resultGpsLabel = locTr.GetComponent<Text>();
            }

            if (resultGpsLabel != null)
            {
                resultGpsLabel.gameObject.SetActive(true);
                if (currentScanHasLocation)
                {
                    string latStr = currentScanLat.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                    string lngStr = currentScanLng.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                    resultGpsLabel.text = $"📍 GPS: {latStr}, {lngStr}";
                }
                else
                {
                    resultGpsLabel.text = ARHerb.Localization.LocalizationManager.Get("location_no_data");
                }
            }

            // 2. Ensure ResultOpenInMapsButton exists & active
            if (resultOpenInMapsButton == null)
            {
                Transform btnTr = resultPanel.transform.Find("ResultOpenInMapsButton");
                if (btnTr != null)
                {
                    resultOpenInMapsButton = btnTr.GetComponent<Button>();
                }
            }

            if (resultOpenInMapsButton == null)
            {
                Debug.LogError("[MapsButton] ResultOpenInMapsButton is missing from active ResultPanel");

                // Fallback runtime creation if scene was not rebuilt via SetupUI
                GameObject resMapsBtnGo = new GameObject("ResultOpenInMapsButton", typeof(Image), typeof(Button));
                resMapsBtnGo.transform.SetParent(resultPanel.transform, false);
                resultOpenInMapsButton = resMapsBtnGo.GetComponent<Button>();
            }

            if (resultOpenInMapsButton != null)
            {
                RectTransform resMapsRect = resultOpenInMapsButton.GetComponent<RectTransform>();
                resMapsRect.anchorMin = new Vector2(0.55f, 0.02f);
                resMapsRect.anchorMax = new Vector2(0.95f, 0.14f);
                resMapsRect.sizeDelta = Vector2.zero;

                Image mapsImg = resultOpenInMapsButton.GetComponent<Image>();
                if (mapsImg == null) mapsImg = resultOpenInMapsButton.gameObject.AddComponent<Image>();
                mapsImg.color = currentScanHasLocation ? new Color(0.12f, 0.45f, 0.9f, 1.0f) : new Color(0.3f, 0.35f, 0.4f, 0.8f);
                mapsImg.raycastTarget = true;

                resultOpenInMapsButton.interactable = currentScanHasLocation;
                resultOpenInMapsButton.enabled = true;
                resultOpenInMapsButton.gameObject.SetActive(true);
                resultOpenInMapsButton.transform.SetAsLastSibling();

                // Child Text setup
                Transform txtTr = resultOpenInMapsButton.transform.Find("Text");
                if (txtTr == null)
                {
                    GameObject txtGo = new GameObject("Text", typeof(Text));
                    txtGo.transform.SetParent(resultOpenInMapsButton.transform, false);
                    txtTr = txtGo.transform;
                }

                RectTransform txtRect = txtTr.GetComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.sizeDelta = Vector2.zero;

                Text btnTxt = txtTr.GetComponent<Text>();
                if (btnTxt == null) btnTxt = txtTr.gameObject.AddComponent<Text>();
                btnTxt.text = currentScanHasLocation ? ARHerb.Localization.LocalizationManager.Get("btn_open_maps") : ARHerb.Localization.LocalizationManager.Get("no_gps");
                btnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                btnTxt.fontSize = 12;
                btnTxt.alignment = TextAnchor.MiddleCenter;
                btnTxt.color = Color.white;
                btnTxt.fontStyle = FontStyle.Bold;
                btnTxt.raycastTarget = false;

                // Event Listeners
                resultOpenInMapsButton.onClick.RemoveAllListeners();
                if (currentScanHasLocation)
                {
                    resultOpenInMapsButton.onClick.AddListener(OpenResultInMaps);
                }

                // Logs (Requirement 10 & 11)
                Debug.Log($"[MapsButton] button activeSelf = {resultOpenInMapsButton.gameObject.activeSelf}");
                Debug.Log($"[MapsButton] activeInHierarchy = {resultOpenInMapsButton.gameObject.activeInHierarchy}");
                Debug.Log($"[MapsButton] rect = {resMapsRect.anchorMin} to {resMapsRect.anchorMax}");
                Debug.Log($"[MapsButton] sibling index = {resultOpenInMapsButton.transform.GetSiblingIndex()}");
                Debug.Log("[MapsButton] listener assigned");
            }

            // 3. Ensure EdibilityPanel is resized to left half
            Transform edibPanelTr = resultPanel.transform.Find("EdibilityPanel");
            if (edibPanelTr != null)
            {
                RectTransform edibRect = edibPanelTr.GetComponent<RectTransform>();
                edibRect.anchorMin = new Vector2(0.05f, 0.02f);
                edibRect.anchorMax = new Vector2(0.53f, 0.14f);
                edibRect.sizeDelta = Vector2.zero;
            }

            // 4. Debug Text
            if (resultDebugText == null)
            {
                Transform debugTr = resultPanel.transform.Find("ResultDebugText");
                if (debugTr != null) resultDebugText = debugTr.GetComponent<Text>();
            }

            if (resultDebugText != null)
            {
                resultDebugText.text = "";
                resultDebugText.gameObject.SetActive(false);
            }
        }

        private string GetSelectedMode()
        {
            if (modeDropdown == null) return "plants";
            int val = modeDropdown.value;
            switch (val)
            {
                case 1: return "mushrooms";
                case 2: return "insects";
                case 3: return "stones";
                default: return "plants";
            }
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
                        ShowWarningToast(message);
                        break;
                }
                
                Debug.Log($"[UIManager] Status ({type}): {message}");
            }
        }

        private void ShowWarningToast(string message)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            Transform existingToast = canvas.transform.Find("WarningToastBanner");
            GameObject toastGo;
            if (existingToast != null)
            {
                toastGo = existingToast.gameObject;
            }
            else
            {
                toastGo = new GameObject("WarningToastBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                toastGo.transform.SetParent(canvas.transform, false);

                RectTransform rt = toastGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.08f, 0.78f);
                rt.anchorMax = new Vector2(0.92f, 0.85f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                Image img = toastGo.GetComponent<Image>();
                img.color = new Color(0.85f, 0.2f, 0.2f, 0.95f);

                GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                txtGo.transform.SetParent(toastGo.transform, false);

                RectTransform txtRt = txtGo.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = new Vector2(10, 5);
                txtRt.offsetMax = new Vector2(-10, -5);

                Text txt = txtGo.GetComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 18;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
            }

            Text toastText = toastGo.GetComponentInChildren<Text>();
            if (toastText != null)
            {
                toastText.text = "⚠️ " + message;
            }

            toastGo.SetActive(true);
            StopCoroutine("DismissWarningToast");
            StartCoroutine("DismissWarningToast", toastGo);
        }

        private System.Collections.IEnumerator DismissWarningToast(GameObject toastGo)
        {
            yield return new WaitForSeconds(4.0f);
            if (toastGo != null)
            {
                toastGo.SetActive(false);
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

        private void EnsureAllButtonsSetup()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            Transform safeAreaTr = canvas.transform.Find("SafeArea");
            Transform parentTr = safeAreaTr != null ? safeAreaTr : canvas.transform;

            // 1. Guarantee HistoryButton
            if (historyButton == null)
            {
                Transform histTr = parentTr.Find("TopBar/HistoryButton");
                if (histTr == null) histTr = parentTr.Find("HistoryButton");
                if (histTr != null)
                {
                    historyButton = histTr.GetComponent<Button>();
                }
            }

            if (historyButton == null)
            {
                Debug.Log("[UIManager] Creating HistoryButton at runtime...");
                GameObject histGo = new GameObject("HistoryButton", typeof(Image), typeof(Button));
                histGo.transform.SetParent(parentTr, false);
                RectTransform histRect = histGo.GetComponent<RectTransform>();
                histRect.anchorMin = new Vector2(0.70f, 0.92f);
                histRect.anchorMax = new Vector2(0.97f, 0.98f);
                histRect.sizeDelta = Vector2.zero;

                Image img = histGo.GetComponent<Image>();
                img.color = new Color(0.18f, 0.8f, 0.44f, 0.9f);

                GameObject txtGo = new GameObject("Text", typeof(Text));
                txtGo.transform.SetParent(histGo.transform, false);
                RectTransform txtRect = txtGo.GetComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.sizeDelta = Vector2.zero;

                Text txt = txtGo.GetComponent<Text>();
                txt.text = ARHerb.Localization.LocalizationManager.Get("btn_history");
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 12;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
                txt.fontStyle = FontStyle.Bold;
                txt.raycastTarget = false;
                historyButtonText = txt;

                historyButton = histGo.GetComponent<Button>();
            }

            if (historyButton != null)
            {
                historyButton.onClick.RemoveAllListeners();
                historyButton.onClick.AddListener(OnHistoryButtonClicked);
                historyButton.gameObject.SetActive(true);
            }

            // 2. Guarantee GalleryButton
            if (galleryButton == null)
            {
                Transform galTr = parentTr.Find("GalleryButton");
                if (galTr != null)
                {
                    galleryButton = galTr.GetComponent<Button>();
                }
            }

            if (galleryButton == null)
            {
                Debug.Log("[UIManager] Creating GalleryButton at runtime...");
                GameObject galGo = new GameObject("GalleryButton", typeof(Image), typeof(Button));
                galGo.transform.SetParent(parentTr, false);
                RectTransform galRect = galGo.GetComponent<RectTransform>();
                galRect.anchorMin = new Vector2(0.78f, 0.04f);
                galRect.anchorMax = new Vector2(0.95f, 0.14f);
                galRect.sizeDelta = Vector2.zero;

                Image img = galGo.GetComponent<Image>();
                img.color = new Color(0.2f, 0.55f, 0.9f, 0.95f);

                GameObject txtGo = new GameObject("Text", typeof(Text));
                txtGo.transform.SetParent(galGo.transform, false);
                RectTransform txtRect = txtGo.GetComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.sizeDelta = Vector2.zero;

                Text txt = txtGo.GetComponent<Text>();
                txt.text = "🖼️";
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 22;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
                txt.fontStyle = FontStyle.Bold;
                txt.raycastTarget = false;

                galleryButton = galGo.GetComponent<Button>();
            }

            if (galleryButton != null)
            {
                galleryButton.onClick.RemoveAllListeners();
                galleryButton.onClick.AddListener(OnGalleryButtonClicked);
                galleryButton.gameObject.SetActive(true);
            }

            // 3. Guarantee HistoryPanel setup
            EnsureHistoryPanelSetup(parentTr);
        }

        private void EnsureSettingsPanelSetup()
        {
            GameObject btnGo = FindGameObjectInScene("SettingsButton");
            GameObject panelGo = FindGameObjectInScene("SettingsPanel");

            Debug.Log($"[UIManager] SettingsButton found = {(btnGo != null)}, SettingsPanel found = {(panelGo != null)}");

            if (btnGo == null || panelGo == null) return;

            Button settingsBtn = btnGo.GetComponent<Button>();
            if (settingsBtn == null) settingsBtn = btnGo.AddComponent<Button>();

            if (backendUrlInput == null)
            {
                backendUrlInput = panelGo.GetComponentInChildren<InputField>(true);
            }

            if (backendUrlInput != null)
            {
                string savedUrl = PlayerPrefs.GetString("SavedBackendUrl", "");
                if (string.IsNullOrEmpty(savedUrl) && backendClient != null)
                {
                    savedUrl = backendClient.GetBackendUrl();
                }
                if (string.IsNullOrEmpty(savedUrl)) savedUrl = "http://localhost:3001";
                backendUrlInput.text = savedUrl;
                backendUrlInput.onEndEdit.RemoveAllListeners();
                backendUrlInput.onEndEdit.AddListener(OnBackendUrlChanged);
            }

            // Ensure panel has valid RectTransform anchors and styling
            RectTransform panelRt = panelGo.GetComponent<RectTransform>();
            if (panelRt != null)
            {
                panelRt.anchorMin = new Vector2(0.05f, 0.74f);
                panelRt.anchorMax = new Vector2(0.95f, 0.84f);
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;
            }

            Image panelImg = panelGo.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.color = new Color(0.1f, 0.13f, 0.18f, 0.98f);
            }

            Canvas extraCanvas = panelGo.GetComponent<Canvas>();
            if (extraCanvas != null) Destroy(extraCanvas);

            settingsBtn.onClick.RemoveAllListeners();
            settingsBtn.onClick.AddListener(() => {
                bool newState = !panelGo.activeSelf;
                panelGo.SetActive(newState);

                if (newState)
                {
                    panelGo.transform.SetAsLastSibling();
                }

                Debug.Log($"[UIManager] Settings panel toggled active: {newState}");
            });
        }

        private void EnsureHistoryPanelSetup(Transform parentTr)
        {
            if (historyPanel == null)
            {
                Transform hpTr = parentTr.Find("HistoryPanel");
                if (hpTr != null)
                {
                    historyPanel = hpTr.gameObject;
                }
            }

            if (historyPanel != null)
            {
                if (historyContentContainer == null)
                {
                    Transform cTr = historyPanel.transform.Find("HistoryScrollView/Viewport/Content");
                    if (cTr != null) historyContentContainer = cTr;
                }
                if (closeHistoryButton == null)
                {
                    Transform cBtn = historyPanel.transform.Find("CloseHistoryButton");
                    if (cBtn != null) closeHistoryButton = cBtn.GetComponent<Button>();
                }
                if (clearHistoryButton == null)
                {
                    Transform clBtn = historyPanel.transform.Find("ClearHistoryButton");
                    if (clBtn != null) clearHistoryButton = clBtn.GetComponent<Button>();
                }
            }

            if (closeHistoryButton != null)
            {
                closeHistoryButton.onClick.RemoveAllListeners();
                closeHistoryButton.onClick.AddListener(OnCloseHistoryButtonClicked);
            }

            if (clearHistoryButton != null)
            {
                clearHistoryButton.onClick.RemoveAllListeners();
                clearHistoryButton.onClick.AddListener(OnClearHistoryButtonClicked);
            }
        }


        private void EnsureLanguageDropdownVisible()
        {
            // 1. Disable empty overlay canvas if present so it never blocks touch/click raycasts
            if (languageOverlayCanvas != null && languageOverlayCanvas.transform.childCount == 0)
            {
                languageOverlayCanvas.gameObject.SetActive(false);
            }

            // 2. Ensure dropdown is visible and interactable
            if (languageDropdown != null)
            {
                languageDropdown.gameObject.SetActive(true);
                languageDropdown.interactable = true;

                // Ensure options are clear (PL, EN, GR)
                languageDropdown.options = new System.Collections.Generic.List<Dropdown.OptionData>
                {
                    new Dropdown.OptionData("PL"),
                    new Dropdown.OptionData("EN"),
                    new Dropdown.OptionData("GR")
                };
                languageDropdown.RefreshShownValue();

                // Ensure Template has Canvas for top-level sorting and wide layout
                if (languageDropdown.template != null)
                {
                    RectTransform templateRt = languageDropdown.template.GetComponent<RectTransform>();
                    if (templateRt != null)
                    {
                        templateRt.sizeDelta = new Vector2(180f, 220f); // Wider spacious dropdown list
                    }

                    Image templateImg = languageDropdown.template.GetComponent<Image>();
                    if (templateImg != null)
                    {
                        templateImg.color = new Color(0.95f, 0.97f, 0.95f, 0.98f); // Clean light background
                    }

                    Canvas templateCanvas = languageDropdown.template.GetComponent<Canvas>();
                    if (templateCanvas == null)
                    {
                        templateCanvas = languageDropdown.template.gameObject.AddComponent<Canvas>();
                    }
                    templateCanvas.overrideSorting = true;
                    templateCanvas.sortingOrder = 30000;

                    if (languageDropdown.template.GetComponent<GraphicRaycaster>() == null)
                    {
                        languageDropdown.template.gameObject.AddComponent<GraphicRaycaster>();
                    }

                    // Ensure each item has sufficient height (55px) and bold dark green text
                    foreach (Toggle itemToggle in languageDropdown.template.GetComponentsInChildren<Toggle>(true))
                    {
                        RectTransform itemRt = itemToggle.GetComponent<RectTransform>();
                        if (itemRt != null)
                        {
                            itemRt.sizeDelta = new Vector2(0f, 55f);
                        }
                    }

                    foreach (Text t in languageDropdown.template.GetComponentsInChildren<Text>(true))
                    {
                        t.color = new Color(0.08f, 0.48f, 0.18f); // Dark green matching button color
                        t.fontSize = 22;
                        t.fontStyle = FontStyle.Bold;
                        t.alignment = TextAnchor.MiddleCenter;
                    }
                }

                Image img = languageDropdown.GetComponent<Image>();
                if (img != null)
                {
                    Color c = img.color;
                    c.a = 1f;
                    img.color = c;
                    img.raycastTarget = true;
                }

                // Add EventTrigger to handle toggle-closing dropdown when button is clicked while list is open
                UnityEngine.EventSystems.EventTrigger trigger = languageDropdown.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                if (trigger == null) trigger = languageDropdown.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                trigger.triggers.Clear();

                UnityEngine.EventSystems.EventTrigger.Entry pointerDownEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                {
                    eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown
                };
                pointerDownEntry.callback.AddListener((data) => {
                    GameObject openList = GameObject.Find("Dropdown List");
                    if (openList == null && languageDropdown.transform.Find("Dropdown List") != null)
                    {
                        openList = languageDropdown.transform.Find("Dropdown List").gameObject;
                    }

                    if (openList != null && openList.activeSelf)
                    {
                        // Dropdown list is currently open -> close it and mark timestamp
                        lastLanguageDropdownCloseTime = Time.unscaledTime;
                        languageDropdown.Hide();
                    }
                });
                trigger.triggers.Add(pointerDownEntry);

                UnityEngine.EventSystems.EventTrigger.Entry pointerClickEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                {
                    eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick
                };
                pointerClickEntry.callback.AddListener((data) => {
                    if (Time.unscaledTime - lastLanguageDropdownCloseTime < 0.4f)
                    {
                        // Suppress instant re-opening
                        languageDropdown.Hide();
                    }
                });
                trigger.triggers.Add(pointerClickEntry);
            }
            else
            {
                // Runtime fallback: create a new language dropdown on top-level overlay canvas
                Debug.LogWarning("[Language] languageDropdown is null – creating runtime fallback");

                GameObject runtimeLangCanvas = new GameObject("RuntimeLanguageCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas c = runtimeLangCanvas.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 1000;

                CanvasScaler cs = runtimeLangCanvas.GetComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1080, 2340);
                cs.matchWidthOrHeight = 0.5f;

                // Simple language button cycling PL→EN→EL
                GameObject btnGo = new GameObject("LanguageButton", typeof(Image), typeof(Button));
                btnGo.transform.SetParent(runtimeLangCanvas.transform, false);
                RectTransform btnRect = btnGo.GetComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(1f, 1f);
                btnRect.anchorMax = new Vector2(1f, 1f);
                btnRect.pivot = new Vector2(1f, 1f);
                btnRect.sizeDelta = new Vector2(130f, 55f);
                btnRect.anchoredPosition = new Vector2(-10f, -10f);

                Image btnImg = btnGo.GetComponent<Image>();
                btnImg.color = new Color(0.12f, 0.13f, 0.18f, 1f);
                btnImg.raycastTarget = true;

                GameObject txtGo = new GameObject("Text", typeof(Text));
                txtGo.transform.SetParent(btnGo.transform, false);
                RectTransform txtRect = txtGo.GetComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.sizeDelta = Vector2.zero;

                Text txt = txtGo.GetComponent<Text>();
                txt.text = ARHerb.Localization.LocalizationManager.CurrentLanguage.ToUpper();
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 18;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
                txt.raycastTarget = false;

                string[] langs = { "pl", "en", "el" };
                int idx = System.Array.IndexOf(langs, ARHerb.Localization.LocalizationManager.CurrentLanguage);
                Button btn = btnGo.GetComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    idx = (idx + 1) % langs.Length;
                    ARHerb.Localization.LocalizationManager.CurrentLanguage = langs[idx];
                    PlayerPrefs.SetString("SavedLanguage", langs[idx]);
                    PlayerPrefs.SetInt("SavedLanguageIndex", idx);
                    txt.text = langs[idx].ToUpper();
                    ApplyLanguageTranslations();
                });

                Debug.Log("[Language] visible on Android = true (runtime fallback)");
            }
        }

        private void EnsureScanningFrameSetup()
        {
            if (scanningFrame == null)
            {
                scanningFrame = GameObject.Find("ScanningFrame");
            }

            if (scanningFrame == null)
            {
                Debug.Log("[ScanningFrame] Creating runtime ScanningFrame overlay");

                GameObject parentGo = GameObject.Find("SafeArea");
                if (parentGo == null) parentGo = GameObject.Find("Canvas");

                if (parentGo != null)
                {
                    scanningFrame = new GameObject("ScanningFrame", typeof(RectTransform));
                    scanningFrame.transform.SetParent(parentGo.transform, false);
                    RectTransform frameRect = scanningFrame.GetComponent<RectTransform>();
                    frameRect.anchorMin = new Vector2(0.5f, 0.5f);
                    frameRect.anchorMax = new Vector2(0.5f, 0.5f);
                    frameRect.pivot = new Vector2(0.5f, 0.5f);
                    frameRect.sizeDelta = new Vector2(700f, 700f);
                    frameRect.anchoredPosition = new Vector2(0f, 60f);

                    CreateCornerBarRuntime(scanningFrame.transform, "TL_H", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(80f, 10f));
                    CreateCornerBarRuntime(scanningFrame.transform, "TL_V", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, 80f));

                    CreateCornerBarRuntime(scanningFrame.transform, "TR_H", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(80f, 10f));
                    CreateCornerBarRuntime(scanningFrame.transform, "TR_V", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(10f, 80f));

                    CreateCornerBarRuntime(scanningFrame.transform, "BL_H", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(80f, 10f));
                    CreateCornerBarRuntime(scanningFrame.transform, "BL_V", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 80f));

                    CreateCornerBarRuntime(scanningFrame.transform, "BR_H", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(80f, 10f));
                    CreateCornerBarRuntime(scanningFrame.transform, "BR_V", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(10f, 80f));
                }
            }
        }

        private void CreateCornerBarRuntime(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            GameObject barGo = new GameObject(name, typeof(Image));
            barGo.transform.SetParent(parent, false);
            RectTransform rect = barGo.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            Image img = barGo.GetComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
        }

        private void SetScanningFrameVisible(bool visible)
        {
            EnsureScanningFrameSetup();
            if (scanningFrame != null)
            {
                scanningFrame.SetActive(true); // Scanning frame stays constantly visible as requested
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Button Icons
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads transparent PNG sprites from Assets/Icons/ and attaches them
        /// as centered, full-size child Image GameObjects inside each UI button.
        /// Icon names correspond to the file names in Assets/Icons/.
        /// </summary>
        private void AttachButtonIcons()
        {
            // Map: button reference → icon path (relative to Resources, or absolute via AssetDatabase in editor)
            // Since we can't use Resources.Load for non-Resources assets at runtime we load via Sprite reference
            // We use the approach of loading sprites by path at edit-time in the Editor, but at runtime we use
            // a helper that tries AssetDatabase first (editor), then falls back to a Resources folder.

            var buttonIconMap = new System.Collections.Generic.Dictionary<Button, string>
            {
                { scanButton,         "scan"         },  // scan.png
                { galleryButton,      "gallery"      },  // gallery.png
                { flashlightButton,   "blyskawica"   },  // blyskawica.png (lightning / flashlight)
                { switchCameraButton, "kamera"       },  // kamera.png
                { historyButton,      "time"         },  // time.png (history / clock)
            };

            if (cameraSelectButton != null) buttonIconMap[cameraSelectButton] = "chose_camera";
            Button camDropdownBtn = FindButtonByName("CameraDropdown");
            if (camDropdownBtn != null) buttonIconMap[camDropdownBtn] = "chose_camera";

            // Also try to find a LanguageButton / LanguageDropdown / SettingsButton by name in the hierarchy
            Button langBtn = FindButtonByName("LanguageButton");
            if (langBtn == null) langBtn = FindButtonByName("LanguageDropdown");
            if (langBtn != null) buttonIconMap[langBtn] = "jezyk";

            Button settingsBtn = FindButtonByName("SettingsButton");
            if (settingsBtn != null) buttonIconMap[settingsBtn] = "settings2";

            foreach (var kvp in buttonIconMap)
            {
                Button btn = kvp.Key;
                string iconName = kvp.Value;
                if (btn == null) continue;

                Sprite sprite = LoadIconSprite(iconName);
                if (sprite == null)
                {
                    Debug.LogWarning($"[UIManager] Icon sprite not found: Icons/{iconName}");
                    continue;
                }

                AttachIconToButton(btn, sprite);
            }
        }

        private GameObject FindGameObjectInScene(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) return go;

            foreach (Canvas c in FindObjectsOfType<Canvas>(true))
            {
                foreach (Transform t in c.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name) return t.gameObject;
                }
            }

            foreach (GameObject rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform t in rootGo.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name) return t.gameObject;
                }
            }

            return null;
        }

        private Button FindButtonByName(string name)
        {
            GameObject go = FindGameObjectInScene(name);
            return go != null ? go.GetComponent<Button>() : null;
        }

        private Sprite LoadIconSprite(string iconName)
        {
#if UNITY_EDITOR
            string path = $"Assets/Icons/{iconName}.png";
            Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;
#endif
            // Runtime fallback: place PNGs in Assets/Resources/Icons/ for Resources.Load
            return Resources.Load<Sprite>($"Icons/{iconName}");
        }

        private void AttachIconToButton(Button btn, Sprite sprite)
        {
            if (btn == null || sprite == null) return;

            // If an Icon child already exists, just update its sprite
            Transform existing = btn.transform.Find("Icon");
            if (existing != null)
            {
                Image img = existing.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.preserveAspect = true;
                }
                return;
            }

            // Create a new centered Icon child
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(btn.transform, false);

            RectTransform rt = iconGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            Image image = iconGo.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false; // Don't block button clicks

            // Move icon behind any existing Text children
            iconGo.transform.SetAsFirstSibling();

            Debug.Log($"[UIManager] Icon '{sprite.name}' attached to button '{btn.name}'");
        }

        private void EnsureZoomButtonSetup()
        {
            // Destroy legacy single toggle ZoomButton if it exists
            GameObject oldSingleZoomBtn = GameObject.Find("ZoomButton");
            if (oldSingleZoomBtn != null)
            {
                Destroy(oldSingleZoomBtn);
            }

            if (zoom1xButton == null)
            {
                GameObject foundBtnGo = GameObject.Find("Zoom1xButton");
                if (foundBtnGo != null) zoom1xButton = foundBtnGo.GetComponent<Button>();
            }
            if (zoom2xButton == null)
            {
                GameObject foundBtnGo = GameObject.Find("Zoom2xButton");
                if (foundBtnGo != null) zoom2xButton = foundBtnGo.GetComponent<Button>();
            }

            if (zoom1xButton == null || zoom2xButton == null)
            {
                Debug.Log("[Zoom] Creating runtime side-by-side ZoomButtons overlay");

                GameObject zoomCanvasGo = GameObject.Find("ZoomOverlayCanvas");
                if (zoomCanvasGo == null)
                {
                    zoomCanvasGo = new GameObject("ZoomOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    Canvas c = zoomCanvasGo.GetComponent<Canvas>();
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    c.sortingOrder = 900;
                    c.overrideSorting = true;

                    CanvasScaler cs = zoomCanvasGo.GetComponent<CanvasScaler>();
                    cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    cs.referenceResolution = new Vector2(1080, 2340);
                    cs.matchWidthOrHeight = 0.5f;
                }

                if (zoom1xButton == null)
                {
                    GameObject btnGo = new GameObject("Zoom1xButton", typeof(Image), typeof(Button));
                    btnGo.transform.SetParent(zoomCanvasGo.transform, false);
                    RectTransform rect = btnGo.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0f);
                    rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.sizeDelta = new Vector2(90f, 90f);
                    rect.anchoredPosition = new Vector2(-55f, 360f);

                    Image img = btnGo.GetComponent<Image>();
                    img.color = new Color(0.18f, 0.8f, 0.44f, 1f);
                    img.raycastTarget = true;

                    GameObject txtGo = new GameObject("Text", typeof(Text));
                    txtGo.transform.SetParent(btnGo.transform, false);
                    RectTransform txtRect = txtGo.GetComponent<RectTransform>();
                    txtRect.anchorMin = Vector2.zero;
                    txtRect.anchorMax = Vector2.one;
                    txtRect.sizeDelta = Vector2.zero;

                    Text txt = txtGo.GetComponent<Text>();
                    txt.text = "1x";
                    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    txt.fontSize = 28;
                    txt.fontStyle = FontStyle.Bold;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = Color.white;
                    txt.raycastTarget = false;

                    zoom1xButton = btnGo.GetComponent<Button>();
                }

                if (zoom2xButton == null)
                {
                    GameObject btnGo = new GameObject("Zoom2xButton", typeof(Image), typeof(Button));
                    btnGo.transform.SetParent(zoomCanvasGo.transform, false);
                    RectTransform rect = btnGo.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0f);
                    rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.sizeDelta = new Vector2(90f, 90f);
                    rect.anchoredPosition = new Vector2(55f, 360f);

                    Image img = btnGo.GetComponent<Image>();
                    img.color = new Color(0.1f, 0.12f, 0.16f, 0.85f);
                    img.raycastTarget = true;

                    GameObject txtGo = new GameObject("Text", typeof(Text));
                    txtGo.transform.SetParent(btnGo.transform, false);
                    RectTransform txtRect = txtGo.GetComponent<RectTransform>();
                    txtRect.anchorMin = Vector2.zero;
                    txtRect.anchorMax = Vector2.one;
                    txtRect.sizeDelta = Vector2.zero;

                    Text txt = txtGo.GetComponent<Text>();
                    txt.text = "2x";
                    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    txt.fontSize = 28;
                    txt.fontStyle = FontStyle.Bold;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = new Color(0.7f, 0.7f, 0.7f);
                    txt.raycastTarget = false;

                    zoom2xButton = btnGo.GetComponent<Button>();
                }
            }

            if (zoom1xButton != null)
            {
                zoom1xButton.onClick.RemoveAllListeners();
                zoom1xButton.onClick.AddListener(() => OnZoomLevelSelected(1.0f));
            }
            if (zoom2xButton != null)
            {
                zoom2xButton.onClick.RemoveAllListeners();
                zoom2xButton.onClick.AddListener(() => OnZoomLevelSelected(2.0f));
            }

            UpdateZoomButtonUI();
        }

        private void OnZoomLevelSelected(float targetZoom)
        {
            if (activeCaptureProvider == null) return;

            activeCaptureProvider.SetZoom(targetZoom);
            UpdateZoomButtonUI();
        }

        private Image cachedZoom1xImg;
        private Text cachedZoom1xTxt;
        private Image cachedZoom2xImg;
        private Text cachedZoom2xTxt;
        private string lastZoom1xStr = "";
        private string lastZoom2xStr = "";

        private void UpdateZoomButtonUI()
        {
            float currentZoom = (activeCaptureProvider != null) ? activeCaptureProvider.GetZoom() : 1.0f;

            if (zoom1xButton != null)
            {
                if (cachedZoom1xImg == null) cachedZoom1xImg = zoom1xButton.GetComponent<Image>();
                if (cachedZoom1xTxt == null) cachedZoom1xTxt = zoom1xButton.GetComponentInChildren<Text>();

                bool is1xActive = (currentZoom <= 1.2f);
                string newStr = is1xActive ? $"{currentZoom:F1}x" : "1.0x";

                if (newStr != lastZoom1xStr)
                {
                    lastZoom1xStr = newStr;
                    if (cachedZoom1xImg != null) cachedZoom1xImg.color = is1xActive ? new Color(0.18f, 0.8f, 0.44f, 1f) : new Color(0.1f, 0.12f, 0.16f, 0.85f);
                    if (cachedZoom1xTxt != null)
                    {
                        cachedZoom1xTxt.text = newStr;
                        cachedZoom1xTxt.color = is1xActive ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
                    }
                }
            }

            if (zoom2xButton != null)
            {
                if (cachedZoom2xImg == null) cachedZoom2xImg = zoom2xButton.GetComponent<Image>();
                if (cachedZoom2xTxt == null) cachedZoom2xTxt = zoom2xButton.GetComponentInChildren<Text>();

                bool is2xActive = (currentZoom > 1.2f);
                string newStr = is2xActive ? $"{currentZoom:F1}x" : "2.0x";

                if (newStr != lastZoom2xStr)
                {
                    lastZoom2xStr = newStr;
                    if (cachedZoom2xImg != null) cachedZoom2xImg.color = is2xActive ? new Color(0.18f, 0.8f, 0.44f, 1f) : new Color(0.1f, 0.12f, 0.16f, 0.85f);
                    if (cachedZoom2xTxt != null)
                    {
                        cachedZoom2xTxt.text = newStr;
                        cachedZoom2xTxt.color = is2xActive ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
                    }
                }
            }
        }

        [SerializeField] private Button cameraSelectButton;

        private void EnsureCameraControlBarSetup()
        {
            GameObject cameraBarGo = GameObject.Find("CameraControlBar");
            if (cameraBarGo == null)
            {
                Debug.Log("[CameraUI] Creating runtime CameraControlBar");
                GameObject safeAreaGo = GameObject.Find("SafeArea");
                if (safeAreaGo == null) safeAreaGo = GameObject.Find("Canvas");

                if (safeAreaGo != null)
                {
                    cameraBarGo = new GameObject("CameraControlBar", typeof(Image));
                    cameraBarGo.transform.SetParent(safeAreaGo.transform, false);
                    RectTransform barRect = cameraBarGo.GetComponent<RectTransform>();
                    barRect.anchorMin = new Vector2(0.03f, 0.86f);
                    barRect.anchorMax = new Vector2(0.97f, 0.92f);
                    barRect.sizeDelta = Vector2.zero;

                    Image barImg = cameraBarGo.GetComponent<Image>();
                    barImg.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
                    barImg.raycastTarget = false;
                }
            }

            if (cameraBarGo != null)
            {
                // Flashlight Button
                if (flashlightButton == null)
                {
                    Transform foundFlash = cameraBarGo.transform.Find("FlashlightButton");
                    if (foundFlash != null) flashlightButton = foundFlash.GetComponent<Button>();
                }
                if (flashlightButton == null)
                {
                    GameObject btnGo = new GameObject("FlashlightButton", typeof(Image), typeof(Button));
                    btnGo.transform.SetParent(cameraBarGo.transform, false);
                    RectTransform rect = btnGo.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.01f, 0.08f);
                    rect.anchorMax = new Vector2(0.24f, 0.92f);
                    rect.sizeDelta = Vector2.zero;

                    Image img = btnGo.GetComponent<Image>();
                    img.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
                    img.raycastTarget = true;

                    GameObject txtGo = new GameObject("Text", typeof(Text));
                    txtGo.transform.SetParent(btnGo.transform, false);
                    RectTransform txtRect = txtGo.GetComponent<RectTransform>();
                    txtRect.anchorMin = Vector2.zero;
                    txtRect.anchorMax = Vector2.one;
                    txtRect.sizeDelta = Vector2.zero;

                    Text txt = txtGo.GetComponent<Text>();
                    txt.text = "⚡ OFF";
                    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    txt.fontSize = 18;
                    txt.fontStyle = FontStyle.Bold;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = Color.white;
                    txt.raycastTarget = false;

                    flashlightButton = btnGo.GetComponent<Button>();
                }

                // Camera Select Button (Center - opens modal picker)
                Transform oldDropdown = cameraBarGo.transform.Find("CameraDropdown");
                if (oldDropdown != null) oldDropdown.gameObject.SetActive(false);

                if (cameraSelectButton == null)
                {
                    Transform foundSelect = cameraBarGo.transform.Find("CameraSelectButton");
                    if (foundSelect != null) cameraSelectButton = foundSelect.GetComponent<Button>();
                }
                if (cameraSelectButton == null)
                {
                    GameObject btnGo = new GameObject("CameraSelectButton", typeof(Image), typeof(Button));
                    btnGo.transform.SetParent(cameraBarGo.transform, false);
                    RectTransform rect = btnGo.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.26f, 0.08f);
                    rect.anchorMax = new Vector2(0.80f, 0.92f);
                    rect.sizeDelta = Vector2.zero;

                    Image img = btnGo.GetComponent<Image>();
                    img.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
                    img.raycastTarget = true;

                    GameObject txtGo = new GameObject("Text", typeof(Text));
                    txtGo.transform.SetParent(btnGo.transform, false);
                    RectTransform txtRect = txtGo.GetComponent<RectTransform>();
                    txtRect.anchorMin = Vector2.zero;
                    txtRect.anchorMax = Vector2.one;
                    txtRect.sizeDelta = Vector2.zero;

                    Text txt = txtGo.GetComponent<Text>();
                    txt.text = "📷 Wybierz Kamere ▾";
                    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    txt.fontSize = 16;
                    txt.fontStyle = FontStyle.Bold;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = Color.white;
                    txt.raycastTarget = false;

                    cameraSelectButton = btnGo.GetComponent<Button>();
                }

                // SwitchCameraButton
                if (switchCameraButton == null)
                {
                    Transform foundSwitch = cameraBarGo.transform.Find("SwitchCameraButton");
                    if (foundSwitch != null) switchCameraButton = foundSwitch.GetComponent<Button>();
                }
                if (switchCameraButton == null)
                {
                    GameObject btnGo = new GameObject("SwitchCameraButton", typeof(Image), typeof(Button));
                    btnGo.transform.SetParent(cameraBarGo.transform, false);
                    RectTransform rect = btnGo.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.82f, 0.08f);
                    rect.anchorMax = new Vector2(0.99f, 0.92f);
                    rect.sizeDelta = Vector2.zero;

                    Image img = btnGo.GetComponent<Image>();
                    img.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
                    img.raycastTarget = true;

                    GameObject txtGo = new GameObject("Text", typeof(Text));
                    txtGo.transform.SetParent(btnGo.transform, false);
                    RectTransform txtRect = txtGo.GetComponent<RectTransform>();
                    txtRect.anchorMin = Vector2.zero;
                    txtRect.anchorMax = Vector2.one;
                    txtRect.sizeDelta = Vector2.zero;

                    Text txt = txtGo.GetComponent<Text>();
                    txt.text = "🔄";
                    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    txt.fontSize = 22;
                    txt.fontStyle = FontStyle.Bold;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = Color.white;
                    txt.raycastTarget = false;

                    switchCameraButton = btnGo.GetComponent<Button>();
                }
            }

            // Bind listeners
            if (flashlightButton != null)
            {
                flashlightButton.onClick.RemoveAllListeners();
                flashlightButton.onClick.AddListener(ToggleFlashlight);
                UpdateFlashlightButtonUI();
            }
            if (switchCameraButton != null)
            {
                switchCameraButton.onClick.RemoveAllListeners();
                switchCameraButton.onClick.AddListener(OnSwitchCameraButtonClicked);
                foreach (Transform child in switchCameraButton.transform)
                {
                    if (child.name == "Text" || child.name == "Label")
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }
            if (cameraSelectButton != null)
            {
                cameraSelectButton.onClick.RemoveAllListeners();
                cameraSelectButton.onClick.AddListener(ShowCameraPickerModal);
            }

            // Hide any child text or label overlay on the camera select button
            if (cameraSelectButton != null)
            {
                foreach (Transform child in cameraSelectButton.transform)
                {
                    if (child.name == "Text" || child.name == "Label")
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            UpdateCameraSelectButtonLabel();
        }

        private void UpdateCameraSelectButtonLabel()
        {
            if (cameraSelectButton == null) return;

            // Keep text overlay on round icon button hidden
            Text txt = cameraSelectButton.GetComponentInChildren<Text>(true);
            if (txt != null)
            {
                txt.gameObject.SetActive(false);
            }
            foreach (Transform child in cameraSelectButton.transform)
            {
                if (child.name == "Text" || child.name == "Label")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void ShowCameraPickerModal()
        {
            if (activeCaptureProvider == null) return;
            string[] devices = activeCaptureProvider.GetAvailableCameraDevices();
            if (devices == null || devices.Length == 0) return;

            GameObject existingOverlay = GameObject.Find("RuntimeCameraOverlayCanvas");
            if (existingOverlay != null) Destroy(existingOverlay);

            GameObject overlayCanvasGo = new GameObject("RuntimeCameraOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas overlayCanvas = overlayCanvasGo.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 32767;
            overlayCanvas.overrideSorting = true;

            CanvasScaler scaler = overlayCanvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            scaler.matchWidthOrHeight = 0.5f;

            // Semi-transparent dark backdrop
            GameObject backdropGo = new GameObject("Backdrop", typeof(Image), typeof(Button));
            backdropGo.transform.SetParent(overlayCanvasGo.transform, false);
            RectTransform bdRect = backdropGo.GetComponent<RectTransform>();
            bdRect.anchorMin = Vector2.zero;
            bdRect.anchorMax = Vector2.one;
            bdRect.sizeDelta = Vector2.zero;

            Image bdImg = backdropGo.GetComponent<Image>();
            bdImg.color = new Color(0f, 0f, 0f, 0.70f);
            bdImg.raycastTarget = true;

            Button bdBtn = backdropGo.GetComponent<Button>();
            bdBtn.onClick.AddListener(() => Destroy(overlayCanvasGo));

            // Modal dialog container
            GameObject dialogGo = new GameObject("Dialog", typeof(Image));
            dialogGo.transform.SetParent(overlayCanvasGo.transform, false);
            RectTransform dialogRect = dialogGo.GetComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.08f, 0.22f);
            dialogRect.anchorMax = new Vector2(0.92f, 0.78f);
            dialogRect.sizeDelta = Vector2.zero;

            Image dialogImg = dialogGo.GetComponent<Image>();
            dialogImg.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);
            dialogImg.raycastTarget = true;

            // Title
            GameObject titleGo = new GameObject("TitleText", typeof(Text));
            titleGo.transform.SetParent(dialogGo.transform, false);
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.04f, 0.88f);
            titleRect.anchorMax = new Vector2(0.96f, 0.98f);
            titleRect.sizeDelta = Vector2.zero;

            Text titleTxt = titleGo.GetComponent<Text>();
            titleTxt.text = "📷 " + ARHerb.Localization.LocalizationManager.Get("camera_select_title");
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.fontSize = 28;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.2f, 0.82f, 0.48f, 1f);

            int currentIdx = activeCaptureProvider.GetCurrentCameraDeviceIndex();
            float availableHeight = 0.72f;
            float slotHeightRatio = availableHeight / Mathf.Max(devices.Length, 1);

            for (int i = 0; i < devices.Length; i++)
            {
                int camIdx = i;
                GameObject itemBtnGo = new GameObject($"CamBtn_{i}", typeof(Image), typeof(Button));
                itemBtnGo.transform.SetParent(dialogGo.transform, false);
                RectTransform itemRect = itemBtnGo.GetComponent<RectTransform>();

                float yTop = 0.86f - (i * slotHeightRatio);
                float yBottom = yTop - (slotHeightRatio * 0.88f);
                itemRect.anchorMin = new Vector2(0.04f, Mathf.Max(yBottom, 0.14f));
                itemRect.anchorMax = new Vector2(0.96f, Mathf.Min(yTop, 0.86f));
                itemRect.sizeDelta = Vector2.zero;

                Image itemImg = itemBtnGo.GetComponent<Image>();
                bool isSelected = (i == currentIdx);
                itemImg.color = isSelected ? new Color(0.18f, 0.55f, 0.30f, 0.98f) : new Color(0.15f, 0.18f, 0.24f, 0.95f);
                itemImg.raycastTarget = true;

                GameObject itemTxtGo = new GameObject("Text", typeof(Text));
                itemTxtGo.transform.SetParent(itemBtnGo.transform, false);
                RectTransform itemTxtRect = itemTxtGo.GetComponent<RectTransform>();
                itemTxtRect.anchorMin = Vector2.zero;
                itemTxtRect.anchorMax = Vector2.one;
                itemTxtRect.sizeDelta = Vector2.zero;

                Text itemTxt = itemTxtGo.GetComponent<Text>();
                itemTxt.text = devices[i] + (isSelected ? "  ✓" : "");
                itemTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                itemTxt.fontSize = 20;
                itemTxt.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
                itemTxt.alignment = TextAnchor.MiddleCenter;
                itemTxt.color = Color.white;
                itemTxt.raycastTarget = false;

                Button itemBtn = itemBtnGo.GetComponent<Button>();
                itemBtn.onClick.AddListener(() =>
                {
                    Debug.Log($"[CameraPickerModal] Selected camera index {camIdx}: {devices[camIdx]}");
                    activeCaptureProvider.SelectCameraDevice(camIdx);
                    UpdateCameraSelectButtonLabel();
                    UpdateZoomButtonUI();
                    Destroy(overlayCanvasGo);
                });
            }

            // Visible note message required for Android WebCamTexture limitations
            GameObject noteGo = new GameObject("NoteText", typeof(Text));
            noteGo.transform.SetParent(dialogGo.transform, false);
            RectTransform noteRect = noteGo.GetComponent<RectTransform>();
            noteRect.anchorMin = new Vector2(0.03f, 0.01f);
            noteRect.anchorMax = new Vector2(0.97f, 0.13f);
            noteRect.sizeDelta = Vector2.zero;

            Text noteTxt = noteGo.GetComponent<Text>();
            noteTxt.text = "Some Android devices do not expose every physical lens through Unity WebCamTexture.";
            noteTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            noteTxt.fontSize = 15;
            noteTxt.fontStyle = FontStyle.Italic;
            noteTxt.alignment = TextAnchor.MiddleCenter;
            noteTxt.color = new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        private void OnSwitchCameraButtonClicked()
        {
            if (activeCaptureProvider != null)
            {
                Debug.Log("[Camera] Switching camera (rear <-> front)...");
                activeCaptureProvider.SwitchCamera();
                UpdateZoomButtonUI();
                UpdateCameraSelectButtonLabel();
            }
        }

        private void OnCameraDropdownChanged(int selectedIndex)
        {
            if (activeCaptureProvider == null) return;
            Debug.Log($"[CameraDropdown] User selected camera index {selectedIndex}");
            activeCaptureProvider.SelectCameraDevice(selectedIndex);
            UpdateZoomButtonUI();
        }

        private bool isFlashlightOn = false;

        private void EnsureFlashlightButtonSetup()
        {
            if (flashlightButton == null)
            {
                GameObject foundBtnGo = GameObject.Find("FlashlightButton");
                if (foundBtnGo != null) flashlightButton = foundBtnGo.GetComponent<Button>();
            }

            if (flashlightButton == null)
            {
                Debug.Log("[Flashlight] Creating runtime FlashlightButton in TopBar");

                GameObject topBarGo = GameObject.Find("TopBar");
                if (topBarGo == null) topBarGo = GameObject.Find("SafeArea");
                if (topBarGo == null) topBarGo = GameObject.Find("Canvas");

                if (topBarGo != null)
                {
                    GameObject btnGo = new GameObject("FlashlightButton", typeof(Image), typeof(Button));
                    btnGo.transform.SetParent(topBarGo.transform, false);
                    RectTransform rect = btnGo.GetComponent<RectTransform>();

                    if (topBarGo.name == "TopBar")
                    {
                        rect.anchorMin = new Vector2(1f, 0.1f);
                        rect.anchorMax = new Vector2(1f, 0.1f);
                        rect.pivot = new Vector2(1f, 0.5f);
                        rect.sizeDelta = new Vector2(110f, 64f);
                        rect.anchoredPosition = new Vector2(-96f, 0f);
                    }
                    else
                    {
                        rect.anchorMin = new Vector2(0.55f, 0.94f);
                        rect.anchorMax = new Vector2(0.72f, 0.98f);
                        rect.sizeDelta = Vector2.zero;
                    }

                    Image img = btnGo.GetComponent<Image>();
                    img.color = new Color(0.1f, 0.12f, 0.16f, 0.85f);
                    img.raycastTarget = true;

                    GameObject txtGo = new GameObject("Text", typeof(Text));
                    txtGo.transform.SetParent(btnGo.transform, false);
                    RectTransform txtRect = txtGo.GetComponent<RectTransform>();
                    txtRect.anchorMin = Vector2.zero;
                    txtRect.anchorMax = Vector2.one;
                    txtRect.sizeDelta = Vector2.zero;

                    Text txt = txtGo.GetComponent<Text>();
                    txt.text = "⚡ OFF";
                    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    txt.fontSize = 20;
                    txt.fontStyle = FontStyle.Bold;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = Color.white;
                    txt.raycastTarget = false;

                    flashlightButton = btnGo.GetComponent<Button>();
                }
            }

            if (flashlightButton != null)
            {
                flashlightButton.onClick.RemoveAllListeners();
                flashlightButton.onClick.AddListener(ToggleFlashlight);
                UpdateFlashlightButtonUI();
            }
        }

        private void ToggleFlashlight()
        {
            isFlashlightOn = !isFlashlightOn;
            Debug.Log($"[Flashlight] Toggle flashlight = {isFlashlightOn}");

            SetFlashlightState(isFlashlightOn);
            UpdateFlashlightButtonUI();
        }

        private void SetFlashlightState(bool enabled)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            bool success = false;

            // Strategy 1: CameraManager.setTorchMode (Camera2 API)
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject context = currentActivity.Call<AndroidJavaObject>("getApplicationContext"))
                using (AndroidJavaObject cameraManager = context.Call<AndroidJavaObject>("getSystemService", "camera"))
                {
                    string[] cameraIdList = cameraManager.Call<string[]>("getCameraIdList");
                    if (cameraIdList != null && cameraIdList.Length > 0)
                    {
                        foreach (string id in cameraIdList)
                        {
                            try
                            {
                                using (AndroidJavaObject characteristics = cameraManager.Call<AndroidJavaObject>("getCameraCharacteristics", id))
                                {
                                    using (AndroidJavaClass charClass = new AndroidJavaClass("android.hardware.camera2.CameraCharacteristics"))
                                    using (AndroidJavaObject flashKey = charClass.GetStatic<AndroidJavaObject>("FLASH_INFO_AVAILABLE"))
                                    using (AndroidJavaObject hasFlashObj = characteristics.Call<AndroidJavaObject>("get", flashKey))
                                    {
                                        if (hasFlashObj != null && hasFlashObj.Call<bool>("booleanValue"))
                                        {
                                            cameraManager.Call("setTorchMode", id, enabled);
                                            Debug.Log($"[Flashlight] setTorchMode succeeded on camera id '{id}' (enabled={enabled})");
                                            success = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            catch (System.Exception innerEx)
                            {
                                Debug.LogWarning($"[Flashlight] setTorchMode check failed on camera id '{id}': {innerEx.Message}");
                            }
                        }

                        // Fallback: If FLASH_INFO_AVAILABLE key check failed or returned false, try setting torch mode on camera ID "0" or "1" directly
                        if (!success)
                        {
                            for (int i = 0; i < cameraIdList.Length; i++)
                            {
                                try
                                {
                                    cameraManager.Call("setTorchMode", cameraIdList[i], enabled);
                                    Debug.Log($"[Flashlight] Direct setTorchMode succeeded on camera id '{cameraIdList[i]}'");
                                    success = true;
                                    break;
                                }
                                catch {}
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Flashlight] CameraManager strategy failed: {ex.Message}");
            }

            // Strategy 2: Legacy Camera API fallback if Camera2 strategy failed
            if (!success)
            {
                try
                {
                    using (AndroidJavaClass cameraClass = new AndroidJavaClass("android.hardware.Camera"))
                    {
                        int numCameras = cameraClass.CallStatic<int>("getNumberOfCameras");
                        for (int i = 0; i < numCameras; i++)
                        {
                            try
                            {
                                using (AndroidJavaObject cam = cameraClass.CallStatic<AndroidJavaObject>("open", i))
                                {
                                    if (cam != null)
                                    {
                                        using (AndroidJavaObject paramsObj = cam.Call<AndroidJavaObject>("getParameters"))
                                        {
                                            string mode = enabled ? "torch" : "off";
                                            paramsObj.Call("setFlashMode", mode);
                                            cam.Call("setParameters", paramsObj);
                                            if (enabled)
                                            {
                                                cam.Call("startPreview");
                                            }
                                            else
                                            {
                                                cam.Call("stopPreview");
                                                cam.Call("release");
                                            }
                                            Debug.Log($"[Flashlight] Legacy Camera API succeeded on camera index {i}");
                                            success = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            catch {}
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Flashlight] Legacy Camera strategy failed: {ex.Message}");
                }
            }
#else
            Debug.Log($"[Flashlight] Editor/Mock mode torch = {enabled}");
#endif
        }

        private void UpdateFlashlightButtonUI()
        {
            if (flashlightButton != null)
            {
                Image img = flashlightButton.GetComponent<Image>();
                Text txt = flashlightButton.GetComponentInChildren<Text>();

                if (img != null)
                {
                    img.color = isFlashlightOn 
                        ? new Color(0.95f, 0.75f, 0.1f, 1f)  // Bright yellow when ON
                        : new Color(0.1f, 0.12f, 0.16f, 0.85f); // Dark translucent when OFF
                }
                if (txt != null)
                {
                    txt.text = isFlashlightOn ? "⚡ ON" : "⚡ OFF";
                    txt.color = isFlashlightOn ? Color.black : Color.white;
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
