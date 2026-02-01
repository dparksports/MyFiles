using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace FileLister
{
    public partial class ComparisonWindow : Window
    {
        public ComparisonWindow()
        {
            InitializeComponent();
        }

        private void BrowseA_Click(object sender, RoutedEventArgs e) => FileAText.Text = BrowseFile();
        private void BrowseB_Click(object sender, RoutedEventArgs e) => FileBText.Text = BrowseFile();

        private string BrowseFile()
        {
            var dialog = new OpenFileDialog { Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*" };
            return dialog.ShowDialog() == true ? dialog.FileName : string.Empty;
        }

        private async void Compare_Click(object sender, RoutedEventArgs e)
        {
            var fileA = FileAText.Text;
            var fileB = FileBText.Text;

            if (string.IsNullOrWhiteSpace(fileA) || string.IsNullOrWhiteSpace(fileB))
            {
                MessageBox.Show("Please select both files.");
                return;
            }

            if (!File.Exists(fileA) || !File.Exists(fileB))
            {
                MessageBox.Show("One or both files do not exist.");
                return;
            }

            var isChecksumMode = ModeCombo.SelectedIndex == 0;
            ResultsGrid.ItemsSource = null;

            try
            {
                List<ComparisonResult> results = null;

                await Task.Run(() =>
                {
                    if (isChecksumMode)
                        results = CompareChecksums(fileA, fileB);
                    else
                        results = CompareSimpleLists(fileA, fileB);
                });

                ResultsGrid.ItemsSource = results;
                MessageBox.Show($"Comparison Complete. Found {results.Count} differences/items of interest.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during comparison: {ex.Message}");
            }
        }

        private List<ComparisonResult> CompareSimpleLists(string pathA, string pathB)
        {
            // Simple logic: Read all lines, assume first column is path if CSV, or just lines
             // Helper to get paths
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
            // Dictionary <Path, Checksum>
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
                    // Exists in both, check checksum
                    if (dictA[path] != checksum)
                    {
                        results.Add(new ComparisonResult { Status = "CHANGED", FilePath = path, Details = "Checksum mismatch" });
                    }
                    // Else unchanged, we don't list unchanged typically unless requested? User asked for "missing or added or changed"
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
            
            // Skip header if looks like CSV
            var start = 0;
            if (lines.Length > 0 && lines[0].Contains("FilePath")) start = 1;

            for (int i = start; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                // If CSV with quotes
                var path = line;
                if (line.StartsWith("\"") && line.Contains("\",")) // Complex logic?
                {
                     // Simple CSV split for now, assuming generated by us
                     var parts = line.Split(new[] { "\"," }, StringSplitOptions.None);
                     path = parts[0].Trim('"');
                }
                else if (line.Contains(","))
                {
                     path = line.Split(',')[0].Trim('"'); // Fallback
                }
                
                set.Add(path);
            }
            return set;
        }

        private Dictionary<string, string> LoadChecksums(string file)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
             var lines = File.ReadAllLines(file);
             
             // Expect Header: FilePath,Checksum,...
             // CSV Parsing is tricky with comma in filename.
             // Our exporter wraps FilePath in Quotes.
             
             for(int i=0; i<lines.Length; i++)
             {
                 var line = lines[i];
                 if (i==0 && line.StartsWith("FilePath")) continue; // Header
                 if(string.IsNullOrWhiteSpace(line)) continue;

                 // Regex or manual split.
                 // Format: "FilePath",Checksum,...
                 // Find first quote, second quote. Checksum is after ","
                 
                 try 
                 {
                    var firstQuote = line.IndexOf('"');
                    var secondQuote = line.LastIndexOf("\","); // The separator between path and checksum
                    
                    // Fallback for simple csv
                    string path;
                    string checksum;
                    
                    if (firstQuote >= 0 && secondQuote > firstQuote)
                    {
                        path = line.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                        // Checksum is next part
                        var rest = line.Substring(secondQuote + 2); // skip ",
                        var parts = rest.Split(',');
                        checksum = parts[0];
                    }
                    else
                    {
                        // Assume no quotes or simple
                        var parts = line.Split(',');
                        path = parts[0];
                        if(parts.Length > 1) checksum = parts[1]; else checksum = "";
                    }

                    if(!dict.ContainsKey(path))
                        dict.Add(path, checksum);
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
        public string Status { get; set; } // ADDED, MISSING, CHANGED
        public string FilePath { get; set; }
        public string Details { get; set; }
    }
}
