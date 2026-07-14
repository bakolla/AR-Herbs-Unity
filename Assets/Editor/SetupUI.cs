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
        
        // Let's destroy existing Canvas or AppManager if they exist to avoid duplication
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
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
        
        // Add EventSystem if not present
        GameObject eventSystemGo = GameObject.Find("EventSystem");
        if (eventSystemGo == null)
        {
            eventSystemGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
        }

        // 3. Add RawImage - CameraPreview
        GameObject cameraPreviewGo = DefaultControls.CreateRawImage(uiResources);
        cameraPreviewGo.name = "CameraPreview";
        cameraPreviewGo.transform.SetParent(canvasGo.transform, false);
        var previewRect = cameraPreviewGo.GetComponent<RectTransform>();
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.offsetMin = Vector2.zero;
        previewRect.offsetMax = Vector2.zero; // Fullscreen stretch
        RawImage cameraPreviewRawImage = cameraPreviewGo.GetComponent<RawImage>();

        // 4. Add InputField — Backend URL
        GameObject backendUrlGo = DefaultControls.CreateInputField(uiResources);
        backendUrlGo.name = "Backend URL";
        backendUrlGo.transform.SetParent(canvasGo.transform, false);
        var urlRect = backendUrlGo.GetComponent<RectTransform>();
        urlRect.anchorMin = new Vector2(0.5f, 1f);
        urlRect.anchorMax = new Vector2(0.5f, 1f);
        urlRect.anchoredPosition = new Vector2(0, -50);
        urlRect.sizeDelta = new Vector2(400, 40);
        InputField backendUrlInput = backendUrlGo.GetComponent<InputField>();
        var inputImage = backendUrlGo.GetComponent<Image>();
        if (inputImage != null) inputImage.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        var inputTexts = backendUrlGo.GetComponentsInChildren<Text>(true);
        foreach (var t in inputTexts) t.color = Color.white;

        // 5. Add Dropdown — Plants/Mushrooms/Insects/Stones
        GameObject dropdownGo = DefaultControls.CreateDropdown(uiResources);
        dropdownGo.name = "Plants/Mushrooms/Insects/Stones";
        dropdownGo.transform.SetParent(canvasGo.transform, false);
        var dropdownRect = dropdownGo.GetComponent<RectTransform>();
        dropdownRect.anchorMin = new Vector2(0.5f, 1f);
        dropdownRect.anchorMax = new Vector2(0.5f, 1f);
        dropdownRect.anchoredPosition = new Vector2(-110, -100);
        dropdownRect.sizeDelta = new Vector2(180, 40);
        Dropdown modeDropdown = dropdownGo.GetComponent<Dropdown>();
        modeDropdown.options.Clear();
        modeDropdown.options.Add(new Dropdown.OptionData("Plants"));
        modeDropdown.options.Add(new Dropdown.OptionData("Mushrooms"));
        modeDropdown.options.Add(new Dropdown.OptionData("Insects"));
        modeDropdown.options.Add(new Dropdown.OptionData("Stones"));
        var dropdownImage = dropdownGo.GetComponent<Image>();
        if (dropdownImage != null) dropdownImage.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        var dropdownLabel = dropdownGo.GetComponentInChildren<Text>();
        if (dropdownLabel != null) dropdownLabel.color = Color.white;
        
        // 6. Add Button — Scan
        GameObject scanButtonGo = DefaultControls.CreateButton(uiResources);
        scanButtonGo.name = "Scan";
        scanButtonGo.transform.SetParent(canvasGo.transform, false);
        var scanRect = scanButtonGo.GetComponent<RectTransform>();
        scanRect.anchorMin = new Vector2(0.5f, 1f);
        scanRect.anchorMax = new Vector2(0.5f, 1f);
        scanRect.anchoredPosition = new Vector2(110, -100);
        scanRect.sizeDelta = new Vector2(180, 40);
        Button scanButton = scanButtonGo.GetComponent<Button>();
        var buttonImage = scanButtonGo.GetComponent<Image>();
        if (buttonImage != null) buttonImage.color = new Color(0.18f, 0.8f, 0.44f, 0.95f);
        var buttonText = scanButtonGo.GetComponentInChildren<Text>();
        if (buttonText != null) 
        {
            buttonText.text = "SCAN SPECIMEN";
            buttonText.color = Color.white;
            buttonText.fontStyle = FontStyle.Bold;
        }

        // 7. Add Text — Status Text
        GameObject statusTextGo = DefaultControls.CreateText(uiResources);
        statusTextGo.name = "Status Text";
        statusTextGo.transform.SetParent(canvasGo.transform, false);
        var statusRect = statusTextGo.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0f);
        statusRect.anchorMax = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0, 100);
        statusRect.sizeDelta = new Vector2(500, 30);
        Text statusText = statusTextGo.GetComponent<Text>();
        statusText.text = "Ready to scan";
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = Color.white;
        statusText.fontSize = 16;

        // 8. Add Panel — Result Panel
        GameObject resultPanelGo = DefaultControls.CreatePanel(uiResources);
        resultPanelGo.name = "Result Panel";
        resultPanelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = resultPanelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0, 200);
        panelRect.sizeDelta = new Vector2(500, 250);
        var panelImage = resultPanelGo.GetComponent<Image>();
        if (panelImage != null) panelImage.color = new Color(0.08f, 0.08f, 0.08f, 0.88f);

        // Add Content to Result Panel
        // commonNameText
        GameObject commonNameGo = DefaultControls.CreateText(uiResources);
        commonNameGo.name = "CommonNameText";
        commonNameGo.transform.SetParent(resultPanelGo.transform, false);
        var commonNameRect = commonNameGo.GetComponent<RectTransform>();
        commonNameRect.anchorMin = new Vector2(0, 1);
        commonNameRect.anchorMax = new Vector2(1, 1);
        commonNameRect.anchoredPosition = new Vector2(0, -30);
        commonNameRect.sizeDelta = new Vector2(-20, 30);
        Text commonNameText = commonNameGo.GetComponent<Text>();
        commonNameText.text = "Common Name";
        commonNameText.alignment = TextAnchor.MiddleCenter;
        commonNameText.fontStyle = FontStyle.Bold;
        commonNameText.fontSize = 18;
        commonNameText.color = new Color(0.18f, 0.8f, 0.44f); // Light Green

        // scientificNameText
        GameObject scientificNameGo = DefaultControls.CreateText(uiResources);
        scientificNameGo.name = "ScientificNameText";
        scientificNameGo.transform.SetParent(resultPanelGo.transform, false);
        var sciNameRect = scientificNameGo.GetComponent<RectTransform>();
        sciNameRect.anchorMin = new Vector2(0, 1);
        sciNameRect.anchorMax = new Vector2(1, 1);
        sciNameRect.anchoredPosition = new Vector2(0, -60);
        sciNameRect.sizeDelta = new Vector2(-20, 30);
        Text scientificNameText = scientificNameGo.GetComponent<Text>();
        scientificNameText.text = "Scientific Name";
        scientificNameText.alignment = TextAnchor.MiddleCenter;
        scientificNameText.fontStyle = FontStyle.Italic;
        scientificNameText.fontSize = 15;
        scientificNameText.color = new Color(0.9f, 0.9f, 0.9f);

        // scoreText
        GameObject scoreGo = DefaultControls.CreateText(uiResources);
        scoreGo.name = "ScoreText";
        scoreGo.transform.SetParent(resultPanelGo.transform, false);
        var scoreRect = scoreGo.GetComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0, 1);
        scoreRect.anchorMax = new Vector2(1, 1);
        scoreRect.anchoredPosition = new Vector2(0, -90);
        scoreRect.sizeDelta = new Vector2(-20, 30);
        Text scoreText = scoreGo.GetComponent<Text>();
        scoreText.text = "Score";
        scoreText.alignment = TextAnchor.MiddleCenter;
        scoreText.fontSize = 14;
        scoreText.color = new Color(0.95f, 0.77f, 0.06f); // Gold

        // descriptionText
        GameObject descGo = DefaultControls.CreateText(uiResources);
        descGo.name = "DescriptionText";
        descGo.transform.SetParent(resultPanelGo.transform, false);
        var descRect = descGo.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 1);
        descRect.anchorMax = new Vector2(1, 1);
        descRect.anchoredPosition = new Vector2(0, -130);
        descRect.sizeDelta = new Vector2(-20, 50);
        Text descriptionText = descGo.GetComponent<Text>();
        descriptionText.text = "Description";
        descriptionText.alignment = TextAnchor.UpperLeft;
        descriptionText.color = Color.white;
        descriptionText.fontSize = 13;

        // funFactText
        GameObject funFactGo = DefaultControls.CreateText(uiResources);
        funFactGo.name = "FunFactText";
        funFactGo.transform.SetParent(resultPanelGo.transform, false);
        var funFactRect = funFactGo.GetComponent<RectTransform>();
        funFactRect.anchorMin = new Vector2(0, 1);
        funFactRect.anchorMax = new Vector2(1, 1);
        funFactRect.anchoredPosition = new Vector2(0, -180);
        funFactRect.sizeDelta = new Vector2(-20, 50);
        Text funFactText = funFactGo.GetComponent<Text>();
        funFactText.text = "Fun Fact";
        funFactText.alignment = TextAnchor.UpperLeft;
        funFactText.color = new Color(0.2f, 0.6f, 1f); // Soft Blue
        funFactText.fontSize = 13;

        // edibilityText
        GameObject edibilityGo = DefaultControls.CreateText(uiResources);
        edibilityGo.name = "EdibilityText";
        edibilityGo.transform.SetParent(resultPanelGo.transform, false);
        var edibilityRect = edibilityGo.GetComponent<RectTransform>();
        edibilityRect.anchorMin = new Vector2(0, 1);
        edibilityRect.anchorMax = new Vector2(1, 1);
        edibilityRect.anchoredPosition = new Vector2(0, -220);
        edibilityRect.sizeDelta = new Vector2(-20, 30);
        Text edibilityText = edibilityGo.GetComponent<Text>();
        edibilityText.text = "Edibility";
        edibilityText.alignment = TextAnchor.MiddleCenter;
        edibilityText.color = new Color(0.9f, 0.5f, 0.5f); // Soft Red/Pink
        edibilityText.fontSize = 13;

        // Assign proper legacy fonts to all Text elements
        AssignDefaultFonts(canvasGo);

        // 9. Create AppManager Empty GameObject
        GameObject appManagerGo = new GameObject("AppManager");
        Undo.RegisterCreatedObjectUndo(appManagerGo, "Create AppManager");

        // 10. Add BackendClient and UIManager components
        BackendClient backendClient = appManagerGo.AddComponent<BackendClient>();
        UIManager uiManager = appManagerGo.AddComponent<UIManager>();

        // 11. Connect fields in UIManager using SerializedObject
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
        
        Debug.Log("UI structure successfully created and connected to UIManager in SampleScene!");
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
