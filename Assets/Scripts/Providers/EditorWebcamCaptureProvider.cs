using System;
using UnityEngine;
using UnityEngine.UI;

namespace ARHerb.Camera
{
    /// <summary>
    /// Editor / PC Fallback Camera Provider using Unity's WebCamTexture.
    /// If no physical webcam is connected, it automatically generates a mock texture fallback.
    /// </summary>
    public class EditorWebcamCaptureProvider : MonoBehaviour, ICameraCaptureProvider
    {
        private WebCamTexture webcamTexture;
        private Texture2D mockTexture;
        private RawImage previewUI;
        private float currentZoomFactor = 1.0f;

        public void SetZoom(float zoomFactor)
        {
            currentZoomFactor = Mathf.Clamp(zoomFactor, 1.0f, 3.0f);
            Debug.Log($"[Zoom] Current level = {currentZoomFactor:F2}x");
            AdjustPreviewOrientation();
            if (mockTexture != null) AdjustPreviewOrientationMock();
        }

        public float GetZoom()
        {
            return currentZoomFactor;
        }

        public bool IsFrontCamera()
        {
            return false;
        }

        public void SwitchCamera()
        {
            Debug.Log("[EditorWebcam] SwitchCamera called in Editor.");
        }

        public string[] GetAvailableCameraDevices()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0) return new string[] { "Kamera Editor (Mock)" };
            string[] names = new string[devices.Length];
            for (int i = 0; i < devices.Length; i++)
            {
                names[i] = $"📷 Kamera {i + 1} ({devices[i].name})";
            }
            return names;
        }

        public int GetCurrentCameraDeviceIndex()
        {
            return 0;
        }

        public void SelectCameraDevice(int deviceIndex)
        {
            Debug.Log($"[EditorWebcam] SelectCameraDevice({deviceIndex}) called");
        }

        public void Initialize(RawImage previewTarget)
        {
            previewUI = previewTarget;
            currentZoomFactor = 1.0f;
            Debug.Log("[Zoom] Zoom reset after camera change");
            Debug.Log($"[Zoom] Current level = {currentZoomFactor:F2}x");
            
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
                Debug.LogWarning("[WebcamCapture] No physical camera/webcam devices found on this system. Creating runtime mock texture fallback.");
                
                // Create a nice placeholder mock texture (green gradient with a leaf pattern/color)
                mockTexture = CreateMockTexture();
                
                if (previewUI != null)
                {
                    previewUI.texture = mockTexture;
                    previewUI.gameObject.SetActive(true);
                }
            }
        }

        private void Update()
        {
            if (webcamTexture != null && webcamTexture.isPlaying && webcamTexture.didUpdateThisFrame)
            {
                AdjustPreviewOrientation();
            }
            else if (mockTexture != null && previewUI != null && previewUI.gameObject.activeSelf)
            {
                AdjustPreviewOrientationMock();
            }
        }

        private void AdjustPreviewOrientation()
        {
            if (webcamTexture == null || previewUI == null) return;

            RectTransform rect = previewUI.rectTransform;
            
            float screenW = Screen.width;
            float screenH = Screen.height;
            
            if (screenW <= 0 || screenH <= 0) return;

            float texW = webcamTexture.width;
            float texH = webcamTexture.height;
            
            if (texW <= 16 || texH <= 16) return;

            int rotationAngle = webcamTexture.videoRotationAngle;
            bool isRotated = (rotationAngle == 90 || rotationAngle == 270);

            float uWidth = 1.0f / currentZoomFactor;
            float uHeight = 1.0f / currentZoomFactor;
            float uX = (1.0f - uWidth) * 0.5f;
            float uY = (1.0f - uHeight) * 0.5f;

            previewUI.uvRect = new Rect(uX, uY, uWidth, uHeight);

            float targetWidth;
            float targetHeight;

            if (isRotated)
            {
                float videoAspect = texW / texH;
                float h = screenW;
                float w = h * videoAspect;
                
                if (w < screenH)
                {
                    w = screenH;
                    h = w / videoAspect;
                }
                
                targetWidth = w;
                targetHeight = h;
            }
            else
            {
                float videoAspect = texW / texH;
                float w = screenW;
                float h = w / videoAspect;
                
                if (h < screenH)
                {
                    h = screenH;
                    w = h * videoAspect;
                }
                
                targetWidth = w;
                targetHeight = h;
            }

            float mirrorY = webcamTexture.videoVerticallyMirrored ? -1f : 1f;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(targetWidth, targetHeight);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0, 0, -rotationAngle);
            rect.localScale = new Vector3(1f, mirrorY, 1f);
        }

        private void AdjustPreviewOrientationMock()
        {
            if (mockTexture == null || previewUI == null) return;

            RectTransform rect = previewUI.rectTransform;
            
            float screenW = Screen.width;
            float screenH = Screen.height;
            
            if (screenW <= 0 || screenH <= 0) return;

            float texW = mockTexture.width;
            float texH = mockTexture.height;

            previewUI.uvRect = new Rect(0, 0, 1, 1);

            float videoAspect = texW / texH;
            float w = screenW;
            float h = w / videoAspect;
            
            if (h < screenH)
            {
                h = screenH;
                w = h * videoAspect;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private Texture2D CreateMockTexture()
        {
            int width = 640;
            int height = 480;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            
            // Draw a premium gradient
            Color centerColor = new Color(0.12f, 0.45f, 0.22f); // Forest Green
            Color cornerColor = new Color(0.08f, 0.25f, 0.14f); // Dark Green
            
            for (int y = 0; y < height; y++)
            {
                float tY = (float)y / height;
                for (int x = 0; x < width; x++)
                {
                    float tX = (float)x / width;
                    
                    // Radial gradient from center
                    float dist = Mathf.Sqrt((tX - 0.5f) * (tX - 0.5f) + (tY - 0.5f) * (tY - 0.5f)) * 1.4f;
                    dist = Mathf.Clamp01(dist);
                    Color c = Color.Lerp(centerColor, cornerColor, dist);
                    
                    // Add some simple plant/leaf diagonal vein lines
                    if (Mathf.Abs((x - y) % 60) < 3 || Mathf.Abs((x + y - width) % 80) < 3)
                    {
                        c = Color.Lerp(c, new Color(0.18f, 0.6f, 0.3f), 0.3f);
                    }
                    
                    tex.SetPixel(x, y, c);
                }
            }
            
            // Add a mockup plant shape or frame in the center
            DrawCameraFrame(tex);
            
            tex.Apply();
            return tex;
        }

        private void DrawCameraFrame(Texture2D tex)
        {
            int w = tex.width;
            int h = tex.height;
            Color frameColor = new Color(1f, 1f, 1f, 0.6f);
            
            // Draw corner guides
            int border = 40;
            int len = 30;
            int thickness = 3;
            
            for (int t = 0; t < thickness; t++)
            {
                // Top Left
                DrawLine(tex, border, border + t, border + len, border + t, frameColor);
                DrawLine(tex, border + t, border, border + t, border + len, frameColor);
                
                // Top Right
                DrawLine(tex, w - border - len, border + t, w - border, border + t, frameColor);
                DrawLine(tex, w - border - t, border, w - border - t, border + len, frameColor);
                
                // Bottom Left
                DrawLine(tex, border, h - border - t, border + len, h - border - t, frameColor);
                DrawLine(tex, border + t, h - border - len, border + t, h - border, frameColor);
                
                // Bottom Right
                DrawLine(tex, w - border - len, h - border - t, w - border, h - border - t, frameColor);
                DrawLine(tex, w - border - t, h - border - len, w - border - t, h - border, frameColor);
            }
        }

        private void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = (dx > dy ? dx : -dy) / 2, e2;
            
            while (true)
            {
                if (x0 >= 0 && x0 < tex.width && y0 >= 0 && y0 < tex.height)
                {
                    tex.SetPixel(x0, y0, col);
                }
                if (x0 == x1 && y0 == y1) break;
                e2 = err;
                if (e2 > -dx) { err -= dy; x0 += sx; }
                if (e2 < dy) { err += dx; y0 += sy; }
            }
        }

        public void CaptureFrame(Action<byte[]> onCaptured)
        {
            if (webcamTexture == null && mockTexture == null)
            {
                Debug.LogError("[WebcamCapture] Cannot capture: neither WebCamTexture nor mock texture is initialized.");
                onCaptured?.Invoke(null);
                return;
            }

            try
            {
                Texture2D photo;
                if (webcamTexture != null && webcamTexture.isPlaying)
                {
                    photo = new Texture2D(webcamTexture.width, webcamTexture.height, TextureFormat.RGB24, false);
                    photo.SetPixels(webcamTexture.GetPixels());
                    photo.Apply();
                }
                else
                {
                    // Copy our mock texture
                    photo = new Texture2D(mockTexture.width, mockTexture.height, TextureFormat.RGB24, false);
                    photo.SetPixels(mockTexture.GetPixels());
                    photo.Apply();
                }

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
            
            if (mockTexture != null)
            {
                Destroy(mockTexture);
                mockTexture = null;
            }
            
            if (previewUI != null)
            {
                previewUI.texture = null;
                previewUI.gameObject.SetActive(false);
            }
            
            Debug.Log("[WebcamCapture] Fallback capture resources released.");
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}
