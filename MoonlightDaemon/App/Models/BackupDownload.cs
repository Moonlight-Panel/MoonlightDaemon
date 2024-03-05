namespace MoonlightDaemon.App.Models;

public class BackupDownload
{
    public string FileName { get; set; }
    public Stream Stream { get; set; }
    public string ContentType { get; set; }
}