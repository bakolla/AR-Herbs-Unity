using System;
using UnityEngine.UI;

namespace ARHerb.Camera
{
    /// <summary>
    /// Unified interface for handling camera frames from different sources (AR Foundation on devices, WebCamTexture in Editor).
    /// </summary>
    public interface ICameraCaptureProvider
    {
        /// <summary>
        /// Initializes the camera provider and links it to a RawImage preview target (needed for Editor/Webcam preview).
        /// </summary>
        /// <param name="previewTarget">The RawImage element where the camera feed should be rendered.</param>
        void Initialize(RawImage previewTarget);
        
        /// <summary>
        /// Captures the current camera frame and returns it as a raw JPEG byte array via the callback.
        /// </summary>
        /// <param name="onCaptured">Callback invoked with the captured JPEG byte array (null if capture fails).</param>
        void CaptureFrame(Action<byte[]> onCaptured);
        
        /// <summary>
        /// Sets the digital zoom factor (clamped between 1.0f and 3.0f).
        /// </summary>
        void SetZoom(float zoomFactor);

        /// <summary>
        /// Gets the current digital zoom factor.
        /// </summary>
        float GetZoom();

        /// <summary>
        /// Switches between rear and front facing camera.
        /// </summary>
        void SwitchCamera();

        /// <summary>
        /// Returns true if currently using front camera.
        /// </summary>
        bool IsFrontCamera();

        /// <summary>
        /// Returns an array of human-readable device names for all cameras available on the system.
        /// </summary>
        string[] GetAvailableCameraDevices();

        /// <summary>
        /// Gets the index of the currently active camera device.
        /// </summary>
        int GetCurrentCameraDeviceIndex();

        /// <summary>
        /// Selects a specific camera device by index.
        /// </summary>
        void SelectCameraDevice(int deviceIndex);

        /// <summary>
        /// Deinitializes and releases all resources/camera locks.
        /// </summary>
        void Release();
    }
}
