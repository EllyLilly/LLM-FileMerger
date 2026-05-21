using System.Text;
using FileMerger.Models;
using System.IO;


namespace FileMerger.Services;

public class FileMergeService
{
    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB per file
    private const long MaxTotalSize = 100 * 1024 * 1024; // 100 MB total for all files

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".json", ".txt", ".xml", ".config",
        ".xaml", ".cshtml", ".razor", ".css", ".js", ".ts",
        ".html", ".htm", ".md", ".yaml", ".yml", ".toml", ".ini"
    };

    public async Task<FileItem> LoadFileAsync(string filePath)
    {
        var fileItem = new FileItem
        {
            FullPath = filePath,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            var extension = Path.GetExtension(filePath);
            if (!AllowedExtensions.Contains(extension))
            {
                fileItem.IsLoaded = false;
                fileItem.ErrorMessage = $"Unsupported file type: {extension}";
                return fileItem;
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSize)
            {
                fileItem.IsLoaded = false;
                fileItem.ErrorMessage = $"File too large ({fileInfo.Length / 1024 / 1024} MB). Max: 10 MB";
                return fileItem;
            }

            if (IsBinaryFile(filePath))
            {
                fileItem.IsLoaded = false;
                fileItem.ErrorMessage = "Binary files are not supported";
                return fileItem;
            }

            fileItem.FileSize = fileInfo.Length;

            fileItem.Content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            fileItem.IsLoaded = true;
        }
        catch (UnauthorizedAccessException)
        {
            fileItem.IsLoaded = false;
            fileItem.ErrorMessage = "Access denied";
        }
        catch (IOException ex)
        {
            fileItem.IsLoaded = false;
            fileItem.ErrorMessage = $"IO error: {ex.Message}";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            fileItem.IsLoaded = false;
            fileItem.ErrorMessage = $"Failed to read file: {ex.Message}";
        }

        return fileItem;
    }

    public string MergeFiles(IEnumerable<FileItem> files, bool includeFullPath)
    {
        var loadedFiles = files.Where(f => f.IsLoaded).ToList();

        var totalSize = loadedFiles.Sum(f => f.FileSize);
        if (totalSize > MaxTotalSize)
        {
            throw new InvalidOperationException(
                $"Total size of all files ({totalSize / 1024 / 1024} MB) exceeds maximum ({MaxTotalSize / 1024 / 1024} MB)");
        }

        var sb = new StringBuilder();

        foreach (var file in loadedFiles)
        {
            sb.AppendLine("===========");
            sb.AppendLine($"FILE: {file.FileName}");

            if (includeFullPath)
            {
                sb.AppendLine($"PATH: {file.FullPath}");
            }

            sb.AppendLine();
            sb.AppendLine(file.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static bool IsBinaryFile(string path)
    {
        const int sampleSize = 1024;

        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[Math.Min(sampleSize, stream.Length)];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                    return true;
            }

            return false;
        }
        catch
        {
            // If we can't even read the file - safer to treat as binary and skip
            return true;
        }
    }
}
