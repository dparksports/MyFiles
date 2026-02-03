using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileLister
{
    public static class AnalyticsService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string GaEndPoint = "https://www.google-analytics.com/mp/collect";
        
        // These will be loaded from config
        private static string _measurementId = "";
        private static string _apiSecret = "";

        public static void Initialize(string measurementId, string apiSecret)
        {
            _measurementId = measurementId;
            _apiSecret = apiSecret;
        }

        public static async Task TrackEvent(string eventName, object? parms = null)
        {
            if (!SettingsManager.Settings.IsAnalyticsEnabled) return;
            if (string.IsNullOrEmpty(_measurementId) || string.IsNullOrEmpty(_apiSecret)) return;

            try
            {
                var url = $"{GaEndPoint}?measurement_id={_measurementId}&api_secret={_apiSecret}";

                var payload = new
                {
                    client_id = SettingsManager.Settings.InstallationId,
                    events = new[]
                    {
                        new
                        {
                            name = eventName,
                            @params = parms ?? new { }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PostAsync(url, content);
            }
            catch
            {
                // Silently fail for analytics to avoid disrupting user experience
            }
        }
    }
}
