using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace FileLister
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Initialize Settings
            SettingsManager.Load();

            // 2. Check EULA
            if (!SettingsManager.Settings.IsEulaAccepted)
            {
                var eulaWindow = new EulaWindow();
                bool? result = eulaWindow.ShowDialog();

                if (eulaWindow.IsAccepted)
                {
                    SettingsManager.Settings.IsEulaAccepted = true;
                    SettingsManager.Save();
                }
                else
                {
                    // User declined, shutdown
                    Shutdown();
                    return;
                }
            }

            // 3. Initialize Analytics
            InitializeAnalytics();

            // 4. Show Main Window
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private void InitializeAnalytics()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firebase_config.json");
                
                // Fallback for dev environment
                if (!File.Exists(configPath))
                {
                    string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\firebase_config.json"));
                    if (File.Exists(devPath)) configPath = devPath;
                }

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        var mid = root.GetProperty("measurementId").GetString();
                        var secret = root.GetProperty("apiSecret").GetString();
                        
                        if (!string.IsNullOrEmpty(mid) && !string.IsNullOrEmpty(secret))
                        {
                            AnalyticsService.Initialize(mid, secret);
                            _ = AnalyticsService.TrackEvent("app_launch");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Analytics failure should not crash app
                System.Diagnostics.Debug.WriteLine($"Analytics Init Failed: {ex.Message}");
            }
        }
    }
}
