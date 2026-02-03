using System;
using System.IO;
using System.Windows;

namespace FileLister
{
    public partial class EulaWindow : Window
    {
        public bool IsAccepted { get; private set; } = false;

        public EulaWindow()
        {
            InitializeComponent();
            LoadEula();
        }

        private void LoadEula()
        {
            // Try to load from local file, otherwise use fallback (or could be embedded resource)
            string eulaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "terms.txt");
            
            // For development, check project root if not in bin
            if (!File.Exists(eulaPath))
            {
                // Go up a few levels to find it if debugging
                 string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\terms.txt"));
                 if (File.Exists(devPath)) eulaPath = devPath;
            }

            if (File.Exists(eulaPath))
            {
                EulaText.Text = File.ReadAllText(eulaPath);
            }
            else
            {
                EulaText.Text = "End User License Agreement not found. Please contact support.";
            }
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            IsAccepted = true;
            this.Close();
        }

        private void Decline_Click(object sender, RoutedEventArgs e)
        {
            IsAccepted = false;
            this.Close();
        }
    }
}
