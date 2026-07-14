using System;
using UnityEngine;
using UnityEngine.UI;

namespace ARHerb.Camera
{
    /// <summary>
    /// Fallback provider that returns a preselected mock test image.
    /// Useful for testing in the Unity Editor if no webcam is connected.
    /// </summary>
    public class TestImageCaptureProvider : MonoBehaviour, ICameraCaptureProvider
    {
        [Header("Mock Asset Settings")]
        [Tooltip("Drag a Texture2D asset here to serve as the mock camera feed.")]
        [SerializeField] private Texture2D testImage;
        
        private RawImage previewUI;

        public void Initialize(RawImage previewTarget)
        {
            previewUI = previewTarget;
            
            if (previewUI != null)
            {
                if (testImage != null)
                {
                    previewUI.texture = testImage;
                    previewUI.gameObject.SetActive(true);
                    Debug.Log("[TestImageCapture] Initialized with test image asset.");
                }
                else
                {
                    previewUI.gameObject.SetActive(false);
                    Debug.LogWarning("[TestImageCapture] No test image asset assigned to the provider.");
                }
            }
        }

        public void CaptureFrame(Action<byte[]> onCaptured)
        {
            if (testImage == null)
            {
                Debug.LogError("[TestImageCapture] Cannot capture: no test image asset assigned.");
                onCaptured?.Invoke(null);
                return;
            }

            try
            {
                // Create a temporary readable Texture2D to extract JPG bytes
                // Since testImage might not be marked as readable in import settings, we copy it using a RenderTexture
                RenderTexture tempRT = RenderTexture.GetTemporary(
                    testImage.width,
                    testImage.height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Linear
                );

                Graphics.Blit(testImage, tempRT);
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = tempRT;

                Texture2D readableCopy = new Texture2D(testImage.width, testImage.height, TextureFormat.RGB24, false);
                readableCopy.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
                readableCopy.Apply();

                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(tempRT);

                byte[] jpegBytes = readableCopy.EncodeToJPG(85);
                Destroy(readableCopy);

                Debug.Log($"[TestImageCapture] Captured frame from test image asset ({jpegBytes.Length} bytes).");
                onCaptured?.Invoke(jpegBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TestImageCapture] Error copying test image: {ex.Message}");
                onCaptured?.Invoke(null);
            }
        }

        public void Release()
        {
            if (previewUI != null)
            {
                previewUI.texture = null;
                previewUI.gameObject.SetActive(false);
            }
            Debug.Log("[TestImageCapture] Test image resources released.");
        }
    }
}
