namespace MoonlightDaemon.App.Http.Resources;

public class FileEntry
{
    public string Name { get; set; }
    public long Size { get; set; }
    public bool IsFile { get; set; }
    public bool IsDirectory { get; set; }
    public DateTime LastModifiedAt { get; set; }
}