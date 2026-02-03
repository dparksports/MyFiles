using System;
using System.IO;
using System.Text.Json;

namespace FileLister
{
    public class AppSettings
    {
        public bool IsEulaAccepted { get; set; } = false;
        public bool IsAnalyticsEnabled { get; set; } = true;
        public string InstallationId { get; set; } = Guid.NewGuid().ToString();
    }

    public static class SettingsManager
    {
        private const string SettingsFile = "settings.json";
        public static AppSettings Settings { get; private set; } = new AppSettings();

        static SettingsManager()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    Settings = new AppSettings();
                    Save(); // Create initial file
                }
            }
            catch
            {
                Settings = new AppSettings();
            }
        }

        public static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch
            {
                // Handle or log save error
            }
        }
    }
}
