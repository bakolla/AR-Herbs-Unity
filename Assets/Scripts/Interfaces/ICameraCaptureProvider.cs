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
        /// Deinitializes and releases all resources/camera locks.
        /// </summary>
        void Release();
    }
}
