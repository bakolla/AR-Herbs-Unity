using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using ARHerb.Data;

namespace ARHerb.Network
{
    /// <summary>
    /// Manages HTTP communication with the Node.js/Express backend server.
    /// Interfaces with the /api/identify and /api/enrich endpoints.
    /// </summary>
    public class BackendClient : MonoBehaviour
    {
        [Header("Backend Settings")]
        [Tooltip("Backend Server Address.\n- In Unity Editor: http://localhost:3001\n- On Mobile: Use your Pinggy, ngrok, or hosted HTTPS backend URL (e.g. https://yourtunnel.pinggy.link).")]
        [SerializeField] private string backendUrl = "http://localhost:3001";

        /// <summary>
        /// Sends an image payload to the backend's /api/identify endpoint.
        /// </summary>
        /// <param name="imageBytes">JPEG compressed image data.</param>
        /// <param name="mode">Specimen category mode (plants, mushrooms, insects, stones).</param>
        /// <param name="lang">Language code (e.g. "pl" or "en").</param>
        /// <param name="onSuccess">Callback invoked upon successful response deserialization.</param>
        /// <param name="onFailure">Callback invoked with detailed error information on failure.</param>
        public void IdentifySpecimen(byte[] imageBytes, string mode, string lang, Action<ScanResult> onSuccess, Action<string> onFailure)
        {
            StartCoroutine(SendIdentifyRequest(imageBytes, mode, lang, onSuccess, onFailure));
        }

        private IEnumerator SendIdentifyRequest(byte[] imageBytes, string mode, string lang, Action<ScanResult> onSuccess, Action<string> onFailure)
        {
            string requestUrl = $"{backendUrl.TrimEnd('/')}/api/identify";

            // Encode raw JPEG image bytes to Base64
            string base64String = Convert.ToBase64String(imageBytes);
            
            // Format as a data URL: "data:image/jpeg;base64,..."
            string dataUrl = $"data:image/jpeg;base64,{base64String}";

            // Construct payload matching existing frontend ar.js request format:
            // JSON body contains: imageBase64, mode, lang, and optional organs list for plants.
            var payload = new IdentifyPayload
            {
                imageBase64 = dataUrl,
                mode = mode.ToLower(),
                lang = lang,
                organs = mode.ToLower() == "plants" ? new string[] { "leaf" } : null
            };

            string jsonPayload = JsonConvert.SerializeObject(payload);

            Debug.Log($"[BackendClient] Sending POST to {requestUrl} (Mode: {mode}, Lang: {lang}, Size: {imageBytes.Length} bytes)...");

            using (UnityWebRequest request = new UnityWebRequest(requestUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"[BackendClient] Identify response:\n{jsonResponse}");

                    try
                    {
                        ScanResult result = JsonConvert.DeserializeObject<ScanResult>(jsonResponse);
                        if (result != null)
                        {
                            onSuccess?.Invoke(result);
                        }
                        else
                        {
                            onFailure?.Invoke("Deserialization returned null.");
                        }
                    }
                    catch (Exception ex)
                    {
                        onFailure?.Invoke($"JSON Deserialization Error: {ex.Message}");
                    }
                }
                else
                {
                    string errorDetail = request.downloadHandler != null ? request.downloadHandler.text : "No details";
                    onFailure?.Invoke($"HTTP Code {request.responseCode}: {request.error}\nResponse Details: {errorDetail}");
                }
            }
        }

        /// <summary>
        /// Sends a request to /api/enrich to fetch detailed Gemini descriptions, fun facts, and edibility for plants.
        /// </summary>
        /// <param name="scientificName">The scientific name of the specimen.</param>
        /// <param name="commonNames">List of common names associated with the specimen.</param>
        /// <param name="lang">Language code (e.g. "pl" or "en").</param>
        /// <param name="onSuccess">Callback returning the enrichment data.</param>
        /// <param name="onFailure">Callback invoked with detailed error information on failure.</param>
        public void EnrichPlant(string scientificName, List<string> commonNames, string lang, Action<EnrichResponse> onSuccess, Action<string> onFailure)
        {
            StartCoroutine(SendEnrichRequest(scientificName, commonNames, lang, onSuccess, onFailure));
        }

        private IEnumerator SendEnrichRequest(string scientificName, List<string> commonNames, string lang, Action<EnrichResponse> onSuccess, Action<string> onFailure)
        {
            string requestUrl = $"{backendUrl.TrimEnd('/')}/api/enrich";

            var payload = new EnrichPayload
            {
                scientificName = scientificName,
                commonNames = commonNames ?? new List<string>(),
                lang = lang
            };

            string jsonPayload = JsonConvert.SerializeObject(payload);

            Debug.Log($"[BackendClient] Sending POST to {requestUrl} (ScientificName: {scientificName})...");

            using (UnityWebRequest request = new UnityWebRequest(requestUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"[BackendClient] Enrich response:\n{jsonResponse}");

                    try
                    {
                        EnrichResponse result = JsonConvert.DeserializeObject<EnrichResponse>(jsonResponse);
                        if (result != null)
                        {
                            onSuccess?.Invoke(result);
                        }
                        else
                        {
                            onFailure?.Invoke("Enrichment deserialization returned null.");
                        }
                    }
                    catch (Exception ex)
                    {
                        onFailure?.Invoke($"JSON Deserialization Error: {ex.Message}");
                    }
                }
                else
                {
                    string errorDetail = request.downloadHandler != null ? request.downloadHandler.text : "No details";
                    onFailure?.Invoke($"HTTP Code {request.responseCode}: {request.error}\nResponse Details: {errorDetail}");
                }
            }
        }

        public void SetBackendUrl(string newUrl)
        {
            if (!string.IsNullOrEmpty(newUrl))
            {
                backendUrl = newUrl;
                Debug.Log($"[BackendClient] Updated backend URL to: {backendUrl}");
            }
        }

        public string GetBackendUrl()
        {
            return backendUrl;
        }
    }

    [Serializable]
    public class IdentifyPayload
    {
        public string imageBase64;
        public string mode;
        public string lang;
        public string[] organs;
    }

    [Serializable]
    public class EnrichPayload
    {
        public string scientificName;
        public List<string> commonNames;
        public string lang;
    }
}
