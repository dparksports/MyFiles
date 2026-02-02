using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        // private List<FileFullDetails> _checksumResults = new List<FileFullDetails>(); // Removing in favor of direct file append
        
        // State for Pause/Resume
        private bool _isPaused = false;
        private bool _isCalculating = false;
        private string _currentScanListPath = string.Empty;
        private int _scanRefreshRate = 10000;

        private int _checksumRefreshRate = 100;

        // Checksum Buffer
        private List<string> _checksumBuffer = new List<string>();
        private object _bufferLock = new object();
        private string _currentChecksumOutputPath = string.Empty;



        private const string STATE_FILE = "last_active_scan.txt";


        public MainWindow()
        {
            InitializeComponent();
            LoadDrives();
            CheckForResumableScan();
            
            // Initialize Views
            SimpleView.SetMode("Simple");
            ChecksumView.SetMode("Checksum");
        }

        private void CheckForResumableScan()
        {
            try
            {
                if (File.Exists(STATE_FILE))
                {
                    var path = File.ReadAllText(STATE_FILE).Trim();
                    if (File.Exists(path))
                    {
                        _currentScanListPath = path;
                        ChecksumControlButton.IsEnabled = true;
                        ChecksumControlButton.Content = "Resume Calculation";
                        StatusText.Text = "Previous scan found. Ready to resume.";
                    }
                }
            }
            catch { /* Ignore */ }
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



        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDrives = Drives.Where(d => d.IsSelected).ToList();
            if (!selectedDrives.Any())
            {
                MessageBox.Show("Please select at least one drive.");
                return;
            }

            ScanButton.IsEnabled = false;
            ChecksumControlButton.IsEnabled = false;
            ScanProgressBar.IsIndeterminate = true;
            StatusText.Text = "Scanning...";
            _scannedFiles.Clear();
            
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
                
                // 1. Auto-Save Scan List
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"scan_{timestamp}.csv");
                
                try 
                {
                    File.WriteAllLines(filename, new[] { "FilePath" }.Concat(_scannedFiles));
                    ScanHistoryManager.AddEntry(filename, "SimpleList");
                    
                    // Set State
                    _currentScanListPath = filename;
                    File.WriteAllText(STATE_FILE, filename);
                    
                    StatusText.Text += " List Saved.";
                    
                    // Auto-Save Integrity Sidecar
                    try
                    {
                        ChecksumHelper.SaveChecksumFile(filename);
                    }
                    catch (Exception ex)
                    {
                         MessageBox.Show($"Error creating sidecar: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error auto-saving list: {ex.Message}");
                }

                // 2. Auto-Start Checksum (or enable Resume capability)
                ChecksumControlButton.IsEnabled = true;
                _isPaused = false;
                StartChecksumProcess();
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
                    
                    if (count % _scanRefreshRate == 0)
                    {
                        Dispatcher.InvokeAsync(() => 
                        {
                            StatusText.Text = $"Found {count} files...";
                            UpdateRamUsage();
                        });
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

        private void ChecksumControlButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCalculating)
            {
                // Request Pause
                _isPaused = true;
                ChecksumControlButton.Content = "Resuming..."; // Transient state
                ChecksumControlButton.IsEnabled = false; // Disable until loop handles it
                // Allow loop to exit and flush, or flush here if loop takes time? 
                // Loop checks _isPaused, so it will exit scope. 
                // But better to let loop handle flush to minimize race conditions, 
                // OR explicit flush here if we want immediate feedback.
                // However, StartChecksumProcess handles the "Paused" state at the end.
                // We will rely on StartChecksumProcess to flush when it sees _isPaused.
            }
            else
            {
                // Output "Resume" or "Start"
                _isPaused = false;
                StartChecksumProcess();
            }
        }

        private async void StartChecksumProcess()
        {
            if (string.IsNullOrEmpty(_currentScanListPath) || !File.Exists(_currentScanListPath))
            {
                MessageBox.Show("No active scan file found.");
                return;
            }

            _isCalculating = true;
            ChecksumControlButton.Content = "Pause Calculation";
            ChecksumControlButton.IsEnabled = true;
            ScanButton.IsEnabled = false;
            
            // Define Output File
            string cleanPath = _currentScanListPath;
            if (cleanPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = cleanPath.Substring(0, cleanPath.Length - 4);
            }
            string outputCsv = cleanPath + ".checksums.csv";
            _currentChecksumOutputPath = outputCsv;


            
            // Load Source Files
            List<string> filesToProcess;
            try
            {
                // If _scannedFiles is empty (restart), reload from disk
                if (_scannedFiles == null || !_scannedFiles.Any())
                {
                    var lines = File.ReadAllLines(_currentScanListPath);
                    filesToProcess = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList(); // Skip Header
                }
                else
                {
                    filesToProcess = new List<string>(_scannedFiles);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading source list: {ex.Message}");
                _isCalculating = false;
                ScanButton.IsEnabled = true;
                return;
            }

            // Determine Start Point (Resume)
            HashSet<string> processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(outputCsv))
            {
                try 
                {
                    var existingLines = File.ReadAllLines(outputCsv);
                    // Assume CSV format: "FilePath",Checksum...
                    // Simple parsing to get FilePath (first column)
                    foreach(var line in existingLines)
                    {
                        if(string.IsNullOrWhiteSpace(line)) continue;
                        // Handle quoted CSV path - rough parse is sufficient if we perform same write logic
                        var parts = line.Split(','); 
                        if(parts.Length > 0)
                        {
                            string p = parts[0].Trim('"');
                            if(p != "FilePath") processedFiles.Add(p);
                        }
                    }
                }
                catch { /* corrupted output, maybe manual intervention needed */ }
            }
            else
            {
                // Initialize Output
                File.WriteAllText(outputCsv, "FilePath,Checksum,CalculationTimestamp,Size,CreationTime,LastWriteTime,Attributes" + Environment.NewLine);
                // Also add to history as "ChecksumManifest"
                ScanHistoryManager.AddEntry(outputCsv, "ChecksumManifest");
            }

            var remainingFiles = filesToProcess.Where(f => !processedFiles.Contains(f)).ToList();
            
            if (!remainingFiles.Any())
            {
                StatusText.Text = "All checksums already calculated.";
                _isCalculating = false;
                ScanButton.IsEnabled = true;
                ChecksumControlButton.Content = "Done";
                ChecksumControlButton.IsEnabled = false;
                // Clear state
                if (File.Exists(STATE_FILE)) File.Delete(STATE_FILE);
                return;
            }

            ScanProgressBar.Value = 0;
            ScanProgressBar.Maximum = remainingFiles.Count;
            ScanProgressBar.IsIndeterminate = false;
            StatusText.Text = $"Calculating: {remainingFiles.Count} remaining...";

            await Task.Run(() =>
            {
                int processedCount = 0;
                // Buffer removed for immediate write


                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                {
                    foreach (var filePath in remainingFiles)
                    {
                        if (_isPaused) break;

                        string lineToWrite = "";
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            string checksum;
                            using (var stream = File.OpenRead(filePath))
                            {
                                byte[] hash = sha256.ComputeHash(stream);
                                checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                            }
                            
                            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                            lineToWrite = $"\"{filePath}\",{checksum},{ts},{fileInfo.Length},{fileInfo.CreationTime},{fileInfo.LastWriteTime},\"{fileInfo.Attributes}\"";
                        }
                        catch
                        {
                            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                            lineToWrite = $"\"{filePath}\",ERROR,{ts},0,0,0,Error";
                        }

                        // Buffer writes
                        lock (_bufferLock)
                        {
                            _checksumBuffer.Add(lineToWrite);
                        }

                        processedCount++;
                        
                        // Update UI periodically
                            if (processedCount % _checksumRefreshRate == 0)
                            {
                                Dispatcher.Invoke(() => 
                                {
                                    ScanProgressBar.Value = processedCount;
                                    StatusText.Text = $"Calculating Checksums... ({processedCount}/{filesToProcess.Count})";
                                    UpdateRamUsage();
                                });
                            }
                    }
                }
            });

            // Flush Buffer on Pause or Complete
            FlushChecksumBuffer();

            _isCalculating = false;
            ScanButton.IsEnabled = true;

            if (_isPaused)
            {
                StatusText.Text = "Paused.";
                ChecksumControlButton.Content = "Resume Calculation";
                ChecksumControlButton.IsEnabled = true;
            }
            else
            {
                StatusText.Text = "Done.";
                ChecksumControlButton.Content = "Calculation Complete";
                ChecksumControlButton.IsEnabled = false;
                if (File.Exists(STATE_FILE)) File.Delete(STATE_FILE);

                // Auto-Save Integrity Sidecar for the Checksum Manifest
                try
                {
                    ChecksumHelper.SaveChecksumFile(_currentChecksumOutputPath);
                }
                catch (Exception ex)
                {
                     MessageBox.Show($"Error creating checksum manifest sidecar: {ex.Message}");
                }
            }
        }


        private void RefreshRateInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox box)
            {
                if (int.TryParse(box.Text, out int result))
                {
                    _scanRefreshRate = result > 0 ? result : 10000;
                }
            }
        }

        private void ChecksumRateInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox box)
            {
                if (int.TryParse(box.Text, out int result))
                {
                    _checksumRefreshRate = result > 0 ? result : 100;
                }
            }
        }

        private void FlushChecksumBuffer()
        {
            lock (_bufferLock)
            {
                if (_checksumBuffer.Any() && !string.IsNullOrEmpty(_currentChecksumOutputPath))
                {
                    try
                    {
                        File.AppendAllLines(_currentChecksumOutputPath, _checksumBuffer);
                        _checksumBuffer.Clear();
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => MessageBox.Show($"Error saving checksums: {ex.Message}"));
                    }
                }
            }
        }

        private void UpdateRamUsage()
        {
            long bytes = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            double mb = bytes / 1024.0 / 1024.0;
            RamUsageText.Text = $"RAM: {mb:F0} MB";
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop logic if running (though hard to force stop separate thread instantly)
            _isPaused = true;
            FlushChecksumBuffer();
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
