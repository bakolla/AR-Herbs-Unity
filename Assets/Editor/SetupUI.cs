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
        string[] oldNames = { "Canvas", "AppManager", "EventSystem", "AR Session", "XR Origin", "AR Session Origin" };
        foreach (var name in oldNames)
        {
            GameObject oldGo = GameObject.Find(name);
            if (oldGo != null) Undo.DestroyObjectImmediate(oldGo);
        }

        // 2. Create AR Foundation Hierarchy safely using reflection
        CreateARFoundationObjects();

        // 3. Create Canvas and UI elements
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");

        // EventSystem
        GameObject eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");

        // UI Camera Preview
        GameObject previewGo = new GameObject("CameraPreviewUI", typeof(RawImage));
        previewGo.transform.SetParent(canvasGo.transform, false);
        RawImage preview = previewGo.GetComponent<RawImage>();
        RectTransform previewRect = preview.rectTransform;
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.sizeDelta = Vector2.zero;
        preview.color = Color.black;

        // Top Header
        GameObject topBarGo = CreatePanel(canvasGo.transform, "TopBar", new Vector2(0f, 0.92f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Color(0.04f, 0.05f, 0.06f, 0.85f));
        
        GameObject titleGo = CreateText(topBarGo.transform, "TitleText", "🌿 HERB & FAUNA SCANNER", 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.sizeDelta = Vector2.zero;

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
        GameObject dropdownGo = new GameObject("ModeDropdown", typeof(Image), typeof(Dropdown));
        dropdownGo.transform.SetParent(canvasGo.transform, false);
        RectTransform ddRect = dropdownGo.GetComponent<RectTransform>();
        ddRect.anchorMin = new Vector2(0.2f, 0.18f);
        ddRect.anchorMax = new Vector2(0.8f, 0.23f);
        ddRect.sizeDelta = Vector2.zero;
        dropdownGo.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 0.95f);

        GameObject ddLabelGo = CreateText(dropdownGo.transform, "Label", "Plants", 18, TextAnchor.MiddleCenter, Color.white);
        RectTransform ddLabelRect = ddLabelGo.GetComponent<RectTransform>();
        ddLabelRect.anchorMin = Vector2.zero;
        ddLabelRect.anchorMax = Vector2.one;
        ddLabelRect.sizeDelta = Vector2.zero;

        Dropdown dropdown = dropdownGo.GetComponent<Dropdown>();
        dropdown.captionText = ddLabelGo.GetComponent<Text>();
        dropdown.options = new List<Dropdown.OptionData>
        {
            new Dropdown.OptionData("Plants"),
            new Dropdown.OptionData("Mushrooms"),
            new Dropdown.OptionData("Insects"),
            new Dropdown.OptionData("Stones")
        };

        // Scan Button (Circular bottom shutter)
        GameObject btnGo = new GameObject("ScanButton", typeof(Image), typeof(Button));
        btnGo.transform.SetParent(canvasGo.transform, false);
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

        // Status Message Text (centered above dropdown)
        GameObject statusGo = CreateText(canvasGo.transform, "StatusText", "Gotowy do skanowania", 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        RectTransform statusRect = statusGo.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.1f, 0.25f);
        statusRect.anchorMax = new Vector2(0.9f, 0.29f);
        statusRect.sizeDelta = Vector2.zero;
        Text statusText = statusGo.GetComponent<Text>();
        // Subtle background for status
        statusGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);

        // 4. Result Panel (Bottom Sheet style card)
        GameObject resPanelGo = CreatePanel(canvasGo.transform, "ResultPanel", new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.85f), new Vector2(0f, 0f), new Color(0.08f, 0.09f, 0.12f, 0.95f));
        
        // Handle Bar
        GameObject handleGo = CreatePanel(resPanelGo.transform, "HandleBar", new Vector2(0.42f, 0.96f), new Vector2(0.58f, 0.98f), new Vector2(0f, 0f), new Color(0.3f, 0.3f, 0.3f, 0.8f));
        handleGo.AddComponent<Image>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

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
        descRect.anchorMax = new Vector2(0.95f, 0.72f);
        descRect.sizeDelta = Vector2.zero;

        // Fun Fact
        GameObject factGo = CreateText(resPanelGo.transform, "FunFactText", "Ciekawostka...", 14, TextAnchor.UpperLeft, new Vector2(0.5f, 0.8f, 1f));
        RectTransform factRect = factGo.GetComponent<RectTransform>();
        factRect.anchorMin = new Vector2(0.05f, 0.16f);
        factRect.anchorMax = new Vector2(0.95f, 0.35f);
        factRect.sizeDelta = Vector2.zero;

        // Edibility status pill
        GameObject ediblePanelGo = CreatePanel(resPanelGo.transform, "EdibilityPanel", new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.13f), new Vector2(0f, 0f), new Color(0.15f, 0.2f, 0.15f, 0.9f));
        GameObject edibGo = CreateText(ediblePanelGo.transform, "EdibilityText", "Status spożywczy: Brak danych", 14, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        RectTransform edibRect = edibGo.GetComponent<RectTransform>();
        edibRect.anchorMin = Vector2.zero;
        edibRect.anchorMax = Vector2.one;
        edibRect.sizeDelta = Vector2.zero;

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
        SetRef(uiManager, "resultPanel", resPanelGo);
        SetRef(uiManager, "commonNameText", commGo.GetComponent<Text>());
        SetRef(uiManager, "scientificNameText", sciGo.GetComponent<Text>());
        SetRef(uiManager, "scoreText", scoreGo.GetComponent<Text>());
        SetRef(uiManager, "descriptionText", descGo.GetComponent<Text>());
        SetRef(uiManager, "funFactText", factGo.GetComponent<Text>());
        SetRef(uiManager, "edibilityText", edibGo.GetComponent<Text>());
        SetRef(uiManager, "statusMessageText", statusText);
        SetRef(uiManager, "scanButton", scanButton);

        // Mark scene dirty and save it
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[SetupUI] Successfully generated MainARScene at '{scenePath}'. AppManager is completely configured.");
        EditorUtility.DisplayDialog("AR Herb Setup", "Pomyślnie wygenerowano scenę MainARScene!\nWszystkie powiązania interfejsu zostały automatycznie podpięte pod AppManager.", "OK");
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
        t.verticalOverflow = VerticalWrapMode.Truncate;
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

    private static void CreateARFoundationObjects()
    {
        // 1. Create AR Session if package is available
        System.Type arSessionType = System.Type.GetType("UnityEngine.XR.ARFoundation.ARSession, Unity.XR.ARFoundation");
        System.Type arInputManagerType = System.Type.GetType("UnityEngine.XR.ARFoundation.ARInputManager, Unity.XR.ARFoundation");
        
        if (arSessionType != null)
        {
            GameObject arSessionGo = new GameObject("AR Session");
            arSessionGo.AddComponent(arSessionType);
            if (arInputManagerType != null)
            {
                arSessionGo.AddComponent(arInputManagerType);
            }
            Undo.RegisterCreatedObjectUndo(arSessionGo, "Create AR Session");
            Debug.Log("[SetupUI] Created AR Session.");
        }

        // 2. Create XR Origin if package is available
        System.Type xrOriginType = System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
        System.Type arCameraManagerType = System.Type.GetType("UnityEngine.XR.ARFoundation.ARCameraManager, Unity.XR.ARFoundation");
        System.Type arCameraBackgroundType = System.Type.GetType("UnityEngine.XR.ARFoundation.ARCameraBackground, Unity.XR.ARFoundation");

        if (xrOriginType != null)
        {
            GameObject xrOriginGo = new GameObject("XR Origin");
            xrOriginGo.AddComponent(xrOriginType);
            Undo.RegisterCreatedObjectUndo(xrOriginGo, "Create XR Origin");

            GameObject cameraOffsetGo = new GameObject("Camera Offset");
            cameraOffsetGo.transform.SetParent(xrOriginGo.transform, false);

            GameObject arCameraGo = new GameObject("Main Camera", typeof(Camera));
            arCameraGo.transform.SetParent(cameraOffsetGo.transform, false);
            arCameraGo.tag = "MainCamera";

            if (arCameraManagerType != null) arCameraGo.AddComponent(arCameraManagerType);
            if (arCameraBackgroundType != null) arCameraGo.AddComponent(arCameraBackgroundType);

            var xrOrigin = xrOriginGo.GetComponent(xrOriginType);
            var offsetProp = xrOriginType.GetProperty("CameraFloorOffsetObject");
            var cameraProp = xrOriginType.GetProperty("Camera");

            if (offsetProp != null) offsetProp.SetValue(xrOrigin, cameraOffsetGo);
            if (cameraProp != null) cameraProp.SetValue(xrOrigin, arCameraGo.GetComponent<Camera>());

            Debug.Log("[SetupUI] Created XR Origin (Mobile AR) structure.");
        }
        else
        {
            // Try fallback ARSessionOrigin
            System.Type arSessionOriginType = System.Type.GetType("UnityEngine.XR.ARFoundation.ARSessionOrigin, Unity.XR.ARFoundation");
            if (arSessionOriginType != null)
            {
                GameObject arOriginGo = new GameObject("AR Session Origin");
                arOriginGo.AddComponent(arSessionOriginType);
                Undo.RegisterCreatedObjectUndo(arOriginGo, "Create AR Session Origin");

                GameObject arCameraGo = new GameObject("AR Camera", typeof(Camera));
                arCameraGo.transform.SetParent(arOriginGo.transform, false);
                arCameraGo.tag = "MainCamera";

                if (arCameraManagerType != null) arCameraGo.AddComponent(arCameraManagerType);
                if (arCameraBackgroundType != null) arCameraGo.AddComponent(arCameraBackgroundType);

                var arOrigin = arOriginGo.GetComponent(arSessionOriginType);
                var cameraProp = arSessionOriginType.GetProperty("camera");
                if (cameraProp != null) cameraProp.SetValue(arOrigin, arCameraGo.GetComponent<Camera>());

                Debug.Log("[SetupUI] Created AR Session Origin (Mobile AR) fallback.");
            }
            else
            {
                // Create a standard PC Camera as fallback
                GameObject pcCamGo = new GameObject("Main Camera", typeof(Camera));
                pcCamGo.tag = "MainCamera";
                Undo.RegisterCreatedObjectUndo(pcCamGo, "Create PC Main Camera");
                Debug.Log("[SetupUI] AR Foundation package not detected. Created default Main Camera for PC testing.");
            }
        }
    }
}
