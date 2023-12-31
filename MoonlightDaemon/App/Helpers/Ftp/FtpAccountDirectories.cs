using FubarDev.FtpServer.FileSystem;

namespace MoonlightDaemon.App.Helpers.Ftp;

public class FtpAccountDirectories : IAccountDirectories
{
    public string? RootPath { get; set; }
    public string? HomePath { get; set; }
}