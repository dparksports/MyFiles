using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace FileLister
{
    public class FileFullDetails
    {
        public string FilePath { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
        public string CalculationTimestamp { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public string Attributes { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        public List<DriveViewModel> Drives { get; set; } = new List<DriveViewModel>();
        private List<string> _scannedFiles = new List<string>();
        private List<FileFullDetails> _checksumResults = new List<FileFullDetails>();

        public MainWindow()
        {
            InitializeComponent();
            LoadDrives();
        }

        private void LoadDrives()
        {
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    if (drive.IsReady)
                    {
                        Drives.Add(new DriveViewModel
                        {
                            Name = drive.Name,
                            DriveType = drive.DriveType.ToString(),
                            IsSelected = drive.DriveType == DriveType.Fixed
                        });
                    }
                }
                DrivesList.ItemsSource = Drives;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading drives: {ex.Message}");
            }
        }

        private void OpenComparison_Click(object sender, RoutedEventArgs e)
        {
            var window = new ComparisonWindow();
            window.Show();
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDrives = Drives.Where(d => d.IsSelected).ToList();
            if (!selectedDrives.Any())
            {
                MessageBox.Show("Please select at least one drive.");
                return;
            }

            ScanButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            ChecksumButton.IsEnabled = false;
            ScanProgressBar.IsIndeterminate = true;
            StatusText.Text = "Scanning...";
            _scannedFiles.Clear();
            _checksumResults.Clear();

            try
            {
                await Task.Run(() =>
                {
                    foreach (var drive in selectedDrives)
                    {
                        Dispatcher.Invoke(() => StatusText.Text = $"Scanning {drive.Name}...");
                        ScanDirectory(new DirectoryInfo(drive.Name));
                    }
                });

                StatusText.Text = $"Scan complete. Found {_scannedFiles.Count} files.";
                SaveButton.IsEnabled = true;
                ChecksumButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error during scan.";
                MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                ScanButton.IsEnabled = true;
                ScanProgressBar.IsIndeterminate = false;
                ScanProgressBar.Value = 100;
            }
        }

        private void ScanDirectory(DirectoryInfo directory)
        {
            try
            {
                foreach (var file in directory.EnumerateFiles())
                {
                    int count;
                    lock (_scannedFiles)
                    {
                        _scannedFiles.Add(file.FullName);
                        count = _scannedFiles.Count;
                    }
                    
                    if (count % 1000 == 0)
                    {
                        Dispatcher.InvokeAsync(() => StatusText.Text = $"Found {count} files...");
                    }
                }

                foreach (var dir in directory.EnumerateDirectories())
                {
                    ScanDirectory(dir);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            catch (Exception) { }
        }

        private async void ChecksumButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_scannedFiles.Any())
            {
                MessageBox.Show("No files to process. Please scan first.");
                return;
            }

            ChecksumButton.IsEnabled = false;
            ScanButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            ScanProgressBar.IsIndeterminate = false;
            ScanProgressBar.Value = 0;
            ScanProgressBar.Maximum = _scannedFiles.Count;
            StatusText.Text = "Calculating Checksums...";

            _checksumResults.Clear();

            try
            {
                await Task.Run(() =>
                {
                    int processedCount = 0;
                    System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create();

                    // Using a loop instead of Parallel.ForEach to allow easier progress reporting and single-threaded SHA instantiation simplicity (or recreate per thread)
                    // For UI responsiveness and progress, simple loop is okay, or Parallel with Interlocked.
                    // Let's stick to simple loop for now to ensure SHA256 object safety or create new inside.
                    
                    foreach (var filePath in _scannedFiles)
                    {
                        var details = new FileFullDetails { FilePath = filePath };
                        
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            details.Size = fileInfo.Length;
                            details.CreationTime = fileInfo.CreationTime;
                            details.LastWriteTime = fileInfo.LastWriteTime;
                            details.Attributes = fileInfo.Attributes.ToString();

                            using (var stream = File.OpenRead(filePath))
                            {
                                // Create hash per iteration or reuse? Reuse is fine if sequential.
                                // Inside Task.Run is one thread unless we spawn more.
                                // Re-creating is safer/clearer.
                                using (var sha = System.Security.Cryptography.SHA256.Create())
                                {
                                     byte[] hash = sha.ComputeHash(stream);
                                     details.Checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                                }
                            }
                            details.CalculationTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        }
                        catch (Exception)
                        {
                            details.Checksum = "ERROR";
                            details.CalculationTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        }

                        lock (_checksumResults)
                        {
                            _checksumResults.Add(details);
                        }

                        processedCount++;
                        if (processedCount % 100 == 0)
                        {
                            Dispatcher.InvokeAsync(() => 
                            {
                                ScanProgressBar.Value = processedCount;
                                StatusText.Text = $"Checksum: {processedCount} / {_scannedFiles.Count}";
                            });
                        }
                    }
                });

                StatusText.Text = "Checksum calculation complete.";
                SaveChecksumsToCsv();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error calculating checksums: {ex.Message}");
                StatusText.Text = "Error during checksum calculation.";
            }
            finally
            {
                ChecksumButton.IsEnabled = true;
                ScanButton.IsEnabled = true;
                SaveButton.IsEnabled = true;
                ScanProgressBar.Value = 100; // Reset or max
            }
        }

        private void SaveChecksumsToCsv()
        {
             var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"checksums_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "Save Checksum Results"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new StreamWriter(saveFileDialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        writer.WriteLine("FilePath,Checksum,CalculationTimestamp,Size,CreationTime,LastWriteTime,Attributes");
                        foreach (var item in _checksumResults)
                        {
                            // Wrap path in quotes to handle commas
                            writer.WriteLine($"\"{item.FilePath}\",{item.Checksum},{item.CalculationTimestamp},{item.Size},{item.CreationTime},{item.LastWriteTime},\"{item.Attributes}\"");
                        }
                    }
                    MessageBox.Show($"Saved {_checksumResults.Count} records to {saveFileDialog.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}");
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "files.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllLines(saveFileDialog.FileName, new[] { "FilePath" }.Concat(_scannedFiles));
                    MessageBox.Show($"Saved {_scannedFiles.Count} files to {saveFileDialog.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}");
                }
            }
        }
    }

    public class DriveViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;
        public bool IsSelected { get; set; }

        public string DisplayName => $"{Name} ({DriveType})";
    }
}
