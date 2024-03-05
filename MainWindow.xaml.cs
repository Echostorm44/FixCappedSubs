using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace FixCappedSubs;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public ObservableCollection<string> FilesToFix { get; set; } = [];

    public MainWindow()
    {
        InitializeComponent();
        this.DataContext = this;
    }

    private void ListBox_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if(e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if(files == null || files.Length == 0)
                {
                    return;
                }
                foreach(var item in files)
                {
                    if(string.IsNullOrEmpty(item) || FilesToFix.Contains(item))
                    {
                        continue;
                    }
                    FilesToFix.Add(item);
                }
            }
        }
        catch(Exception ex)
        {
            lblError.Text = ex.ToString();
        }
    }

    private void btnBrowseFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var fileBrowser = new Microsoft.Win32.OpenFileDialog() { CheckFileExists = true, Multiselect = true, };
            fileBrowser.ShowDialog();
            if(fileBrowser.FileNames != null && fileBrowser.FileNames.Length > 0)
            {
                foreach(string item in fileBrowser.FileNames)
                {
                    FilesToFix.Add(item.Trim());
                }
            }
        }
        catch(Exception ex)
        {
            lblError.Text = ex.ToString();
        }
    }

    private void btnGo_Click(object sender, RoutedEventArgs e)
    {
        // This is so fast it isn't worth multi-threading
        try
        {
            var files = FilesToFix.ToList();
            foreach(var inputFilePath in files)
            {
                lblStatus.Text = $"Processing {inputFilePath}...";
                string outputFilePath = Path.ChangeExtension(inputFilePath, ".srt");

                // Execute FFmpeg command
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-i \"{inputFilePath}\" -map 0:s:0 \"{outputFilePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using(Process process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    process.WaitForExit();
                }

                string[] lines = File.ReadAllLines(outputFilePath);
                bool isTextLine = false;

                for(int i = 0; i < lines.Length; i++)
                {
                    // Check if line is a number line, indicating the start of a new subtitle
                    if(Regex.IsMatch(lines[i], @"^\d+$"))
                    {
                        isTextLine = false;
                        continue;
                    }

                    // Check if line is a timestamp line
                    if(Regex.IsMatch(lines[i], @"\d{2}:\d{2}:\d{2},\d{3} --> \d{2}:\d{2}:\d{2},\d{3}"))
                    {
                        isTextLine = true;
                        continue;
                    }

                    // Process text lines
                    if(isTextLine)
                    {
                        lines[i] = FixLineCasing(lines[i]);
                    }
                }

                File.WriteAllLines(outputFilePath, lines);
                FilesToFix.Remove(inputFilePath);
            }
            lblStatus.Text = "Done!";
        }
        catch(Exception ex)
        {
            lblError.Text = ex.ToString();
        }
    }

    public static string FixLineCasing(string input)
    {
        if(string.IsNullOrEmpty(input))
        {
            return "";
        }
        // Remove font tags and extract the inner text
        string innerText = Regex.Replace(input, @"<[^>]+>", "");
        // Reconstruct the string with font tags
        string capitalizedText = SentenceCase(innerText);
        return input.Replace(innerText, capitalizedText);
    }

    public static string SentenceCase(string input)
    {
        if(input.Length < 1)
        {
            return input;
        }

        var sentenceRegex = new Regex(@"(^[a-z])|[?!.:;]\s+(.)", RegexOptions.ExplicitCapture);
        return sentenceRegex.Replace(input.ToLower(), s => s.Value.ToUpper());
    }
}