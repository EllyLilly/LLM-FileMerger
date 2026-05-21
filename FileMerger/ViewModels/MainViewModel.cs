using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileMerger.Models;
using FileMerger.Services;
using Microsoft.Win32;
using System.IO;

namespace FileMerger.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FileMergeService _fileService;

    public ObservableCollection<FileItem> Files { get; } = new();

    [ObservableProperty]
    private string _mergedText = string.Empty;

    [ObservableProperty]
    private bool _hasFiles;

    [ObservableProperty]
    private FileItem? _selectedFile;

    [ObservableProperty]
    private bool _removeEmptyLines;

    [ObservableProperty]
    private bool _removeComments;

    [ObservableProperty]
    private bool _removeExtraSpaces;

    [ObservableProperty]
    private bool _includeFullPath = true;

    [ObservableProperty]
    private bool _isProcessing;

    public MainViewModel()
    {
        _fileService = new FileMergeService();
        Files.CollectionChanged += (_, _) => HasFiles = Files.Count > 0;
    }

    public async Task AddFilesFromPathsAsync(IEnumerable<string> filePaths)
    {
        var existingPaths = Files
            .Select(f => f.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in filePaths)
        {
            if (!existingPaths.Contains(filePath))
            {
                var fileItem = await _fileService.LoadFileAsync(filePath);
                Files.Add(fileItem);
                existingPaths.Add(filePath);
            }
        }
    }

    [RelayCommand]
    private async Task AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select text files",
            Multiselect = true,
            Filter = "All supported files|*.cs;*.csproj;*.json;*.txt;*.xml;*.config;*.xaml;*.cshtml;*.razor|" +
                     "C# files|*.cs|" +
                     "JSON files|*.json|" +
                     "Project files|*.csproj|" +
                     "Text files|*.txt|" +
                     "All files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            await AddFilesFromPathsAsync(dialog.FileNames);
        }
    }

    [RelayCommand]
    private void RemoveFile(FileItem? file)
    {
        if (file != null)
        {
            Files.Remove(file);
        }
    }

    [RelayCommand]
    private void ClearFiles()
    {
        Files.Clear();
        MergedText = string.Empty;
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedFile == null) return;

        var index = Files.IndexOf(SelectedFile);
        if (index > 0)
        {
            Files.Move(index, index - 1);
        }
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedFile == null) return;

        var index = Files.IndexOf(SelectedFile);
        if (index < Files.Count - 1)
        {
            Files.Move(index, index + 1);
        }
    }

    [RelayCommand]
    private async Task MergeFiles()
    {
        if (Files.Count == 0) return;
        if (IsProcessing) return;

        IsProcessing = true;
        try
        {
            MergedText = await Task.Run(() =>
            {
                var merged = _fileService.MergeFiles(Files, IncludeFullPath);

                if (RemoveEmptyLines)
                    merged = RemoveEmptyLinesFromText(merged);

                if (RemoveComments)
                    merged = RemoveCommentsFromText(merged);

                if (RemoveExtraSpaces)
                    merged = RemoveExtraSpacesFromText(merged);

                return merged;
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during merge: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        if (string.IsNullOrEmpty(MergedText)) return;

        try
        {
            Clipboard.SetText(MergedText);
        }
        catch
        {
            MessageBox.Show("Failed to copy to clipboard. It may be busy.", "Warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("Copied to clipboard!", "Success",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task SaveToFile()
    {
        if (string.IsNullOrEmpty(MergedText)) return;
        if (IsProcessing) return;

        IsProcessing = true;
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save merged file",
                Filter = "Text file|*.txt|All files|*.*",
                DefaultExt = "txt",
                FileName = "merged_files.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(dialog.FileName, MergedText, Encoding.UTF8);
                MessageBox.Show($"File saved successfully!\n{dialog.FileName}", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private static string RemoveEmptyLinesFromText(string text)
    {
        var lines = text.Split('\n');
        var filtered = lines
            .Where(line => !string.IsNullOrWhiteSpace(line.Trim('\r')))
            .ToList();
        return string.Join("\n", filtered);
    }

    private static string RemoveCommentsFromText(string text)
    {
        // Remove multi-line comments /* ... */
        text = Regex.Replace(text, @"/\*.*?\*/", "",
            RegexOptions.Singleline, TimeSpan.FromSeconds(2));

        // Remove single-line comments
        // Use concatenation to prevent self-deletion
        var pattern = "/" + "/" + @".*$";
        text = Regex.Replace(text, pattern, "",
            RegexOptions.Multiline, TimeSpan.FromSeconds(1));

        // Remove lines that became empty
        var lines = text.Split('\n');
        var filtered = lines
            .Where(line => !string.IsNullOrWhiteSpace(line.Trim('\r')))
            .ToList();

        return string.Join("\n", filtered);
    }

    private static string RemoveExtraSpacesFromText(string text)
    {
        var lines = text.Split('\n');
        var cleaned = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r', '\n');

            if (trimmed.Length == 0)
            {
                cleaned.AppendLine();
                continue;
            }

            var leadingCount = trimmed.Length - trimmed.TrimStart().Length;
            var leading = leadingCount > 0 ? trimmed[..leadingCount] : "";
            var rest = trimmed[leadingCount..];

            rest = Regex.Replace(rest, @" {2,}", " ",
                RegexOptions.None, TimeSpan.FromSeconds(1));

            cleaned.AppendLine(leading + rest);
        }

        return cleaned.ToString();
    }
}