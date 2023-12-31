using FubarDev.FtpServer;
using FubarDev.FtpServer.FileSystem;

namespace MoonlightDaemon.App.Helpers.Ftp;

public class FtpAccountDirectoryQuery : IAccountDirectoryQuery
{
    public IAccountDirectories GetDirectories(IAccountInformation accountInformation)
    {
        return new FtpAccountDirectories()
        {
            HomePath = "/",
            RootPath = "/"
        };
    }
}