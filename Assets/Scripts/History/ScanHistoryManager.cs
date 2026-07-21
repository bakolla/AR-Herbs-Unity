using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ARHerb.History
{
    [Serializable]
    public class HistoryItem
    {
        public string id;
        public string commonName;
        public string scientificName;
        public string mode;
        public string timestamp;
        public float score;
        public string description;
        public string funFact;
        public string edibleStatus;
        public string edibleNote;
        public string thumbnailPath;
        public bool hasLocation;
        public float latitude;
        public float longitude;
    }

    [Serializable]
    public class HistoryDataContainer
    {
        public List<HistoryItem> items = new List<HistoryItem>();
    }

    public static class ScanHistoryManager
    {
        private const int MaxHistoryItems = 30;

        private static string GetFilePath()
        {
            return Path.Combine(Application.persistentDataPath, "scan_history.json");
        }

        private static string GetThumbnailsDir()
        {
            string dir = Path.Combine(Application.persistentDataPath, "thumbnails");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static HistoryDataContainer LoadHistory()
        {
            string path = GetFilePath();
            if (!File.Exists(path))
            {
                return new HistoryDataContainer();
            }

            try
            {
                string json = File.ReadAllText(path);
                HistoryDataContainer data = JsonUtility.FromJson<HistoryDataContainer>(json);
                return data ?? new HistoryDataContainer();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScanHistoryManager] Error loading history: {ex.Message}");
                return new HistoryDataContainer();
            }
        }

        public static void SaveHistory(HistoryDataContainer container)
        {
            try
            {
                string path = GetFilePath();
                string json = JsonUtility.ToJson(container, true);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScanHistoryManager] Error saving history: {ex.Message}");
            }
        }

        public static HistoryItem SaveScan(
            string commonName, 
            string scientificName, 
            string mode, 
            float score, 
            string description, 
            string funFact, 
            string edibleStatus, 
            string edibleNote, 
            byte[] jpegBytes,
            bool hasLocation = false,
            float latitude = 0f,
            float longitude = 0f)
        {
            HistoryDataContainer container = LoadHistory();

            string itemId = Guid.NewGuid().ToString();
            string thumbPath = "";

            if (jpegBytes != null && jpegBytes.Length > 0)
            {
                try
                {
                    string filename = $"thumb_{itemId}.jpg";
                    thumbPath = Path.Combine(GetThumbnailsDir(), filename);
                    File.WriteAllBytes(thumbPath, jpegBytes);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ScanHistoryManager] Error saving thumbnail: {ex.Message}");
                }
            }

            HistoryItem item = new HistoryItem
            {
                id = itemId,
                commonName = commonName,
                scientificName = scientificName,
                mode = mode,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                score = score,
                description = description,
                funFact = funFact,
                edibleStatus = edibleStatus,
                edibleNote = edibleNote,
                thumbnailPath = thumbPath,
                hasLocation = hasLocation,
                latitude = latitude,
                longitude = longitude
            };

            // Insert at the beginning (newest first)
            container.items.Insert(0, item);

            // Keep only the 30 most recent items
            while (container.items.Count > MaxHistoryItems)
            {
                HistoryItem oldest = container.items[container.items.Count - 1];
                if (!string.IsNullOrEmpty(oldest.thumbnailPath) && File.Exists(oldest.thumbnailPath))
                {
                    try { File.Delete(oldest.thumbnailPath); } catch {}
                }
                container.items.RemoveAt(container.items.Count - 1);
            }

            SaveHistory(container);
            Debug.Log($"[ScanHistoryManager] Saved scan: {commonName} ({scientificName}), total items: {container.items.Count}");
            return item;
        }

        public static void ClearHistory()
        {
            HistoryDataContainer container = LoadHistory();
            foreach (var item in container.items)
            {
                if (!string.IsNullOrEmpty(item.thumbnailPath) && File.Exists(item.thumbnailPath))
                {
                    try { File.Delete(item.thumbnailPath); } catch {}
                }
            }
            container.items.Clear();
            SaveHistory(container);
            Debug.Log("[ScanHistoryManager] Cleared history.");
        }
    }
}
