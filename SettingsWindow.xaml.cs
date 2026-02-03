using System.Windows;

namespace FileLister
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            AnalyticsCheckbox.IsChecked = SettingsManager.Settings.IsAnalyticsEnabled;
            InstallIdText.Text = SettingsManager.Settings.InstallationId;
        }

        private void AnalyticsCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (AnalyticsCheckbox.IsChecked.HasValue)
            {
                SettingsManager.Settings.IsAnalyticsEnabled = AnalyticsCheckbox.IsChecked.Value;
                SettingsManager.Save();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
