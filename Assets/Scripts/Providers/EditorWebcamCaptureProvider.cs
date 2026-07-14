using System;
using UnityEngine;
using UnityEngine.UI;

namespace ARHerb.Camera
{
    /// <summary>
    /// Editor / PC Fallback Camera Provider using Unity's WebCamTexture.
    /// Used when testing in the Unity Editor or running standalone on PC.
    /// </summary>
    public class EditorWebcamCaptureProvider : MonoBehaviour, ICameraCaptureProvider
    {
        private WebCamTexture webcamTexture;
        private RawImage previewUI;

        public void Initialize(RawImage previewTarget)
        {
            previewUI = previewTarget;
            
            if (WebCamTexture.devices.Length > 0)
            {
                // Attempt to request standard 720p resolution
                webcamTexture = new WebCamTexture(1280, 720);
                
                if (previewUI != null)
                {
                    previewUI.texture = webcamTexture;
                    previewUI.gameObject.SetActive(true);
                }
                
                webcamTexture.Play();
                Debug.Log("[WebcamCapture] WebCamTexture initialized and playing.");
            }
            else
            {
                Debug.LogError("[WebcamCapture] No physical camera/webcam devices found on this system.");
            }
        }

        public void CaptureFrame(Action<byte[]> onCaptured)
        {
            if (webcamTexture == null || !webcamTexture.isPlaying)
            {
                Debug.LogError("[WebcamCapture] Cannot capture: WebCamTexture is not active or initialized.");
                onCaptured?.Invoke(null);
                return;
            }

            try
            {
                // Create a new Texture2D copy of the current camera buffer pixels
                Texture2D photo = new Texture2D(webcamTexture.width, webcamTexture.height, TextureFormat.RGB24, false);
                photo.SetPixels(webcamTexture.GetPixels());
                photo.Apply();

                // Encode texture copy to JPEG format
                byte[] jpegBytes = photo.EncodeToJPG(85);
                
                // Destroy local texture to prevent memory accumulation
                Destroy(photo);

                Debug.Log($"[WebcamCapture] Frame captured successfully ({jpegBytes.Length} bytes).");
                onCaptured?.Invoke(jpegBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebcamCapture] Error capturing frame: {ex.Message}");
                onCaptured?.Invoke(null);
            }
        }

        public void Release()
        {
            if (webcamTexture != null)
            {
                if (webcamTexture.isPlaying)
                {
                    webcamTexture.Stop();
                }
                webcamTexture = null;
            }
            
            if (previewUI != null)
            {
                previewUI.texture = null;
                previewUI.gameObject.SetActive(false);
            }
            
            Debug.Log("[WebcamCapture] WebCamTexture resources released.");
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}
