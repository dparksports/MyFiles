using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace FileLister
{
    public partial class ComparisonView : UserControl
    {
        private string _selectedPathA;
        private string _selectedPathB;

        public ComparisonView()
        {
            InitializeComponent();
            _selectedPathA = string.Empty;
            _selectedPathB = string.Empty;
            LoadHistory();
        }

        public void SetMode(string mode)
        {
            // Reset Layout
            Grid.SetColumn(SimpleHistoryGroup, 0);
            Grid.SetColumnSpan(SimpleHistoryGroup, 1);
            Grid.SetColumn(ChecksumHistoryGroup, 2);
            Grid.SetColumnSpan(ChecksumHistoryGroup, 1);
            SimpleHistoryGroup.Visibility = Visibility.Visible;
            ChecksumHistoryGroup.Visibility = Visibility.Visible;
            ModeSelectionPanel.Visibility = Visibility.Visible;

            if (mode == "Simple")
            {
                ChecksumHistoryGroup.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(SimpleHistoryGroup, 3); // Span full width
                ModeCombo.SelectedIndex = 1;
                ModeSelectionPanel.Visibility = Visibility.Collapsed;
            }
            else if (mode == "Checksum")
            {
                SimpleHistoryGroup.Visibility = Visibility.Collapsed;
                Grid.SetColumn(ChecksumHistoryGroup, 0); // Move to first col
                Grid.SetColumnSpan(ChecksumHistoryGroup, 3); // Span full width
                ModeCombo.SelectedIndex = 0;
                ModeSelectionPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadHistory()
        {
            try
            {
                // 1. Load structured history for Grids
                var structuredHistory = ScanHistoryManager.GetHistory();
                
                await Task.Run(() => 
                {
                    foreach (var item in structuredHistory)
                    {
                        try
                        {
                            if (!File.Exists(item.FilePath))
                            {
                                item.VerificationStatus = "Missing File";
                            }
                            else
                            {
                                if (ChecksumHelper.VerifyChecksumFile(item.FilePath))
                                {
                                    item.VerificationStatus = "Verified";
                                }
                                else
                                {
                                    item.VerificationStatus = "Mismatch";
                                }
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            item.VerificationStatus = "No Sidecar";
                        }
                        catch
                        {
                            item.VerificationStatus = "Error";
                        }
                    }
                });

                SimpleHistoryGrid.ItemsSource = structuredHistory
                    .Where(x => x.Type == "SimpleList")
                    .OrderByDescending(x => x.Timestamp)
                    .ToList();

                ChecksumHistoryGrid.ItemsSource = structuredHistory
                    .Where(x => x.Type == "ChecksumManifest")
                    .OrderByDescending(x => x.Timestamp)
                    .ToList();
            }
            catch { /* Ignore load errors */ }
        }

        private void CompareSimple_Click(object sender, RoutedEventArgs e)
        {
            CompareSelectedRows(SimpleHistoryGrid, 1); // Mode 1 = Simple
        }

        private void CompareChecksum_Click(object sender, RoutedEventArgs e)
        {
            CompareSelectedRows(ChecksumHistoryGrid, 0); // Mode 0 = Checksum
        }
        
        private void BrowseSimple_Click(object sender, RoutedEventArgs e)
        {
            var file = BrowseFile();
            if(!string.IsNullOrEmpty(file))
            {
                // Add to history manager
                ScanHistoryManager.AddEntry(file, "SimpleList");
                // Reload to refresh grid and Verify
                LoadHistory();
            }
        }

        private void BrowseChecksum_Click(object sender, RoutedEventArgs e)
        {
            var file = BrowseFile();
            if(!string.IsNullOrEmpty(file))
            {
                // Add to history manager
                ScanHistoryManager.AddEntry(file, "ChecksumManifest");
                // Reload to refresh grid and Verify
                LoadHistory();
            }
        }

        private void RefreshHistory_Click(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        private void HistorySetBase_Click(object sender, RoutedEventArgs e)
        {
             SetFileFromHistoryContextMenu(sender, isBase: true);
        }

        private void HistorySetTarget_Click(object sender, RoutedEventArgs e)
        {
             SetFileFromHistoryContextMenu(sender, isBase: false);
        }

        private void SetFileFromHistoryContextMenu(object sender, bool isBase)
        {
             if (sender is MenuItem menuItem)
             {
                 // Find the row that was clicked
                 // The DataContext of the MenuItem (via ContextMenu) is the ScanHistoryItem
                 if (menuItem.DataContext is ScanHistoryItem item)
                 {
                     if (isBase)
                     {
                         _selectedPathA = item.FilePath;
                         SelectedBaseText.Text = item.FilePath;
                     }
                     else
                     {
                         _selectedPathB = item.FilePath;
                         SelectedTargetText.Text = item.FilePath;
                     }

                     // Try to match mode
                     if (item.Type == "ChecksumManifest") ModeCombo.SelectedIndex = 0;
                     if (item.Type == "SimpleList") ModeCombo.SelectedIndex = 1;
                 }
             }
        }

        private void CompareSelectedRows(System.Windows.Controls.DataGrid grid, int modeIndex)
        {
            var selectedItems = grid.SelectedItems.Cast<ScanHistoryItem>().ToList();
            if (selectedItems.Count != 2)
            {
                MessageBox.Show("Please select exactly 2 rows to compare.");
                return;
            }

            // Order by timestamp: Oldest = Base (A), Newest = Target (B)
            var ordered = selectedItems.OrderBy(x => x.Timestamp).ToList();
            var older = ordered[0];
            var newer = ordered[1];

            _selectedPathA = older.FilePath;
            _selectedPathB = newer.FilePath;

            SelectedBaseText.Text = _selectedPathA;
            SelectedTargetText.Text = _selectedPathB;

            // Set Mode
            ModeCombo.SelectedIndex = modeIndex;

            // Trigger compare
            Compare_Click(grid, new RoutedEventArgs());
        }

        private string BrowseFile()
        {
            var dialog = new OpenFileDialog { Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*" };
            return dialog.ShowDialog() == true ? dialog.FileName : string.Empty;
        }

        private async void Compare_Click(object sender, RoutedEventArgs e)
        {
            var fileA = _selectedPathA;
            var fileB = _selectedPathB;

            if (string.IsNullOrWhiteSpace(fileA) || string.IsNullOrWhiteSpace(fileB))
            {
                MessageBox.Show("Please select both files (Base and Target).");
                return;
            }
            
            if (!File.Exists(fileA))
            {
                MessageBox.Show($"File A (Base) does not exist: {fileA}");
                return;
            }
             if (!File.Exists(fileB))
            {
                MessageBox.Show($"File B (Target) does not exist: {fileB}");
                return;
            }

            var isChecksumMode = ModeCombo.SelectedIndex == 0;
            ResultsGrid.ItemsSource = null;

            try
            {
                List<ComparisonResult> results;

                if (isChecksumMode)
                {
                    results = await Task.Run(() => CompareChecksums(fileA, fileB));
                }
                else
                {
                    results = await Task.Run(() => CompareSimpleLists(fileA, fileB));
                }

                ResultsGrid.ItemsSource = results;
                MessageBox.Show($"Comparison Complete. Found {results.Count} differences/items of interest.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during comparison: {ex.Message}");
            }
        }

        private void Verify_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog 
            { 
                Title = "Select File List/Checksum File to Verify",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*" 
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool isValid = ChecksumHelper.VerifyChecksumFile(dialog.FileName);
                    if (isValid)
                    {
                        MessageBox.Show("Verification SUCCESS: The file matches its checksum sidecar.", "Integrity Check", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Verification FAILED: The file does NOT match its checksum sidecar!", "Integrity Check", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (FileNotFoundException)
                {
                    MessageBox.Show("Verification FAILED: Checksum sidecar file (.sha256) not found.", "Integrity Check", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error during verification: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsGrid.SelectedItem is ComparisonResult selected)
            {
                try
                {
                    Clipboard.SetText(selected.FilePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy to clipboard: {ex.Message}");
                }
            }
        }

        private List<ComparisonResult> CompareSimpleLists(string pathA, string pathB)
        {
            var listA = LoadPaths(pathA);
            var listB = LoadPaths(pathB);

            var results = new List<ComparisonResult>();

            // Added in B
            foreach (var file in listB)
            {
                if (!listA.Contains(file))
                    results.Add(new ComparisonResult { Status = "ADDED", FilePath = file, Details = "Not found in old file" });
            }

            // Missing in B (Removed)
            foreach (var file in listA)
            {
                if (!listB.Contains(file))
                    results.Add(new ComparisonResult { Status = "MISSING", FilePath = file, Details = "Found in old file only" });
            }

            return results.OrderBy(x => x.Status).ThenBy(x => x.FilePath).ToList();
        }

        private List<ComparisonResult> CompareChecksums(string pathA, string pathB)
        {
            var dictA = LoadChecksums(pathA);
            var dictB = LoadChecksums(pathB);
            
            var results = new List<ComparisonResult>();

            // Check B against A
            foreach (var kvp in dictB)
            {
                var path = kvp.Key;
                var checksum = kvp.Value;

                if (!dictA.ContainsKey(path))
                {
                    results.Add(new ComparisonResult { Status = "ADDED", FilePath = path, Details = "New file" });
                }
                else
                {
                    if (dictA[path] != checksum)
                    {
                        results.Add(new ComparisonResult { Status = "CHANGED", FilePath = path, Details = "Checksum mismatch" });
                    }
                }
            }

            // Check Missing
            foreach (var kvp in dictA)
            {
                if (!dictB.ContainsKey(kvp.Key))
                {
                    results.Add(new ComparisonResult { Status = "MISSING", FilePath = kvp.Key, Details = "Deleted file" });
                }
            }

            return results.OrderBy(x => x.Status).ThenBy(x => x.FilePath).ToList();
        }

        // Helpers
        private HashSet<string> LoadPaths(string file)
        {
            var lines = File.ReadAllLines(file);
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var csvRegex = new System.Text.RegularExpressions.Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (i == 0 && (line.StartsWith("FilePath") || line.StartsWith("\"FilePath\""))) continue;

                string path;
                if (line.StartsWith("\""))
                {
                    var parts = csvRegex.Split(line);
                    path = parts[0].Trim('"').Trim();
                }
                else
                {
                    path = line.Trim();
                }

                if (!string.IsNullOrWhiteSpace(path))
                {
                    set.Add(path);
                }
            }
            return set;
        }

        private Dictionary<string, string> LoadChecksums(string file)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(file);
            var csvRegex = new System.Text.RegularExpressions.Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");

            for(int i=0; i<lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (i == 0 && (line.StartsWith("FilePath") || line.StartsWith("\"FilePath\""))) continue;
                
                try 
                {
                    var parts = csvRegex.Split(line);
                    if (parts.Length >= 2)
                    {
                        var path = parts[0].Trim('"').Trim();
                        var checksum = parts[1].Trim('"').Trim();

                        if (!string.IsNullOrWhiteSpace(path) && !dict.ContainsKey(path))
                        {
                            dict.Add(path, checksum);
                        }
                    }
                }
                catch { /* Skip malformed */ }
            }

            return dict;
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var results = ResultsGrid.ItemsSource as List<ComparisonResult>;
            if (results == null || !results.Any()) return;

            var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "comparison_report.csv" };
            if (dialog.ShowDialog() == true)
            {
                var lines = new List<string> { "Status,FilePath,Details" };
                lines.AddRange(results.Select(r => $"{r.Status},\"{r.FilePath}\",{r.Details}"));
                File.WriteAllLines(dialog.FileName, lines);
                MessageBox.Show("Report Saved.");
            }
        }
    }

    public class ComparisonResult
    {
        public string Status { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
