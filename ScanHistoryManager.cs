using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FileLister
{
    public class ScanHistoryItem
    {
        public string FilePath { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty; // "SimpleList" or "ChecksumManifest"
        public string FileChecksum { get; set; } = string.Empty; // SHA256 of the CSV file itself

        [System.Text.Json.Serialization.JsonIgnore]
        public string VerificationStatus { get; set; } = "Pending...";
    }

    public static class ScanHistoryManager
    {
        private const string HistoryFile = "scan_history.json";
        private static List<ScanHistoryItem> _history = new List<ScanHistoryItem>();

        static ScanHistoryManager()
        {
            Load();
        }

        public static void AddEntry(string path, string type)
        {
            try
            {
                var item = new ScanHistoryItem
                {
                    FilePath = path,
                    Timestamp = DateTime.Now,
                    Type = type,
                    FileChecksum = ChecksumHelper.CalculateSha256(path)
                };

                _history.Insert(0, item);
                
                // Limit to last 50
                if (_history.Count > 50) _history.RemoveRange(50, _history.Count - 50);

                Save();
            }
            catch (Exception ex)
            {
                // Silently fail or log? For a helper, maybe Debug.WriteLine
                System.Diagnostics.Debug.WriteLine($"Error adding history: {ex.Message}");
            }
        }

        public static List<ScanHistoryItem> GetHistory()
        {
            return _history.ToList();
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(HistoryFile))
                {
                    var json = File.ReadAllText(HistoryFile);
                    _history = JsonSerializer.Deserialize<List<ScanHistoryItem>>(json) ?? new List<ScanHistoryItem>();
                }
            }
            catch
            {
                _history = new List<ScanHistoryItem>();
            }
        }

        private static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(HistoryFile, json);
            }
            catch { }
        }
    }
}
