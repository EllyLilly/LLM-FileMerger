namespace FileMerger.Models;

public class FileItem
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsLoaded { get; set; }
    public string? ErrorMessage { get; set; }
}
