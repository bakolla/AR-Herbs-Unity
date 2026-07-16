using UnityEngine;

namespace ARHerb.UI
{
    /// <summary>
    /// Adjusts the RectTransform anchors to match the device safe area dynamically,
    /// protecting the UI from being clipped by notches, hole punches, or navigation bars.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaHandler : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2 lastScreenSize = new Vector2(0, 0);
        private ScreenOrientation lastOrientation = ScreenOrientation.Unknown;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            Rect safeArea = Screen.safeArea;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            ScreenOrientation orientation = Screen.orientation;

            if (safeArea != lastSafeArea || screenSize != lastScreenSize || orientation != lastOrientation)
            {
                lastSafeArea = safeArea;
                lastScreenSize = screenSize;
                lastOrientation = orientation;
                ApplySafeArea(safeArea);
            }
        }

        private void ApplySafeArea(Rect r)
        {
            if (rectTransform == null) return;

            // Convert safe area rectangle from pixels to normalized anchors
            Vector2 anchorMin = r.position;
            Vector2 anchorMax = r.position + r.size;

            float screenW = Screen.width;
            float screenH = Screen.height;

            if (screenW <= 0 || screenH <= 0) return;

            anchorMin.x /= screenW;
            anchorMin.y /= screenH;
            anchorMax.x /= screenW;
            anchorMax.y /= screenH;

            // Prevent negative values or out of bounds
            anchorMin.x = Mathf.Clamp01(anchorMin.x);
            anchorMin.y = Mathf.Clamp01(anchorMin.y);
            anchorMax.x = Mathf.Clamp01(anchorMax.x);
            anchorMax.y = Mathf.Clamp01(anchorMax.y);

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
