using FubarDev.FtpServer;
using FubarDev.FtpServer.FileSystem;

namespace MoonlightDaemon.App.Helpers.Ftp;

public class FtpFileSystemClassFactory : IFileSystemClassFactory
{
    public Task<IUnixFileSystem> Create(IAccountInformation accountInformation)
    {
        var rootPath = accountInformation.FtpUser.Claims.First(x => x.Type == "rootPath").Value;

        return Task.FromResult(new FtpFileSystem(rootPath, true) as IUnixFileSystem);
    }
}