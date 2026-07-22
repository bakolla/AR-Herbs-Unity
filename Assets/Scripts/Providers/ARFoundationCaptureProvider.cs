using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARHerb.Camera
{
    /// <summary>
    /// AR Foundation Camera Provider for Android and iOS.
    /// Captures the camera frame directly via CPU access (TryAcquireLatestCpuImage)
    /// to get a clean camera frame without UI overlays. Falls back to screen capture if needed.
    /// </summary>
    public class ARFoundationCaptureProvider : MonoBehaviour, ICameraCaptureProvider
    {
        [Header("AR References")]
        [SerializeField] private ARCameraManager cameraManager;
        private float currentZoomFactor = 1.0f;

        public void SetZoom(float zoomFactor)
        {
            currentZoomFactor = Mathf.Clamp(zoomFactor, 1.0f, 3.0f);
            Debug.Log($"[Zoom] Current level = {currentZoomFactor:F2}x");
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
            Debug.Log("[ARFoundation] SwitchCamera called.");
        }

        public string[] GetAvailableCameraDevices()
        {
            return new string[] { "Kamera AR Foundation" };
        }

        public int GetCurrentCameraDeviceIndex()
        {
            return 0;
        }

        public void SelectCameraDevice(int deviceIndex)
        {
            Debug.Log($"[ARFoundation] SelectCameraDevice({deviceIndex})");
        }

        public void Initialize(RawImage previewTarget)
        {
            // In AR Foundation, the AR Camera Background component automatically renders
            // the camera feed directly onto the screen background (behind UI).
            // Thus, we hide the RawImage preview target since it is not needed.
            if (previewTarget != null)
            {
                previewTarget.gameObject.SetActive(false);
            }
            
            if (cameraManager == null)
            {
                cameraManager = FindFirstObjectByType<ARCameraManager>();
            }

            if (cameraManager == null)
            {
                Debug.LogError("[ARCapture] ARCameraManager component not found in the scene.");
            }
            else
            {
                Debug.Log("[ARCapture] ARFoundationCaptureProvider initialized with ARCameraManager.");
            }
        }

        public void CaptureFrame(Action<byte[]> onCaptured)
        {
            if (cameraManager == null || !cameraManager.subsystem.running)
            {
                Debug.LogWarning("[ARCapture] AR Camera Subsystem not running. Falling back to Screen Capture...");
                StartCoroutine(CaptureScreenRoutine(onCaptured));
                return;
            }

            // Try to acquire the latest camera image directly from the GPU/CPU interface
            if (cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            {
                using (image)
                {
                    try
                    {
                        // Convert YUV/Grayscale raw camera texture format to standard RGB24 format
                        var conversionParams = new XRCpuImage.ConversionParams
                        {
                            inputRect = new RectInt(0, 0, image.width, image.height),
                            outputDimensions = new Vector2Int(image.width, image.height),
                            outputFormat = TextureFormat.RGB24,
                            transformation = XRCpuImage.Transformation.None
                        };

                        // Allocate temporary texture to receive converted bytes
                        Texture2D texture = new Texture2D(image.width, image.height, TextureFormat.RGB24, false);
                        var rawTextureData = texture.GetRawTextureData<byte>();
                        image.Convert(conversionParams, rawTextureData);
                        texture.Apply();

                        // Encode the texture to JPG
                        byte[] jpegBytes = texture.EncodeToJPG(85);
                        Destroy(texture);

                        Debug.Log($"[ARCapture] Frame captured via XRCpuImage ({jpegBytes.Length} bytes).");
                        onCaptured?.Invoke(jpegBytes);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ARCapture] XRCpuImage conversion failed: {ex.Message}. Falling back to Screen Capture...");
                        StartCoroutine(CaptureScreenRoutine(onCaptured));
                    }
                }
            }
            else
            {
                Debug.LogWarning("[ARCapture] Could not acquire CPU image. Falling back to Screen Capture...");
                StartCoroutine(CaptureScreenRoutine(onCaptured));
            }
        }

        private IEnumerator CaptureScreenRoutine(Action<byte[]> onCaptured)
        {
            // Wait until the current frame has finished rendering to avoid half-rendered frames
            yield return new WaitForEndOfFrame();

            try
            {
                Texture2D screenTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                screenTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                screenTexture.Apply();

                byte[] jpegBytes = screenTexture.EncodeToJPG(85);
                Destroy(screenTexture);

                Debug.Log($"[ARCapture] Frame captured via Screen Capture ({jpegBytes.Length} bytes).");
                onCaptured?.Invoke(jpegBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ARCapture] Screen capture fallback failed: {ex.Message}");
                onCaptured?.Invoke(null);
            }
        }

        public void Release()
        {
            Debug.Log("[ARCapture] AR Foundation Capture resources released.");
        }
    }
}
