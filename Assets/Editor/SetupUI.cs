using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using ARHerb.UI;
using ARHerb.Network;

public class SetupUI
{
    [MenuItem("Tools/Build UI")]
    public static void BuildUI()
    {
        // 1. Open the SampleScene or make sure it's active
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        
        // Destroy existing Canvas or AppManager if they exist to avoid duplication
        var oldCanvas = GameObject.Find("Canvas");
        if (oldCanvas != null)
        {
            Undo.DestroyObjectImmediate(oldCanvas);
        }
        var oldAppManager = GameObject.Find("AppManager");
        if (oldAppManager != null)
        {
            Undo.DestroyObjectImmediate(oldAppManager);
        }
        var oldEventSystem = GameObject.Find("EventSystem");
        if (oldEventSystem != null)
        {
            Undo.DestroyObjectImmediate(oldEventSystem);
        }

        // Create standard DefaultControls resources
        DefaultControls.Resources uiResources = new DefaultControls.Resources();
        
        // Populate standard resources from Unity Editor extra assets
        uiResources.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        uiResources.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        uiResources.inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
        uiResources.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        uiResources.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        uiResources.dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        uiResources.mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");

        // 2. Create Canvas
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); // Mobile vertical reference
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
        
        // Add EventSystem if not present
        GameObject eventSystemGo = GameObject.Find("EventSystem");
        if (eventSystemGo == null)
        {
            eventSystemGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
        }

        // 3. Add RawImage - Fullscreen CameraPreview
        GameObject cameraPreviewGo = DefaultControls.CreateRawImage(uiResources);
        cameraPreviewGo.name = "CameraPreview";
        cameraPreviewGo.transform.SetParent(canvasGo.transform, false);
        var previewRect = cameraPreviewGo.GetComponent<RectTransform>();
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.offsetMin = Vector2.zero;
        previewRect.offsetMax = Vector2.zero; // Fullscreen stretch
        RawImage cameraPreviewRawImage = cameraPreviewGo.GetComponent<RawImage>();
        cameraPreviewRawImage.color = Color.white;

        // 4. Create Top Header Panel (Modern Glassmorphic Header)
        GameObject headerPanelGo = DefaultControls.CreatePanel(uiResources);
        headerPanelGo.name = "Header Panel";
        headerPanelGo.transform.SetParent(canvasGo.transform, false);
        var headerRect = headerPanelGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.anchoredPosition = new Vector2(0, -40); // Pivot centered at top
        headerRect.sizeDelta = new Vector2(0, 80);
        var headerImage = headerPanelGo.GetComponent<Image>();
        if (headerImage != null)
        {
            headerImage.sprite = uiResources.standard;
            headerImage.color = new Color(0.05f, 0.06f, 0.08f, 0.85f); // Premium dark translucent
        }

        // Header Title Text
        GameObject headerTextGo = DefaultControls.CreateText(uiResources);
        headerTextGo.name = "Header Title";
        headerTextGo.transform.SetParent(headerPanelGo.transform, false);
        var headerTextRect = headerTextGo.GetComponent<RectTransform>();
        headerTextRect.anchorMin = new Vector2(0f, 0f);
        headerTextRect.anchorMax = new Vector2(1f, 1f);
        headerTextRect.offsetMin = new Vector2(20, 0);
        headerTextRect.offsetMax = new Vector2(-20, 0);
        Text headerText = headerTextGo.GetComponent<Text>();
        headerText.text = "🌿 HERB & FAUNA SCANNER";
        headerText.alignment = TextAnchor.MiddleCenter;
        headerText.fontStyle = FontStyle.Bold;
        headerText.fontSize = 28;
        headerText.color = new Color(0.18f, 0.8f, 0.44f); // Emerald Green

        // 5. Add InputField — Backend URL (styled as a search/config bar below the header)
        GameObject backendUrlGo = DefaultControls.CreateInputField(uiResources);
        backendUrlGo.name = "Backend URL Input";
        backendUrlGo.transform.SetParent(canvasGo.transform, false);
        var urlRect = backendUrlGo.GetComponent<RectTransform>();
        urlRect.anchorMin = new Vector2(0.5f, 1f);
        urlRect.anchorMax = new Vector2(0.5f, 1f);
        urlRect.anchoredPosition = new Vector2(0, -110);
        urlRect.sizeDelta = new Vector2(420, 45);
        InputField backendUrlInput = backendUrlGo.GetComponent<InputField>();
        var inputImage = backendUrlGo.GetComponent<Image>();
        if (inputImage != null)
        {
            inputImage.sprite = uiResources.standard;
            inputImage.color = new Color(0.1f, 0.11f, 0.14f, 0.9f); // Dark fill
        }
        var inputTexts = backendUrlGo.GetComponentsInChildren<Text>(true);
        foreach (var t in inputTexts)
        {
            t.color = Color.white;
            t.fontSize = 16;
            if (t.name == "Placeholder") t.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        }

        // 6. Mode Selection Dropdown (placed above scan button)
        GameObject dropdownGo = DefaultControls.CreateDropdown(uiResources);
        dropdownGo.name = "Category Dropdown";
        dropdownGo.transform.SetParent(canvasGo.transform, false);
        var dropdownRect = dropdownGo.GetComponent<RectTransform>();
        dropdownRect.anchorMin = new Vector2(0.5f, 0f);
        dropdownRect.anchorMax = new Vector2(0.5f, 0f);
        dropdownRect.anchoredPosition = new Vector2(0, 190);
        dropdownRect.sizeDelta = new Vector2(280, 50);
        Dropdown modeDropdown = dropdownGo.GetComponent<Dropdown>();
        modeDropdown.options.Clear();
        modeDropdown.options.Add(new Dropdown.OptionData("Plants (Rośliny)"));
        modeDropdown.options.Add(new Dropdown.OptionData("Mushrooms (Grzyby)"));
        modeDropdown.options.Add(new Dropdown.OptionData("Insects (Owady)"));
        modeDropdown.options.Add(new Dropdown.OptionData("Stones (Kamienie)"));
        var dropdownImage = dropdownGo.GetComponent<Image>();
        if (dropdownImage != null)
        {
            dropdownImage.sprite = uiResources.standard;
            dropdownImage.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
        }
        var dropdownLabel = dropdownGo.GetComponentInChildren<Text>();
        if (dropdownLabel != null)
        {
            dropdownLabel.color = Color.white;
            dropdownLabel.fontSize = 18;
            dropdownLabel.alignment = TextAnchor.MiddleCenter;
        }

        // 7. Large Circular Scan Button (Modern Figma-style capture trigger)
        GameObject scanButtonGo = DefaultControls.CreateButton(uiResources);
        scanButtonGo.name = "Scan Button";
        scanButtonGo.transform.SetParent(canvasGo.transform, false);
        var scanRect = scanButtonGo.GetComponent<RectTransform>();
        scanRect.anchorMin = new Vector2(0.5f, 0f);
        scanRect.anchorMax = new Vector2(0.5f, 0f);
        scanRect.anchoredPosition = new Vector2(0, 90);
        scanRect.sizeDelta = new Vector2(110, 110); // Large circle size
        
        Button scanButton = scanButtonGo.GetComponent<Button>();
        var buttonImage = scanButtonGo.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.sprite = uiResources.knob; // Use standard circular knob
            buttonImage.color = new Color(0.18f, 0.8f, 0.44f, 0.95f); // Vibrant emerald green
        }
        
        // Subtle outer border ring for the button
        GameObject ringGo = new GameObject("Outer Ring", typeof(Image));
        ringGo.transform.SetParent(scanButtonGo.transform, false);
        var ringRect = ringGo.GetComponent<RectTransform>();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.sizeDelta = new Vector2(16, 16); // Slightly larger than parent button
        var ringImage = ringGo.GetComponent<Image>();
        ringImage.sprite = uiResources.knob;
        ringImage.color = new Color(1f, 1f, 1f, 0.25f); // White halo
        
        var buttonText = scanButtonGo.GetComponentInChildren<Text>();
        if (buttonText != null) 
        {
            buttonText.text = "SCAN";
            buttonText.color = Color.white;
            buttonText.fontSize = 18;
            buttonText.fontStyle = FontStyle.Bold;
        }

        // 8. Text — Status Text (Loading/Status Display)
        GameObject statusTextGo = DefaultControls.CreateText(uiResources);
        statusTextGo.name = "Status Text";
        statusTextGo.transform.SetParent(canvasGo.transform, false);
        var statusRect = statusTextGo.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0f);
        statusRect.anchorMax = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0, 270);
        statusRect.sizeDelta = new Vector2(600, 40);
        Text statusText = statusTextGo.GetComponent<Text>();
        statusText.text = "Ready to scan";
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = new Color(1f, 1f, 1f, 0.9f);
        statusText.fontSize = 18;

        // 9. Panel — Result Panel (Figma-style Bottom Sheet Card)
        GameObject resultPanelGo = DefaultControls.CreatePanel(uiResources);
        resultPanelGo.name = "Result Panel";
        resultPanelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = resultPanelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0, 520); // Anchored above controls
        panelRect.sizeDelta = new Vector2(480, 440);      // Tall bottom card
        var panelImage = resultPanelGo.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.sprite = uiResources.standard;
            panelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.95f); // Sleek card background
        }

        // Bottom Sheet Handle (Mock pill handle for visual design)
        GameObject handleGo = new GameObject("Handle Bar", typeof(Image));
        handleGo.transform.SetParent(resultPanelGo.transform, false);
        var handleRect = handleGo.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 1f);
        handleRect.anchorMax = new Vector2(0.5f, 1f);
        handleRect.anchoredPosition = new Vector2(0, -10);
        handleRect.sizeDelta = new Vector2(60, 6);
        var handleImg = handleGo.GetComponent<Image>();
        handleImg.sprite = uiResources.standard;
        handleImg.color = new Color(1f, 1f, 1f, 0.25f);

        // commonNameText (Primary result header)
        GameObject commonNameGo = DefaultControls.CreateText(uiResources);
        commonNameGo.name = "CommonNameText";
        commonNameGo.transform.SetParent(resultPanelGo.transform, false);
        var commonNameRect = commonNameGo.GetComponent<RectTransform>();
        commonNameRect.anchorMin = new Vector2(0, 1);
        commonNameRect.anchorMax = new Vector2(1, 1);
        commonNameRect.anchoredPosition = new Vector2(15, -45);
        commonNameRect.sizeDelta = new Vector2(-150, 40); // Leave space for score badge
        Text commonNameText = commonNameGo.GetComponent<Text>();
        commonNameText.text = "Common Name";
        commonNameText.alignment = TextAnchor.MiddleLeft;
        commonNameText.fontStyle = FontStyle.Bold;
        commonNameText.fontSize = 24;
        commonNameText.color = new Color(0.18f, 0.8f, 0.44f); // Emerald Green

        // scoreText (Right-aligned tag badge)
        GameObject scoreGo = DefaultControls.CreateText(uiResources);
        scoreGo.name = "ScoreText";
        scoreGo.transform.SetParent(resultPanelGo.transform, false);
        var scoreRect = scoreGo.GetComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(1, 1);
        scoreRect.anchorMax = new Vector2(1, 1);
        scoreRect.anchoredPosition = new Vector2(-15, -45);
        scoreRect.sizeDelta = new Vector2(120, 40);
        Text scoreText = scoreGo.GetComponent<Text>();
        scoreText.text = "Score";
        scoreText.alignment = TextAnchor.MiddleRight;
        scoreText.fontStyle = FontStyle.Bold;
        scoreText.fontSize = 16;
        scoreText.color = new Color(0.95f, 0.77f, 0.06f); // Gold

        // scientificNameText (Secondary italic label)
        GameObject scientificNameGo = DefaultControls.CreateText(uiResources);
        scientificNameGo.name = "ScientificNameText";
        scientificNameGo.transform.SetParent(resultPanelGo.transform, false);
        var sciNameRect = scientificNameGo.GetComponent<RectTransform>();
        sciNameRect.anchorMin = new Vector2(0, 1);
        sciNameRect.anchorMax = new Vector2(1, 1);
        sciNameRect.anchoredPosition = new Vector2(15, -85);
        sciNameRect.sizeDelta = new Vector2(-30, 30);
        Text scientificNameText = scientificNameGo.GetComponent<Text>();
        scientificNameText.text = "Scientific Name";
        scientificNameText.alignment = TextAnchor.MiddleLeft;
        scientificNameText.fontStyle = FontStyle.Italic;
        scientificNameText.fontSize = 16;
        scientificNameText.color = new Color(0.74f, 0.76f, 0.78f);

        // Thin Separator line in UI card
        GameObject separatorGo = new GameObject("UI Separator", typeof(Image));
        separatorGo.transform.SetParent(resultPanelGo.transform, false);
        var sepRect = separatorGo.GetComponent<RectTransform>();
        sepRect.anchorMin = new Vector2(0f, 1f);
        sepRect.anchorMax = new Vector2(1f, 1f);
        sepRect.anchoredPosition = new Vector2(0, -120);
        sepRect.sizeDelta = new Vector2(-30, 2);
        var sepImg = separatorGo.GetComponent<Image>();
        sepImg.color = new Color(1f, 1f, 1f, 0.1f);

        // descriptionText (Clean body block)
        GameObject descGo = DefaultControls.CreateText(uiResources);
        descGo.name = "DescriptionText";
        descGo.transform.SetParent(resultPanelGo.transform, false);
        var descRect = descGo.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 1);
        descRect.anchorMax = new Vector2(1, 1);
        descRect.anchoredPosition = new Vector2(15, -200);
        descRect.sizeDelta = new Vector2(-30, 120);
        Text descriptionText = descGo.GetComponent<Text>();
        descriptionText.text = "Description";
        descriptionText.alignment = TextAnchor.UpperLeft;
        descriptionText.color = new Color(0.95f, 0.95f, 0.95f);
        descriptionText.fontSize = 15;

        // funFactText (Blue box or stylized block)
        GameObject funFactGo = DefaultControls.CreateText(uiResources);
        funFactGo.name = "FunFactText";
        funFactGo.transform.SetParent(resultPanelGo.transform, false);
        var funFactRect = funFactGo.GetComponent<RectTransform>();
        funFactRect.anchorMin = new Vector2(0, 1);
        funFactRect.anchorMax = new Vector2(1, 1);
        funFactRect.anchoredPosition = new Vector2(15, -305);
        funFactRect.sizeDelta = new Vector2(-30, 70);
        Text funFactText = funFactGo.GetComponent<Text>();
        funFactText.text = "Fun Fact";
        funFactText.alignment = TextAnchor.UpperLeft;
        funFactText.color = new Color(0.2f, 0.6f, 1f); // Soft premium blue
        funFactText.fontSize = 14;

        // edibilityText (Highlighted indicator pill)
        GameObject edibilityGo = DefaultControls.CreateText(uiResources);
        edibilityGo.name = "EdibilityText";
        edibilityGo.transform.SetParent(resultPanelGo.transform, false);
        var edibilityRect = edibilityGo.GetComponent<RectTransform>();
        edibilityRect.anchorMin = new Vector2(0, 1);
        edibilityRect.anchorMax = new Vector2(1, 1);
        edibilityRect.anchoredPosition = new Vector2(15, -395);
        edibilityRect.sizeDelta = new Vector2(-30, 45);
        Text edibilityText = edibilityGo.GetComponent<Text>();
        edibilityText.text = "Edibility";
        edibilityText.alignment = TextAnchor.MiddleCenter;
        edibilityText.color = new Color(0.9f, 0.5f, 0.5f);
        edibilityText.fontStyle = FontStyle.Bold;
        edibilityText.fontSize = 15;

        // Assign proper legacy fonts to all Text elements
        AssignDefaultFonts(canvasGo);

        // 10. Create AppManager Empty GameObject
        GameObject appManagerGo = new GameObject("AppManager");
        Undo.RegisterCreatedObjectUndo(appManagerGo, "Create AppManager");

        // 11. Add BackendClient and UIManager components
        BackendClient backendClient = appManagerGo.AddComponent<BackendClient>();
        UIManager uiManager = appManagerGo.AddComponent<UIManager>();

        // 12. Connect fields in UIManager using SerializedObject
        SerializedObject so = new SerializedObject(uiManager);
        so.FindProperty("backendClient").objectReferenceValue = backendClient;
        so.FindProperty("backendUrlInput").objectReferenceValue = backendUrlInput;
        so.FindProperty("modeDropdown").objectReferenceValue = modeDropdown;
        so.FindProperty("cameraPreviewUI").objectReferenceValue = cameraPreviewRawImage;
        so.FindProperty("resultPanel").objectReferenceValue = resultPanelGo;
        so.FindProperty("commonNameText").objectReferenceValue = commonNameText;
        so.FindProperty("scientificNameText").objectReferenceValue = scientificNameText;
        so.FindProperty("scoreText").objectReferenceValue = scoreText;
        so.FindProperty("descriptionText").objectReferenceValue = descriptionText;
        so.FindProperty("funFactText").objectReferenceValue = funFactText;
        so.FindProperty("edibilityText").objectReferenceValue = edibilityText;
        so.FindProperty("statusMessageText").objectReferenceValue = statusText;
        so.FindProperty("scanButton").objectReferenceValue = scanButton;
        
        // Apply properties
        so.ApplyModifiedProperties();

        // Save scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        
        Debug.Log("Figma/Lovable-style UI built successfully in SampleScene!");
    }

    private static void AssignDefaultFonts(GameObject root)
    {
        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
        {
            defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (defaultFont != null)
        {
            foreach (var txt in root.GetComponentsInChildren<Text>(true))
            {
                if (txt.font == null)
                {
                    txt.font = defaultFont;
                }
            }
        }
    }
}
