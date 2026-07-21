using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace ARHerb.Location
{
    /// <summary>
    /// Handles native GPS location services for Android and Editor.
    /// Uses native Input.location with zero HTTP requests.
    /// </summary>
    public class GPSLocationManager : MonoBehaviour
    {
        public static GPSLocationManager Instance { get; private set; }

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
        /// Requests Android permissions and attempts to get current GPS coordinates via native Input.location.
        /// </summary>
        public IEnumerator FetchLocationCoroutine(Action<bool, float, float> callback, float timeoutSeconds = 5f)
        {
            Debug.Log("[GPS] Native location flow started");

            bool hasPermission = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Debug.Log("[GPS] Permission granted = false. Requesting FineLocation permission...");
                Permission.RequestUserPermission(Permission.FineLocation);
                yield return new WaitForSeconds(0.5f);
            }
            hasPermission = Permission.HasUserAuthorizedPermission(Permission.FineLocation);
            Debug.Log($"[GPS] Permission granted = {hasPermission}");
#else
            Debug.Log("[GPS] Permission granted = true (Editor/PC mode)");
#endif

            if (!hasPermission)
            {
                Debug.LogWarning("[GPS] FineLocation permission denied. Saving scan without GPS.");
                Debug.Log("[GPS] No GPS, hasLocation=false");
                callback?.Invoke(false, 0f, 0f);
                yield break;
            }

            bool locationEnabled = Input.location.isEnabledByUser;
            Debug.Log($"[GPS] Location enabled by user = {locationEnabled}");

            if (!locationEnabled)
            {
                Debug.LogWarning("[GPS] Location disabled by user. Saving scan without GPS.");
                Debug.Log("[GPS] No GPS, hasLocation=false");
                callback?.Invoke(false, 0f, 0f);
                yield break;
            }

            try
            {
                Input.location.Start(10f, 10f); // 10m desired accuracy, 10m update distance
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GPS] Exception starting LocationService: {ex.Message}");
                Debug.Log("[GPS] No GPS, hasLocation=false");
                callback?.Invoke(false, 0f, 0f);
                yield break;
            }

            float elapsed = 0f;
            while (Input.location.status == LocationServiceStatus.Initializing && elapsed < timeoutSeconds)
            {
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            Debug.Log($"[GPS] Status = {Input.location.status}");

            if (Input.location.status == LocationServiceStatus.Running)
            {
                float latitude = Input.location.lastData.latitude;
                float longitude = Input.location.lastData.longitude;

                Debug.Log($"[GPS] Latitude = {latitude}");
                Debug.Log($"[GPS] Longitude = {longitude}");

                try { Input.location.Stop(); } catch {}

                callback?.Invoke(true, latitude, longitude);
            }
            else
            {
                if (Input.location.status == LocationServiceStatus.Initializing)
                {
                    Debug.LogWarning("[GPS] Timeout, saving scan without GPS");
                }
                else
                {
                    Debug.LogWarning($"[GPS] Location status {Input.location.status}, saving scan without GPS");
                }

                try { Input.location.Stop(); } catch {}

                Debug.Log("[GPS] No GPS, hasLocation=false");
                callback?.Invoke(false, 0f, 0f);
            }
        }

        /// <summary>
        /// Opens Google Maps in external browser/app at exact geographical coordinates formatted with CultureInfo.InvariantCulture.
        /// </summary>
        public static void OpenInGoogleMaps(float latitude, float longitude)
        {
            string latStr = latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string lngStr = longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string url = $"https://www.google.com/maps?q={latStr},{lngStr}";
            Debug.Log($"[Maps] formatted URL = {url}");
            Debug.Log("[Maps] Opening URL: " + url);
            Application.OpenURL(url);
        }
    }
}
