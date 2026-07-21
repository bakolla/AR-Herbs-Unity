using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;
using ARHerb.UI;
using ARHerb.Network;

public class SetupUI
{
    [MenuItem("AR Herb/Build MVP Scene")]
    public static void BuildMVPScene()
    {
        // 1. Ensure Scenes directory exists
        if (!Directory.Exists("Assets/Scenes"))
        {
            Directory.CreateDirectory("Assets/Scenes");
        }

        string scenePath = "Assets/Scenes/MainARScene.unity";
        UnityEngine.SceneManagement.Scene scene;

        // Open or create the scene
        if (File.Exists(scenePath))
        {
            scene = EditorSceneManager.OpenScene(scenePath);
        }
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        // Remove default Main Camera
        GameObject defaultCam = GameObject.Find("Main Camera");
        if (defaultCam != null)
        {
            Undo.DestroyObjectImmediate(defaultCam);
        }

        // Clean up existing duplicates
        string[] oldNames = { "Canvas", "AppManager", "EventSystem", "AR Session", "XR Origin", "AR Session Origin", "ARMobileRoot", "Main Camera" };
        foreach (var name in oldNames)
        {
            GameObject oldGo = GameObject.Find(name);
            if (oldGo != null) Undo.DestroyObjectImmediate(oldGo);
        }

        // 2. Create AR Foundation Hierarchy safely using reflection
        GameObject arMobileRoot = CreateARFoundationObjects();

        // 3. Create Canvas and UI elements
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 2340);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");

        // EventSystem
        GameObject eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");

        // UI Camera Preview (parented to Canvas directly to stay fullscreen background)
        GameObject previewGo = new GameObject("CameraPreviewUI", typeof(RawImage));
        previewGo.transform.SetParent(canvasGo.transform, false);
        RawImage preview = previewGo.GetComponent<RawImage>();
        RectTransform previewRect = preview.rectTransform;
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.sizeDelta = Vector2.zero;
        preview.color = Color.white;

        // SafeArea Container (parented to Canvas, holds all other UI elements)
        GameObject safeAreaGo = new GameObject("SafeArea", typeof(RectTransform), typeof(ARHerb.UI.SafeAreaHandler));
        safeAreaGo.transform.SetParent(canvasGo.transform, false);
        RectTransform safeAreaRect = safeAreaGo.GetComponent<RectTransform>();
        safeAreaRect.anchorMin = Vector2.zero;
        safeAreaRect.anchorMax = Vector2.one;
        safeAreaRect.sizeDelta = Vector2.zero;
        Undo.RegisterCreatedObjectUndo(safeAreaGo, "Create SafeArea");

        // Top Header
        GameObject topBarGo = CreatePanel(safeAreaGo.transform, "TopBar", new Vector2(0f, 0.92f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Color(0.04f, 0.05f, 0.06f, 0.85f));
        
        GameObject titleGo = CreateText(topBarGo.transform, "TitleText", "🌿 HERB & FAUNA", 18, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.02f, 0.5f);
        titleRect.anchorMax = new Vector2(0.46f, 1f);
        titleRect.sizeDelta = Vector2.zero;

        // UI Resources
        DefaultControls.Resources uiResources = new DefaultControls.Resources();
        uiResources.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        uiResources.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
        uiResources.inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
        uiResources.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        uiResources.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        uiResources.dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        uiResources.mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");

        // Language Overlay Canvas (sortingOrder=1000, always above camera preview)
        GameObject langOverlayCanvasGo = new GameObject("LanguageOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas langCanvas = langOverlayCanvasGo.GetComponent<Canvas>();
        langCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        langCanvas.sortingOrder = 1000;
        langCanvas.overrideSorting = true;

        CanvasScaler langCanvasScaler = langOverlayCanvasGo.GetComponent<CanvasScaler>();
        langCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        langCanvasScaler.referenceResolution = new Vector2(1080, 2340);
        langCanvasScaler.matchWidthOrHeight = 0.5f;

        // Language Dropdown (PL / EN / EL)
        GameObject langDropdownGo = DefaultControls.CreateDropdown(uiResources);
        langDropdownGo.name = "LanguageDropdown";
        langDropdownGo.transform.SetParent(langOverlayCanvasGo.transform, false);
        RectTransform langRect = langDropdownGo.GetComponent<RectTransform>();
        langRect.anchorMin = new Vector2(0f, 0f);
        langRect.anchorMax = new Vector2(0f, 0f);
        langRect.pivot = new Vector2(1f, 1f);
        langRect.sizeDelta = new Vector2(160f, 60f);
        langRect.anchoredPosition = new Vector2(-10f, -10f);
        // anchor to top-right of screen
        langRect.anchorMin = new Vector2(1f, 1f);
        langRect.anchorMax = new Vector2(1f, 1f);

        Image langImage = langDropdownGo.GetComponent<Image>();
        if (langImage != null)
        {
            langImage.color = new Color(0.12f, 0.13f, 0.16f, 1f);
            langImage.raycastTarget = true;
        }

        Dropdown langDropdown = langDropdownGo.GetComponent<Dropdown>();
        langDropdown.interactable = true;
        if (langDropdown.captionText != null)
        {
            langDropdown.captionText.color = Color.white;
            langDropdown.captionText.fontSize = 15;
            langDropdown.captionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            langDropdown.captionText.alignment = TextAnchor.MiddleCenter;
            langDropdown.captionText.raycastTarget = false;
        }

        // Make all child texts non-raycasting
        foreach (Text t in langDropdownGo.GetComponentsInChildren<Text>(true))
        {
            t.color = Color.white;
            t.raycastTarget = false;
        }

        langDropdown.options = new List<Dropdown.OptionData>
        {
            new Dropdown.OptionData("PL"),
            new Dropdown.OptionData("EN"),
            new Dropdown.OptionData("EL")
        };

        // History Button
        GameObject historyBtnGo = new GameObject("HistoryButton", typeof(Image), typeof(Button));
        historyBtnGo.transform.SetParent(topBarGo.transform, false);
        RectTransform historyBtnRect = historyBtnGo.GetComponent<RectTransform>();
        historyBtnRect.anchorMin = new Vector2(0.70f, 0.55f);
        historyBtnRect.anchorMax = new Vector2(0.97f, 0.92f);
        historyBtnRect.sizeDelta = Vector2.zero;
        Image histImg = historyBtnGo.GetComponent<Image>();
        histImg.color = new Color(0.18f, 0.8f, 0.44f, 0.9f);
        histImg.raycastTarget = true;
        GameObject historyBtnTextGo = CreateText(historyBtnGo.transform, "Text", "📜 Historia", 12, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        historyBtnTextGo.GetComponent<Text>().raycastTarget = false;
        Button historyButton = historyBtnGo.GetComponent<Button>();
        historyButton.interactable = true;

        // URL Input (like a pill search bar below title)
        GameObject urlGo = new GameObject("BackendUrlInput", typeof(Image), typeof(InputField));
        urlGo.transform.SetParent(topBarGo.transform, false);
        RectTransform urlRect = urlGo.GetComponent<RectTransform>();
        urlRect.anchorMin = new Vector2(0.1f, 0.15f);
        urlRect.anchorMax = new Vector2(0.9f, 0.45f);
        urlRect.sizeDelta = Vector2.zero;
        urlGo.GetComponent<Image>().color = new Color(0.15f, 0.17f, 0.22f, 0.9f);

        GameObject urlTextGo = CreateText(urlGo.transform, "Text", "http://localhost:3001", 14, TextAnchor.MiddleLeft, Color.white);
        RectTransform urlTextRect = urlTextGo.GetComponent<RectTransform>();
        urlTextRect.anchorMin = Vector2.zero;
        urlTextRect.anchorMax = Vector2.one;
        urlTextRect.offsetMin = new Vector2(15f, 5f);
        urlTextRect.offsetMax = new Vector2(-15f, -5f);

        InputField urlInputField = urlGo.GetComponent<InputField>();
        urlInputField.textComponent = urlTextGo.GetComponent<Text>();
        urlInputField.text = "http://localhost:3001";

        // Dropdown (styled dark above scan button)
        GameObject dropdownGo = DefaultControls.CreateDropdown(uiResources);
        dropdownGo.name = "ModeDropdown";
        dropdownGo.transform.SetParent(safeAreaGo.transform, false);
        Undo.RegisterCreatedObjectUndo(dropdownGo, "Create ModeDropdown");

        RectTransform ddRect = dropdownGo.GetComponent<RectTransform>();
        ddRect.anchorMin = new Vector2(0.2f, 0.18f);
        ddRect.anchorMax = new Vector2(0.8f, 0.23f);
        ddRect.sizeDelta = Vector2.zero;

        Image ddImage = dropdownGo.GetComponent<Image>();
        if (ddImage != null) ddImage.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);

        Dropdown dropdown = dropdownGo.GetComponent<Dropdown>();
        if (dropdown.captionText != null)
        {
            dropdown.captionText.color = Color.white;
            dropdown.captionText.fontSize = 18;
            dropdown.captionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dropdown.captionText.alignment = TextAnchor.MiddleCenter;
        }

        Image arrowImg = dropdownGo.transform.Find("Arrow")?.GetComponent<Image>();
        if (arrowImg != null) arrowImg.color = Color.white;

        if (dropdown.template != null)
        {
            Image templateImg = dropdown.template.GetComponent<Image>();
            if (templateImg != null) templateImg.color = new Color(0.12f, 0.13f, 0.16f, 0.98f);

            Text itemText = dropdown.template.GetComponentInChildren<Text>(true);
            if (itemText != null)
            {
                itemText.color = Color.white;
                itemText.fontSize = 16;
                itemText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        dropdown.options = new List<Dropdown.OptionData>
        {
            new Dropdown.OptionData("Plants"),
            new Dropdown.OptionData("Mushrooms"),
            new Dropdown.OptionData("Insects"),
            new Dropdown.OptionData("Stones")
        };

        // Scan Button (Circular bottom shutter)
        GameObject btnGo = new GameObject("ScanButton", typeof(Image), typeof(Button));
        btnGo.transform.SetParent(safeAreaGo.transform, false);
        RectTransform btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.08f);
        btnRect.anchorMax = new Vector2(0.5f, 0.08f);
        btnRect.sizeDelta = new Vector2(100f, 100f);
        btnRect.anchoredPosition = Vector2.zero;

        // Use knob sprite for circle, fallback to color
        Image btnImg = btnGo.GetComponent<Image>();
        btnImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        btnImg.color = new Color(0.18f, 0.8f, 0.44f, 1f); // Vibrant emerald green

        // Soft outer glow highlight circle
        GameObject ringGo = new GameObject("OuterRing", typeof(Image));
        ringGo.transform.SetParent(btnGo.transform, false);
        RectTransform ringRect = ringGo.GetComponent<RectTransform>();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.sizeDelta = new Vector2(16f, 16f);
        Image ringImg = ringGo.GetComponent<Image>();
        ringImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        ringImg.color = new Color(1f, 1f, 1f, 0.25f);

        GameObject btnTextGo = CreateText(btnGo.transform, "BtnText", "SCAN", 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        RectTransform btnTextRect = btnTextGo.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;
        Button scanButton = btnGo.GetComponent<Button>();

        // Gallery Button (Pick photo from gallery)
        GameObject galleryBtnGo = new GameObject("GalleryButton", typeof(Image), typeof(Button));
        galleryBtnGo.transform.SetParent(safeAreaGo.transform, false);
        RectTransform galleryBtnRect = galleryBtnGo.GetComponent<RectTransform>();
        galleryBtnRect.anchorMin = new Vector2(0.80f, 0.08f);
        galleryBtnRect.anchorMax = new Vector2(0.80f, 0.08f);
        galleryBtnRect.sizeDelta = new Vector2(70f, 70f);
        galleryBtnRect.anchoredPosition = Vector2.zero;

        Image galleryImg = galleryBtnGo.GetComponent<Image>();
        galleryImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        galleryImg.color = new Color(0.2f, 0.55f, 0.9f, 0.95f);

        GameObject galleryTxtGo = CreateText(galleryBtnGo.transform, "BtnText", "🖼️", 24, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        RectTransform galleryTxtRect = galleryTxtGo.GetComponent<RectTransform>();
        galleryTxtRect.anchorMin = Vector2.zero;
        galleryTxtRect.anchorMax = Vector2.one;
        galleryTxtRect.sizeDelta = Vector2.zero;
        galleryTxtGo.GetComponent<Text>().raycastTarget = false;
        Button galleryButton = galleryBtnGo.GetComponent<Button>();

        // Status Panel (centered above dropdown) with subtle background and button capability
        GameObject statusPanelGo = CreatePanel(safeAreaGo.transform, "StatusPanel", new Vector2(0.1f, 0.25f), new Vector2(0.9f, 0.29f), Vector2.zero, new Color(0f, 0f, 0f, 0.4f));
        Button statusBtn = statusPanelGo.AddComponent<Button>();

        GameObject statusGo = CreateText(statusPanelGo.transform, "StatusText", "Gotowy do skanowania", 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        RectTransform statusRect = statusGo.GetComponent<RectTransform>();
        statusRect.anchorMin = Vector2.zero;
        statusRect.anchorMax = Vector2.one;
        statusRect.sizeDelta = Vector2.zero;
        Text statusText = statusGo.GetComponent<Text>();

        // 4. Result Panel (Bottom Sheet style card)
        GameObject resPanelGo = CreatePanel(safeAreaGo.transform, "ResultPanel", new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.85f), new Vector2(0f, 0f), new Color(0.08f, 0.09f, 0.12f, 0.95f));
        
        // Handle Bar
        GameObject handleGo = CreatePanel(resPanelGo.transform, "HandleBar", new Vector2(0.42f, 0.96f), new Vector2(0.58f, 0.98f), new Vector2(0f, 0f), new Color(0.3f, 0.3f, 0.3f, 0.8f));
        handleGo.GetComponent<Image>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // Close Result Button ("X" / Powrót)
        GameObject closeResBtnGo = new GameObject("CloseResultButton", typeof(Image), typeof(Button));
        closeResBtnGo.transform.SetParent(resPanelGo.transform, false);
        RectTransform closeResRect = closeResBtnGo.GetComponent<RectTransform>();
        closeResRect.anchorMin = new Vector2(0.88f, 0.93f);
        closeResRect.anchorMax = new Vector2(0.97f, 0.98f);
        closeResRect.sizeDelta = Vector2.zero;
        closeResBtnGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 0.8f);
        CreateText(closeResBtnGo.transform, "Text", "✕", 14, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Button closeResultBtn = closeResBtnGo.GetComponent<Button>();

        // Common Name
        GameObject commGo = CreateText(resPanelGo.transform, "CommonNameText", "Nazwa rośliny", 22, TextAnchor.MiddleLeft, new Color(0.18f, 0.8f, 0.44f), FontStyle.Bold);
        RectTransform commRect = commGo.GetComponent<RectTransform>();
        commRect.anchorMin = new Vector2(0.05f, 0.85f);
        commRect.anchorMax = new Vector2(0.6f, 0.94f);
        commRect.sizeDelta = Vector2.zero;

        // Score
        GameObject scoreGo = CreateText(resPanelGo.transform, "ScoreText", "Prawdopodobieństwo: 100%", 15, TextAnchor.MiddleRight, new Color(0.95f, 0.77f, 0.06f), FontStyle.Bold);
        RectTransform scoreRect = scoreGo.GetComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0.6f, 0.85f);
        scoreRect.anchorMax = new Vector2(0.95f, 0.94f);
        scoreRect.sizeDelta = Vector2.zero;

        // Scientific Name
        GameObject sciGo = CreateText(resPanelGo.transform, "ScientificNameText", "Scientific Name", 16, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f), FontStyle.Italic);
        RectTransform sciRect = sciGo.GetComponent<RectTransform>();
        sciRect.anchorMin = new Vector2(0.05f, 0.77f);
        sciRect.anchorMax = new Vector2(0.95f, 0.84f);
        sciRect.sizeDelta = Vector2.zero;

        // Separator line
        CreatePanel(resPanelGo.transform, "Line", new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.755f), new Vector2(0f, 0f), new Color(0.2f, 0.2f, 0.2f, 1f));

        // Description
        GameObject descGo = CreateText(resPanelGo.transform, "DescriptionText", "Opis rośliny...", 15, TextAnchor.UpperLeft, Color.white);
        RectTransform descRect = descGo.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.05f, 0.38f);
        descRect.anchorMax = new Vector2(0.68f, 0.72f);
        descRect.sizeDelta = Vector2.zero;

        // Thumbnail Image
        GameObject thumbGo = new GameObject("ThumbnailImage", typeof(RawImage));
        thumbGo.transform.SetParent(resPanelGo.transform, false);
        RectTransform thumbRect = thumbGo.GetComponent<RectTransform>();
        thumbRect.anchorMin = new Vector2(0.72f, 0.42f);
        thumbRect.anchorMax = new Vector2(0.95f, 0.68f);
        thumbRect.sizeDelta = Vector2.zero;
        RawImage thumbRaw = thumbGo.GetComponent<RawImage>();
        thumbRaw.color = Color.white;
        thumbGo.SetActive(false);

        // Fun Fact
        GameObject factGo = CreateText(resPanelGo.transform, "FunFactText", "Ciekawostka...", 14, TextAnchor.UpperLeft, new Color(0.5f, 0.8f, 1f));
        RectTransform factRect = factGo.GetComponent<RectTransform>();
        factRect.anchorMin = new Vector2(0.05f, 0.22f);
        factRect.anchorMax = new Vector2(0.95f, 0.43f);
        factRect.sizeDelta = Vector2.zero;

        // Location GPS label
        GameObject locationGo = CreateText(resPanelGo.transform, "ResultGpsLabel", "📍 GPS: Brak danych", 13, TextAnchor.MiddleLeft, new Color(0.8f, 0.9f, 1f), FontStyle.Italic);
        RectTransform locationRect = locationGo.GetComponent<RectTransform>();
        locationRect.anchorMin = new Vector2(0.05f, 0.14f);
        locationRect.anchorMax = new Vector2(0.95f, 0.21f);
        locationRect.sizeDelta = Vector2.zero;

        // Debug text for MapsButton verification
        GameObject resDebugGo = CreateText(resPanelGo.transform, "ResultDebugText", "MAP BUTTON EXISTS", 12, TextAnchor.MiddleLeft, Color.yellow, FontStyle.Bold);
        RectTransform resDebugRect = resDebugGo.GetComponent<RectTransform>();
        resDebugRect.anchorMin = new Vector2(0.05f, 0.22f);
        resDebugRect.anchorMax = new Vector2(0.95f, 0.26f);
        resDebugRect.sizeDelta = Vector2.zero;
        resDebugGo.GetComponent<Text>().raycastTarget = false;

        // Edibility status pill (Left side of bottom bar)
        GameObject ediblePanelGo = CreatePanel(resPanelGo.transform, "EdibilityPanel", new Vector2(0.05f, 0.02f), new Vector2(0.53f, 0.12f), new Vector2(0f, 0f), new Color(0.15f, 0.2f, 0.15f, 0.9f));
        GameObject edibGo = CreateText(ediblePanelGo.transform, "EdibilityText", "Status spożywczy: Brak danych", 11, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        RectTransform edibRect = edibGo.GetComponent<RectTransform>();
        edibRect.anchorMin = Vector2.zero;
        edibRect.anchorMax = Vector2.one;
        edibRect.sizeDelta = Vector2.zero;

        // Open in Maps Button (Right side of bottom bar)
        GameObject resMapsBtnGo = new GameObject("ResultOpenInMapsButton", typeof(Image), typeof(Button));
        resMapsBtnGo.transform.SetParent(resPanelGo.transform, false);
        RectTransform resMapsRect = resMapsBtnGo.GetComponent<RectTransform>();
        resMapsRect.anchorMin = new Vector2(0.55f, 0.02f);
        resMapsRect.anchorMax = new Vector2(0.95f, 0.12f);
        resMapsRect.sizeDelta = Vector2.zero;
        Image resMapsImg = resMapsBtnGo.GetComponent<Image>();
        resMapsImg.color = new Color(0.12f, 0.45f, 0.9f, 1.0f);
        resMapsImg.raycastTarget = true;
        GameObject resMapsTextGo = CreateText(resMapsBtnGo.transform, "Text", "🗺️ Otwórz w Mapach", 11, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        resMapsTextGo.GetComponent<Text>().raycastTarget = false;
        Button resultOpenInMapsBtn = resMapsBtnGo.GetComponent<Button>();
        resultOpenInMapsBtn.enabled = true;



        // 4.8 Dedicated History Overlay Canvas (Screen Space Overlay - Sorting Order 9999)
        GameObject historyOverlayCanvasGo = new GameObject("HistoryOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas historyCanvas = historyOverlayCanvasGo.GetComponent<Canvas>();
        historyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        historyCanvas.sortingOrder = 9999;
        historyCanvas.overrideSorting = true;

        CanvasScaler historyScaler = historyOverlayCanvasGo.GetComponent<CanvasScaler>();
        historyScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        historyScaler.referenceResolution = new Vector2(1080, 2340);
        historyScaler.matchWidthOrHeight = 0.5f;

        GameObject historyPanelGo = CreatePanel(historyOverlayCanvasGo.transform, "HistoryPanel", new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.88f), Vector2.zero, new Color(0.05f, 0.05f, 0.07f, 0.98f));
        historyOverlayCanvasGo.SetActive(false);

        // Header
        GameObject hTitleGo = CreateText(historyPanelGo.transform, "HistoryTitle", "📜 HISTORIA SKANOWANIA", 20, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
        RectTransform hTitleRect = hTitleGo.GetComponent<RectTransform>();
        hTitleRect.anchorMin = new Vector2(0.05f, 0.92f);
        hTitleRect.anchorMax = new Vector2(0.55f, 0.98f);
        hTitleRect.sizeDelta = Vector2.zero;

        // Debug text for HistoryPanel verification
        GameObject hDebugGo = CreateText(historyPanelGo.transform, "HistoryDebugText", "HISTORY PANEL OPEN", 11, TextAnchor.MiddleRight, Color.yellow, FontStyle.Bold);
        RectTransform hDebugRect = hDebugGo.GetComponent<RectTransform>();
        hDebugRect.anchorMin = new Vector2(0.56f, 0.92f);
        hDebugRect.anchorMax = new Vector2(0.84f, 0.98f);
        hDebugRect.sizeDelta = Vector2.zero;
        hDebugGo.GetComponent<Text>().raycastTarget = false;

        // Close History Button ("X")
        GameObject closeHistBtnGo = new GameObject("CloseHistoryButton", typeof(Image), typeof(Button));
        closeHistBtnGo.transform.SetParent(historyPanelGo.transform, false);
        RectTransform closeHistRect = closeHistBtnGo.GetComponent<RectTransform>();
        closeHistRect.anchorMin = new Vector2(0.85f, 0.92f);
        closeHistRect.anchorMax = new Vector2(0.95f, 0.98f);
        closeHistRect.sizeDelta = Vector2.zero;
        closeHistBtnGo.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        CreateText(closeHistBtnGo.transform, "Text", "✕", 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Button closeHistBtn = closeHistBtnGo.GetComponent<Button>();

        // Separator
        CreatePanel(historyPanelGo.transform, "HLine", new Vector2(0.03f, 0.91f), new Vector2(0.97f, 0.915f), Vector2.zero, new Color(0.25f, 0.25f, 0.25f, 1f));

        // Scroll View
        GameObject scrollGo = new GameObject("HistoryScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGo.transform.SetParent(historyPanelGo.transform, false);
        RectTransform scrollRectTransform = scrollGo.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0.03f, 0.12f);
        scrollRectTransform.anchorMax = new Vector2(0.97f, 0.90f);
        scrollRectTransform.sizeDelta = Vector2.zero;
        scrollGo.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.5f);
        ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Viewport
        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportGo.GetComponent<Image>().color = Color.white;
        Mask mask = viewportGo.GetComponent<Mask>();
        mask.showMaskGraphic = false;
        scrollRect.viewport = viewportRect;

        // Content
        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRect;

        // Bottom Bar (Clear History Button)
        GameObject clearHistBtnGo = new GameObject("ClearHistoryButton", typeof(Image), typeof(Button));
        clearHistBtnGo.transform.SetParent(historyPanelGo.transform, false);
        RectTransform clearHistRect = clearHistBtnGo.GetComponent<RectTransform>();
        clearHistRect.anchorMin = new Vector2(0.05f, 0.03f);
        clearHistRect.anchorMax = new Vector2(0.95f, 0.09f);
        clearHistRect.sizeDelta = Vector2.zero;
        clearHistBtnGo.GetComponent<Image>().color = new Color(0.6f, 0.15f, 0.15f, 0.9f);
        CreateText(clearHistBtnGo.transform, "Text", "🗑️ WYCZYŚĆ HISTORIĘ", 14, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Button clearHistBtn = clearHistBtnGo.GetComponent<Button>();

        // 5. Create AppManager
        GameObject appManagerGo = new GameObject("AppManager");
        BackendClient client = appManagerGo.AddComponent<BackendClient>();
        UIManager uiManager = appManagerGo.AddComponent<UIManager>();
        Undo.RegisterCreatedObjectUndo(appManagerGo, "Create AppManager");

        // 6. Setup UIManager fields via reflection
        SetRef(uiManager, "backendClient", client);
        SetRef(uiManager, "backendUrlInput", urlInputField);
        SetRef(uiManager, "modeDropdown", dropdown);
        SetRef(uiManager, "cameraPreviewUI", preview);
        SetRef(uiManager, "thumbnailPreviewUI", thumbRaw);
        SetRef(uiManager, "resultPanel", resPanelGo);
        SetRef(uiManager, "languageOverlayCanvas", langCanvas);
        SetRef(uiManager, "languageDropdown", langDropdown);
        SetRef(uiManager, "titleText", titleGo.GetComponent<Text>());
        SetRef(uiManager, "historyButtonText", historyBtnTextGo.GetComponent<Text>());
        SetRef(uiManager, "historyTitleText", hTitleGo.GetComponent<Text>());
        SetRef(uiManager, "clearHistoryButtonText", clearHistBtnGo.GetComponentInChildren<Text>());
        SetRef(uiManager, "historyButton", historyButton);
        SetRef(uiManager, "historyOverlayCanvas", historyCanvas);
        SetRef(uiManager, "historyPanel", historyPanelGo);
        SetRef(uiManager, "closeHistoryButton", closeHistBtn);
        SetRef(uiManager, "clearHistoryButton", clearHistBtn);
        SetRef(uiManager, "historyContentContainer", contentGo.transform);
        SetRef(uiManager, "historyDebugText", hDebugGo.GetComponent<Text>());
        SetRef(uiManager, "commonNameText", commGo.GetComponent<Text>());
        SetRef(uiManager, "scientificNameText", sciGo.GetComponent<Text>());
        SetRef(uiManager, "scoreText", scoreGo.GetComponent<Text>());
        SetRef(uiManager, "descriptionText", descGo.GetComponent<Text>());
        SetRef(uiManager, "funFactText", factGo.GetComponent<Text>());
        SetRef(uiManager, "edibilityText", edibGo.GetComponent<Text>());
        SetRef(uiManager, "resultGpsLabel", locationGo.GetComponent<Text>());
        SetRef(uiManager, "resultOpenInMapsButton", resultOpenInMapsBtn);
        SetRef(uiManager, "resultDebugText", resDebugGo.GetComponent<Text>());
        Debug.Log("[MapsButton] SetupUI created ResultOpenInMapsButton");
        SetRef(uiManager, "statusMessageText", statusText);
        SetRef(uiManager, "statusButton", statusBtn);
        SetRef(uiManager, "closeResultButton", closeResultBtn);
        SetRef(uiManager, "scanButton", scanButton);
        SetRef(uiManager, "galleryButton", galleryButton);
        SetRef(uiManager, "arMobileRoot", arMobileRoot);
        SetRef(uiManager, "pcCamera", GameObject.Find("Main Camera"));

        // Mark scene dirty and save it
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // 7. Configure Project Settings & Build Settings automatically
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.bakolla.arherb");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
        buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.path != scenePath)
            {
                buildScenes.Add(s);
            }
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();

        // 8. Ensure URP Renderer features include AR Background Renderer Feature (fixes mobile blue/gray screen)
        AddARBackgroundRendererFeatureToAllRenderers();

        Debug.Log($"[SetupUI] Successfully generated MainARScene at '{scenePath}'. AppManager is completely configured.");
        EditorUtility.DisplayDialog("AR Herb Setup", "Pomyślnie wygenerowano scenę MainARScene!\nUstawiono identyfikator pakietu Android na com.bakolla.arherb oraz dodano scenę do okna Build Settings.", "OK");
    }

    private static void AddARBackgroundRendererFeatureToAllRenderers()
    {
        string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData");
        if (guids == null || guids.Length == 0)
        {
            Debug.LogWarning("[SetupUI] No UniversalRendererData assets found. Make sure URP is installed.");
            return;
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var rendererData = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(path);
            if (rendererData != null)
            {
                bool hasFeature = false;
                if (rendererData.rendererFeatures != null)
                {
                    foreach (var feature in rendererData.rendererFeatures)
                    {
                        if (feature != null && feature.GetType().Name == "ARBackgroundRendererFeature")
                        {
                            hasFeature = true;
                            break;
                        }
                    }
                }

                if (!hasFeature)
                {
                    System.Type type = System.Type.GetType("UnityEngine.XR.ARFoundation.ARBackgroundRendererFeature, Unity.XR.ARFoundation");
                    if (type != null)
                    {
                        var featureInstance = ScriptableObject.CreateInstance(type) as UnityEngine.Rendering.Universal.ScriptableRendererFeature;
                        if (featureInstance != null)
                        {
                            featureInstance.name = "ARBackgroundRendererFeature";
                            AssetDatabase.AddObjectToAsset(featureInstance, rendererData);
                            rendererData.rendererFeatures.Add(featureInstance);
                            EditorUtility.SetDirty(rendererData);
                            Debug.Log($"[SetupUI] Successfully added ARBackgroundRendererFeature to URP Renderer: {path}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[SetupUI] Could not find ARBackgroundRendererFeature type. Make sure AR Foundation package is installed.");
                    }
                }
                else
                {
                    Debug.Log($"[SetupUI] URP Renderer at '{path}' already has ARBackgroundRendererFeature.");
                }
            }
        }
        AssetDatabase.SaveAssets();
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Color color)
    {
        GameObject go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.sizeDelta = sizeDelta;
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static GameObject CreateText(Transform parent, string name, string defaultText, int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
    {
        GameObject go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.text = defaultText;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = alignment;
        t.color = color;
        t.fontStyle = style;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return go;
    }

    private static void SetRef(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(target, value);
        }
        else
        {
            Debug.LogWarning($"[SetupUI] Field '{fieldName}' not found in '{target.GetType().Name}'.");
        }
    }

    private static GameObject CreateARFoundationObjects()
    {
        // Always create a standard PC Camera as fallback/main camera for Editor/PC testing
        GameObject pcCamGo = new GameObject("Main Camera", typeof(Camera));
        pcCamGo.tag = "MainCamera";
        pcCamGo.transform.position = new Vector3(0, 0, -10);
        Undo.RegisterCreatedObjectUndo(pcCamGo, "Create PC Main Camera");

        // Try to locate AR Foundation types
        System.Type arSessionType = System.Type.GetType("UnityEngine.XR.ARFoundation.ARSession, Unity.XR.ARFoundation");
        System.Type arInputManagerType = System.Type.GetType("UnityEngine.XR.ARFoundation.ARInputManager, Unity.XR.ARFoundation");
        System.Type xrOriginType = System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
        System.Type arSessionOriginType = System.Type.GetType("UnityEngine.XR.ARFoundation.ARSessionOrigin, Unity.XR.ARFoundation");
        System.Type arCameraManagerType = System.Type.GetType("UnityEngine.XR.ARFoundation.ARCameraManager, Unity.XR.ARFoundation");
        System.Type arCameraBackgroundType = System.Type.GetType("UnityEngine.XR.ARFoundation.ARCameraBackground, Unity.XR.ARFoundation");

        bool hasAR = arSessionType != null || xrOriginType != null || arSessionOriginType != null;

        if (!hasAR)
        {
            Debug.Log("[SetupUI] AR Foundation package not detected. Created default Main Camera for PC testing.");
            return null;
        }

        // Create ARMobileRoot
        GameObject arMobileRoot = new GameObject("ARMobileRoot");
        Undo.RegisterCreatedObjectUndo(arMobileRoot, "Create ARMobileRoot");

        // Create AR Session inside root
        if (arSessionType != null)
        {
            GameObject arSessionGo = new GameObject("AR Session");
            arSessionGo.transform.SetParent(arMobileRoot.transform, false);
            arSessionGo.AddComponent(arSessionType);
            if (arInputManagerType != null)
            {
                arSessionGo.AddComponent(arInputManagerType);
            }
            Debug.Log("[SetupUI] Created AR Session under ARMobileRoot.");
        }

        // Create Origin inside root
        if (xrOriginType != null)
        {
            GameObject xrOriginGo = new GameObject("XR Origin");
            xrOriginGo.transform.SetParent(arMobileRoot.transform, false);
            xrOriginGo.AddComponent(xrOriginType);

            GameObject cameraOffsetGo = new GameObject("Camera Offset");
            cameraOffsetGo.transform.SetParent(xrOriginGo.transform, false);

            // Create AR Camera (do NOT tag MainCamera to avoid conflict in Editor)
            GameObject arCameraGo = new GameObject("AR Camera", typeof(Camera));
            arCameraGo.transform.SetParent(cameraOffsetGo.transform, false);

            if (arCameraManagerType != null) arCameraGo.AddComponent(arCameraManagerType);
            if (arCameraBackgroundType != null) arCameraGo.AddComponent(arCameraBackgroundType);

            var xrOrigin = xrOriginGo.GetComponent(xrOriginType);
            var offsetProp = xrOriginType.GetProperty("CameraFloorOffsetObject");
            var cameraProp = xrOriginType.GetProperty("Camera");

            if (offsetProp != null) offsetProp.SetValue(xrOrigin, cameraOffsetGo);
            if (cameraProp != null) cameraProp.SetValue(xrOrigin, arCameraGo.GetComponent<Camera>());

            Debug.Log("[SetupUI] Created XR Origin under ARMobileRoot.");
        }
        else if (arSessionOriginType != null)
        {
            GameObject arOriginGo = new GameObject("AR Session Origin");
            arOriginGo.transform.SetParent(arMobileRoot.transform, false);
            arOriginGo.AddComponent(arSessionOriginType);

            GameObject arCameraGo = new GameObject("AR Camera", typeof(Camera));
            arCameraGo.transform.SetParent(arOriginGo.transform, false);

            if (arCameraManagerType != null) arCameraGo.AddComponent(arCameraManagerType);
            if (arCameraBackgroundType != null) arCameraGo.AddComponent(arCameraBackgroundType);

            var arOrigin = arOriginGo.GetComponent(arSessionOriginType);
            var cameraProp = arSessionOriginType.GetProperty("camera");
            if (cameraProp != null) cameraProp.SetValue(arOrigin, arCameraGo.GetComponent<Camera>());

            Debug.Log("[SetupUI] Created AR Session Origin under ARMobileRoot.");
        }

        // Deactivate ARMobileRoot by default in Editor
        arMobileRoot.SetActive(false);
        Debug.Log("[SetupUI] AR Foundation components structured under ARMobileRoot and deactivated by default.");

        return arMobileRoot;
    }
}
