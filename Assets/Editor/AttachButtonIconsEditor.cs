using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Editor tool: AR Herb → Attach Button Icons
/// Finds all UI buttons in the open TE_UI scene and attaches icon sprites
/// from Assets/Icons/ directly as child Image objects in the scene hierarchy.
/// Icons are visible in Edit Mode permanently (not just at Play time).
/// </summary>
public class AttachButtonIconsEditor
{
    private static readonly Dictionary<string, string> ButtonIconMap = new Dictionary<string, string>
    {
        { "ScanButton",         "scan"         },
        { "GalleryButton",      "gallery"      },
        { "FlashlightButton",   "blyskawica"   },
        { "SwitchCameraButton", "kamera"       },
        { "HistoryButton",      "time"         },
        { "LanguageButton",     "jezyk"        },
        { "LanguageDropdown",   "jezyk"        },
        { "SettingsButton",     "settings2"    },
        { "CameraDropdown",     "chose_camera" },
        { "CameraSelectButton", "chose_camera" },
    };

    [MenuItem("AR Herb/Attach Button Icons")]
    public static void AttachIcons()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.name.Equals("TE_UI", System.StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Wrong Scene",
                "Please open Assets/Scenes/TE_UI.unity first, then run Attach Button Icons.",
                "OK");
            return;
        }

        int attached = 0;
        int skipped  = 0;

        Button[] allButtons = Object.FindObjectsOfType<Button>(true);

        foreach (Button btn in allButtons)
        {
            string btnName = btn.gameObject.name;
            if (!ButtonIconMap.TryGetValue(btnName, out string iconName))
                continue;

            string assetPath = "Assets/Icons/" + iconName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

            if (sprite == null)
            {
                Debug.LogWarning("[AttachButtonIcons] Sprite not found: " + assetPath);
                skipped++;
                continue;
            }

            // Update existing Icon child if present
            Transform existing = btn.transform.Find("Icon");
            if (existing != null)
            {
                Image img = existing.GetComponent<Image>();
                if (img != null)
                {
                    Undo.RecordObject(img, "Update Button Icon");
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.preserveAspect = true;
                    EditorUtility.SetDirty(img);
                }
                attached++;
                Debug.Log("[AttachButtonIcons] Updated icon on '" + btnName + "' to " + iconName + ".png");
                continue;
            }

            // Create new centered Icon child
            GameObject iconGo = new GameObject("Icon");
            Undo.RegisterCreatedObjectUndo(iconGo, "Add Button Icon");
            iconGo.transform.SetParent(btn.transform, false);

            RectTransform rt = iconGo.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.1f, 0.1f);
            rt.anchorMax        = new Vector2(0.9f, 0.9f);
            rt.offsetMin        = Vector2.zero;
            rt.offsetMax        = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;

            iconGo.AddComponent<CanvasRenderer>();
            Image image = iconGo.AddComponent<Image>();
            image.sprite         = sprite;
            image.color          = Color.white;
            image.preserveAspect = true;
            image.raycastTarget  = false;

            iconGo.transform.SetAsFirstSibling();

            EditorUtility.SetDirty(btn.gameObject);
            attached++;
            Debug.Log("[AttachButtonIcons] Attached '" + iconName + ".png' to '" + btnName + "'");
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        EditorUtility.DisplayDialog(
            "Attach Button Icons - Done",
            "Icons attached: " + attached + "\nSkipped (not found): " + skipped + "\n\nScene saved.",
            "OK");
    }
}
