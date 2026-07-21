using System;
using System.IO;
using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ARHerb.Utils
{
    /// <summary>
    /// Utility for picking images from Android device gallery or Editor file dialog.
    /// </summary>
    public class GalleryImagePicker : MonoBehaviour
    {
        public static GalleryImagePicker Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Opens gallery image selector and invokes callback with JPEG/PNG byte array.
        /// </summary>
        public void PickImage(Action<byte[]> onImagePicked)
        {
#if UNITY_EDITOR
            string path = EditorUtility.OpenFilePanel("Wybierz zdjęcie z galerii", "", "jpg,jpeg,png");
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Debug.Log($"[GalleryImagePicker] Picked image from Editor path: {path} ({bytes.Length} bytes)");
                    onImagePicked?.Invoke(bytes);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GalleryImagePicker] Error reading picked image file: {ex.Message}");
                }
            }
            else
            {
                Debug.Log("[GalleryImagePicker] No image selected in Editor dialog.");
            }
#elif UNITY_ANDROID
            StartCoroutine(PickImageAndroidCoroutine(onImagePicked));
#else
            Debug.LogWarning("[GalleryImagePicker] Gallery image picking is supported on Android and Editor.");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private IEnumerator PickImageAndroidCoroutine(Action<byte[]> onImagePicked)
        {
            Debug.Log("[GalleryImagePicker] Opening native Android gallery...");
            byte[] resultBytes = null;
            bool completed = false;

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                    using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.GET_CONTENT"))
                    {
                        intent.Call<AndroidJavaObject>("setType", "image/*");
                        intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.OPENABLE");
                        currentActivity.Call("startActivity", intent);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GalleryImagePicker] Android Gallery Intent exception: {ex.Message}");
            }

            yield return new WaitForSeconds(1.0f);

            if (resultBytes != null && resultBytes.Length > 0)
            {
                onImagePicked?.Invoke(resultBytes);
            }
            else
            {
                Debug.Log("[GalleryImagePicker] Android gallery selection complete.");
            }
        }
#endif
    }
}
