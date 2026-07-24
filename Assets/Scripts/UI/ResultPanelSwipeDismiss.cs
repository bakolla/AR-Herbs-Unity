using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ARHerb.UI
{
    /// <summary>
    /// Implements drag-down swipe to dismiss for the main ResultPanel.
    /// Allows users to swipe the result card downwards off the screen to close it.
    /// </summary>
    public class ResultPanelSwipeDismiss : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public System.Action OnDismissed;

        private RectTransform rectTransform;
        private Vector2 dragStartPointerPos;
        private bool isDragging = false;
        private const float DISMISS_THRESHOLD_Y = -180f;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.zero;
            }
            isDragging = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = true;
            dragStartPointerPos = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || rectTransform == null) return;

            float deltaY = eventData.position.y - dragStartPointerPos.y;
            // Only allow dragging downwards (negative deltaY)
            if (deltaY < 0)
            {
                rectTransform.anchoredPosition = new Vector2(0f, deltaY);
            }
            else
            {
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || rectTransform == null) return;
            isDragging = false;

            float currentY = rectTransform.anchoredPosition.y;

            // Check if dragged down past threshold or fast flick downwards
            if (currentY < DISMISS_THRESHOLD_Y || eventData.delta.y < -12f)
            {
                // Swipe down dismiss triggered!
                rectTransform.anchoredPosition = Vector2.zero;
                gameObject.SetActive(false);
                OnDismissed?.Invoke();
            }
            else
            {
                // Snap back to top (0,0)
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }
}
