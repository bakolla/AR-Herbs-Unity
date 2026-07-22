using System;
using UnityEngine;
using UnityEngine.UI;

namespace ARHerb.Camera
{
    /// <summary>
    /// Android mobile fallback camera provider using WebCamTexture.
    /// Selects the rear-facing camera if possible and streams it to a fullscreen RawImage.
    /// </summary>
    public class MobileWebcamCaptureProvider : MonoBehaviour, ICameraCaptureProvider
    {
        private WebCamTexture webcamTexture;
        private RawImage previewUI;
        private bool isInitialized = false;
        private float targetZoomFactor = 1.0f;
        private float currentZoomFactor = 1.0f;
        private float lastLoggedZoom = 1.0f;

        public event Action<float> OnZoomChanged;

        public void SetZoom(float zoomFactor)
        {
            targetZoomFactor = Mathf.Clamp(zoomFactor, 1.0f, 3.0f);
            currentZoomFactor = targetZoomFactor;
            UpdatePreviewUVRect();
            OnZoomChanged?.Invoke(currentZoomFactor);
            Debug.Log($"[Zoom] Current level = {currentZoomFactor:F2}x");
        }

        public float GetZoom()
        {
            return targetZoomFactor;
        }

        private bool isUsingFrontCamera = false;
        private int currentDeviceIndex = 0;

        public bool IsFrontCamera()
        {
            return isUsingFrontCamera;
        }

        public string[] GetAvailableCameraDevices()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                Debug.Log("[Zoom] No WebCamTexture camera devices detected on device.");
                return new string[] { "No camera detected" };
            }

            Debug.Log($"[Zoom] Refreshing camera device list. Found {devices.Length} device(s):");
            string[] names = new string[devices.Length];
            int rearCount = 0;
            int frontCount = 0;

            for (int i = 0; i < devices.Length; i++)
            {
                WebCamDevice dev = devices[i];
                Debug.Log($"[Zoom] Detected camera index={i}, name='{dev.name}', isFrontFacing={dev.isFrontFacing}");

                string lowerName = dev.name.ToLower();
                bool hasWide = lowerName.Contains("wide");
                bool hasTele = lowerName.Contains("tele");

                if (dev.isFrontFacing)
                {
                    frontCount++;
                    if (hasWide) names[i] = $"Front Camera {frontCount} (Wide)";
                    else if (hasTele) names[i] = $"Front Camera {frontCount} (Tele)";
                    else names[i] = $"Front Camera {frontCount}";
                }
                else
                {
                    rearCount++;
                    if (hasWide) names[i] = $"Rear Camera {rearCount} (Wide)";
                    else if (hasTele) names[i] = $"Rear Camera {rearCount} (Tele)";
                    else names[i] = $"Rear Camera {rearCount}";
                }
            }
            return names;
        }

        public int GetCurrentCameraDeviceIndex()
        {
            return currentDeviceIndex;
        }

        public void SelectCameraDevice(int deviceIndex)
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0) return;

            if (deviceIndex < 0 || deviceIndex >= devices.Length)
            {
                deviceIndex = 0;
            }

            currentDeviceIndex = deviceIndex;
            WebCamDevice selectedDevice = devices[deviceIndex];
            isUsingFrontCamera = selectedDevice.isFrontFacing;

            Debug.Log($"[Zoom] Selected camera index {deviceIndex}: name='{selectedDevice.name}', isFrontFacing={isUsingFrontCamera}");

            if (webcamTexture != null)
            {
                if (webcamTexture.isPlaying)
                {
                    webcamTexture.Stop();
                }
                webcamTexture = null;
            }

            targetZoomFactor = 1.0f;
            currentZoomFactor = 1.0f;
            lastLoggedZoom = 1.0f;
            Debug.Log("[Zoom] Zoom reset after camera change");

            webcamTexture = new WebCamTexture(selectedDevice.name, 1280, 720, 30);

            if (previewUI != null)
            {
                previewUI.texture = webcamTexture;
                previewUI.color = Color.white;
                previewUI.gameObject.SetActive(true);
            }

            webcamTexture.Play();
            isInitialized = true;

            // Wait for WebCamTexture initialization before updating aspect ratio and orientation
            StopAllCoroutines();
            StartCoroutine(WaitForWebCamInit());

            OnZoomChanged?.Invoke(currentZoomFactor);
        }

        private System.Collections.IEnumerator WaitForWebCamInit()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (webcamTexture != null && (!webcamTexture.isPlaying || webcamTexture.width <= 16) && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (webcamTexture != null && webcamTexture.isPlaying && webcamTexture.width > 16)
            {
                Debug.Log($"[Zoom] WebCamTexture initialized successfully ({webcamTexture.width}x{webcamTexture.height}, angle={webcamTexture.videoRotationAngle}). Updating orientation/aspect.");
                AdjustPreviewOrientation();
            }
            else
            {
                Debug.LogWarning("[Zoom] WebCamTexture init timeout or invalid resolution.");
            }
        }

        public void SwitchCamera()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0) return;

            int nextIndex = (currentDeviceIndex + 1) % devices.Length;
            SelectCameraDevice(nextIndex);
        }

        public void Initialize(RawImage previewTarget)
        {
            previewUI = previewTarget;

            // Check Android camera permission
            bool permissionGranted = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
            Debug.Log($"Android MVP mode: camera permission status = {(permissionGranted ? "GRANTED" : "DENIED")}");

            if (!permissionGranted)
            {
                Debug.LogError("[MobileWebcam] Camera permission is not granted. Cannot initialize WebCamTexture.");
                return;
            }

            // Find first rear camera as default
            WebCamDevice[] devices = WebCamTexture.devices;
            int defaultIdx = 0;
            if (devices != null && devices.Length > 0)
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    if (!devices[i].isFrontFacing)
                    {
                        defaultIdx = i;
                        break;
                    }
                }
            }

            SelectCameraDevice(defaultIdx);
        }

        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;

        private void Update()
        {
            HandlePinchZoom();

            // Fast snappy lerp for button preset transitions
            if (Mathf.Abs(currentZoomFactor - targetZoomFactor) > 0.001f)
            {
                currentZoomFactor = Mathf.Lerp(currentZoomFactor, targetZoomFactor, Time.deltaTime * 45f);
                UpdatePreviewUVRect();
                OnZoomChanged?.Invoke(currentZoomFactor);
            }

            // Re-adjust layout only if screen resolution/orientation actually changes
            if (isInitialized && webcamTexture != null && webcamTexture.isPlaying)
            {
                if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
                {
                    lastScreenWidth = Screen.width;
                    lastScreenHeight = Screen.height;
                    AdjustPreviewOrientation();
                }
            }
        }

        private void HandlePinchZoom()
        {
            // Touch Pinch-to-zoom gesture on mobile devices
            if (Input.touchCount == 2)
            {
                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                // Skip initial frame of touch down to prevent position jumps
                if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
                {
                    return;
                }

                Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
                Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

                float prevTouchDeltaMag = (touch0PrevPos - touch1PrevPos).magnitude;
                float touchDeltaMag = (touch0.position - touch1.position).magnitude;

                float deltaDistance = touchDeltaMag - prevTouchDeltaMag;

                if (Mathf.Abs(deltaDistance) > 0.01f)
                {
                    // Sensitivity multiplier: 0.015f per pixel delta
                    float zoomSensitivity = 0.015f;
                    targetZoomFactor = Mathf.Clamp(targetZoomFactor + (deltaDistance * zoomSensitivity), 1.0f, 3.0f);
                }
            }

#if UNITY_EDITOR
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetZoomFactor = Mathf.Clamp(targetZoomFactor + (scroll * 1.5f), 1.0f, 3.0f);
            }
#endif
        }

        private void UpdatePreviewUVRect()
        {
            if (previewUI == null) return;

            float uWidth = 1.0f / currentZoomFactor;
            float uHeight = 1.0f / currentZoomFactor;
            float uX = (1.0f - uWidth) * 0.5f;
            float uY = (1.0f - uHeight) * 0.5f;

            previewUI.uvRect = new Rect(uX, uY, uWidth, uHeight);
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

            UpdatePreviewUVRect();

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

            float mirrorX = 1f;
            float mirrorY = 1f;

            if (isRotated)
            {
                // When texture is rotated 90/270 degrees in UI, local Y maps to screen X (horizontal) and local X maps to screen Y (vertical).
                if (isUsingFrontCamera)
                {
                    mirrorX = 1f; // Keep vertical upright
                    mirrorY = webcamTexture.videoVerticallyMirrored ? 1f : -1f; // Mirror left/right horizontally
                }
                else
                {
                    mirrorX = 1f;
                    mirrorY = webcamTexture.videoVerticallyMirrored ? -1f : 1f;
                }
            }
            else
            {
                if (isUsingFrontCamera)
                {
                    mirrorX = -1f; // Mirror left/right horizontally
                    mirrorY = webcamTexture.videoVerticallyMirrored ? -1f : 1f;
                }
                else
                {
                    mirrorX = 1f;
                    mirrorY = webcamTexture.videoVerticallyMirrored ? -1f : 1f;
                }
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(targetWidth, targetHeight);
            rect.anchoredPosition = Vector2.zero;
            
            rect.localRotation = Quaternion.Euler(0, 0, -rotationAngle);
            rect.localScale = new Vector3(mirrorX, mirrorY, 1f);
        }

        public void CaptureFrame(Action<byte[]> onCaptured)
        {
            if (webcamTexture == null || !webcamTexture.isPlaying)
            {
                Debug.LogError("[MobileWebcam] Cannot capture: WebCamTexture is not active or initialized.");
                onCaptured?.Invoke(null);
                return;
            }

            try
            {
                int texW = webcamTexture.width;
                int texH = webcamTexture.height;

                int cropW = Mathf.Clamp(Mathf.RoundToInt(texW / currentZoomFactor), 1, texW);
                int cropH = Mathf.Clamp(Mathf.RoundToInt(texH / currentZoomFactor), 1, texH);
                int cropX = Mathf.RoundToInt((texW - cropW) * 0.5f);
                int cropY = Mathf.RoundToInt((texH - cropH) * 0.5f);

                Debug.Log("[Zoom] Capture uses current zoom crop");
                Debug.Log($"[Zoom] Capture crop rect = (x={cropX}, y={cropY}, width={cropW}, height={cropH})");

                Color[] pixels = webcamTexture.GetPixels(cropX, cropY, cropW, cropH);
                Texture2D croppedPhoto = new Texture2D(cropW, cropH, TextureFormat.RGB24, false);
                croppedPhoto.SetPixels(pixels);
                croppedPhoto.Apply();

                int rotationAngle = webcamTexture.videoRotationAngle;
                Texture2D orientedPhoto = croppedPhoto;

                if (rotationAngle != 0)
                {
                    orientedPhoto = RotateTexture(croppedPhoto, rotationAngle);
                    Destroy(croppedPhoto);
                }

                if (isUsingFrontCamera)
                {
                    Texture2D flippedPhoto = FlipTextureHorizontal(orientedPhoto);
                    if (orientedPhoto != croppedPhoto) Destroy(orientedPhoto);
                    orientedPhoto = flippedPhoto;
                }

                int normalW = (rotationAngle == 90 || rotationAngle == 270) ? texH : texW;
                int normalH = (rotationAngle == 90 || rotationAngle == 270) ? texW : texH;

                Texture2D finalPhoto = orientedPhoto;
                if (currentZoomFactor > 1.01f)
                {
                    finalPhoto = ResizeTexture(orientedPhoto, normalW, normalH);
                    Destroy(orientedPhoto);
                }

                byte[] jpegBytes = finalPhoto.EncodeToJPG(85);
                Destroy(finalPhoto);

                Debug.Log($"[MobileWebcam] Frame captured successfully ({jpegBytes.Length} bytes, output resolution {normalW}x{normalH}, zoom {currentZoomFactor:F2}x).");
                onCaptured?.Invoke(jpegBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MobileWebcam] Error capturing frame: {ex.Message}");
                onCaptured?.Invoke(null);
            }
        }

        private Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private Texture2D RotateTexture(Texture2D originalTexture, int angle)
        {
            int w = originalTexture.width;
            int h = originalTexture.height;
            int rotatedW = (angle == 90 || angle == 270) ? h : w;
            int rotatedH = (angle == 90 || angle == 270) ? w : h;
            
            Texture2D rotated = new Texture2D(rotatedW, rotatedH, originalTexture.format, false);
            Color32[] originalPixels = originalTexture.GetPixels32();
            Color32[] rotatedPixels = new Color32[originalPixels.Length];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int originalIndex = x + y * w;
                    int targetX = x;
                    int targetY = y;

                    if (angle == 90)
                    {
                        targetX = y;
                        targetY = w - 1 - x;
                    }
                    else if (angle == 180)
                    {
                        targetX = w - 1 - x;
                        targetY = h - 1 - y;
                    }
                    else if (angle == 270)
                    {
                        targetX = h - 1 - y;
                        targetY = x;
                    }

                    int targetIndex = targetX + targetY * rotatedW;
                    rotatedPixels[targetIndex] = originalPixels[originalIndex];
                }
            }

            rotated.SetPixels32(rotatedPixels);
            rotated.Apply();
            return rotated;
        }

        private Texture2D FlipTextureHorizontal(Texture2D originalTexture)
        {
            int w = originalTexture.width;
            int h = originalTexture.height;
            Texture2D flipped = new Texture2D(w, h, originalTexture.format, false);
            Color32[] pixels = originalTexture.GetPixels32();
            Color32[] flippedPixels = new Color32[pixels.Length];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int srcIndex = x + y * w;
                    int dstIndex = (w - 1 - x) + y * w;
                    flippedPixels[dstIndex] = pixels[srcIndex];
                }
            }

            flipped.SetPixels32(flippedPixels);
            flipped.Apply();
            return flipped;
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

            isInitialized = false;
            Debug.Log("[MobileWebcam] Released WebCamTexture resources.");
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}
