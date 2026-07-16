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

        public void Initialize(RawImage previewTarget)
        {
            previewUI = previewTarget;

            // 1. Check Android camera permission
            bool permissionGranted = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
            Debug.Log($"Android MVP mode: camera permission status = {(permissionGranted ? "GRANTED" : "DENIED")}");

            if (!permissionGranted)
            {
                Debug.LogError("[MobileWebcam] Camera permission is not granted. Cannot initialize WebCamTexture.");
                return;
            }

            // 2. Scan for camera devices
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                Debug.LogError("[MobileWebcam] No camera found on Android device.");
                return;
            }

            // 3. Select rear-facing camera
            WebCamDevice selectedDevice = devices[0];
            bool foundRear = false;
            foreach (var device in devices)
            {
                if (!device.isFrontFacing)
                {
                    selectedDevice = device;
                    foundRear = true;
                    break;
                }
            }

            Debug.Log("Android MVP mode: using WebCamTexture camera");
            Debug.Log($"[MobileWebcam] Selected camera device name: {selectedDevice.name} (Rear: {foundRear})");

            // 4. Start WebCamTexture
            webcamTexture = new WebCamTexture(selectedDevice.name, 1280, 720, 30);
            
            if (previewUI != null)
            {
                previewUI.texture = webcamTexture;
                previewUI.color = Color.white;
                previewUI.gameObject.SetActive(true);
                AdjustPreviewOrientation();
            }

            webcamTexture.Play();
            isInitialized = true;
            Debug.Log($"[MobileWebcam] Started WebCamTexture: width={webcamTexture.width}, height={webcamTexture.height}");
        }

        private void Update()
        {
            if (isInitialized && webcamTexture != null && webcamTexture.isPlaying && webcamTexture.didUpdateThisFrame)
            {
                AdjustPreviewOrientation();
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
            
            // Wait for WebCamTexture initialization (reports 16x16 initially)
            if (texW <= 16 || texH <= 16) return;

            int rotationAngle = webcamTexture.videoRotationAngle;
            bool isRotated = (rotationAngle == 90 || rotationAngle == 270);

            // Reset uvRect to default (no texture coordinate cropping)
            previewUI.uvRect = new Rect(0, 0, 1, 1);

            float targetWidth;
            float targetHeight;

            if (isRotated)
            {
                // Texture is rotated. Local width maps to screen height, local height maps to screen width.
                // We want: localWidth / localHeight = texW / texH
                float videoAspect = texW / texH; // e.g., 1.778
                
                // Try matching localHeight to screenW
                float h = screenW;
                float w = h * videoAspect;
                
                if (w < screenH)
                {
                    // Width is not enough to cover screenH, so fit to w = screenH
                    w = screenH;
                    h = w / videoAspect;
                }
                
                targetWidth = w;
                targetHeight = h;
            }
            else
            {
                // Texture is not rotated. Local width maps to screen width, local height maps to screen height.
                // We want: localWidth / localHeight = texW / texH
                float videoAspect = texW / texH;
                
                // Try matching localWidth to screenW
                float w = screenW;
                float h = w / videoAspect;
                
                if (h < screenH)
                {
                    // Height is not enough to cover screenH, so fit to h = screenH
                    h = screenH;
                    w = h * videoAspect;
                }
                
                targetWidth = w;
                targetHeight = h;
            }

            // Support mirroring
            float mirrorY = webcamTexture.videoVerticallyMirrored ? -1f : 1f;

            // Apply size, anchors and pivot
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(targetWidth, targetHeight);
            rect.anchoredPosition = Vector2.zero;
            
            // Set rotation and scale mirroring
            rect.localRotation = Quaternion.Euler(0, 0, -rotationAngle);
            rect.localScale = new Vector3(1f, mirrorY, 1f);
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
                Texture2D photo = new Texture2D(webcamTexture.width, webcamTexture.height, TextureFormat.RGB24, false);
                photo.SetPixels(webcamTexture.GetPixels());
                photo.Apply();

                int rotationAngle = webcamTexture.videoRotationAngle;
                if (rotationAngle != 0)
                {
                    Texture2D rotatedPhoto = RotateTexture(photo, rotationAngle);
                    byte[] jpegBytes = rotatedPhoto.EncodeToJPG(85);
                    Destroy(photo);
                    Destroy(rotatedPhoto);
                    Debug.Log($"[MobileWebcam] Frame captured and rotated successfully ({jpegBytes.Length} bytes).");
                    onCaptured?.Invoke(jpegBytes);
                }
                else
                {
                    byte[] jpegBytes = photo.EncodeToJPG(85);
                    Destroy(photo);
                    Debug.Log($"[MobileWebcam] Frame captured successfully ({jpegBytes.Length} bytes).");
                    onCaptured?.Invoke(jpegBytes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MobileWebcam] Error capturing frame: {ex.Message}");
                onCaptured?.Invoke(null);
            }
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
