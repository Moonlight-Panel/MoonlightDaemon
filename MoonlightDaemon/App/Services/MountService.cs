using MoonlightDaemon.App.Helpers;

namespace MoonlightDaemon.App.Services;

public class MountService
{
    private readonly BashHelper BashHelper;

    public MountService(BashHelper bashHelper)
    {
        BashHelper = bashHelper;
    }

    public async Task Mount(string server, string serverPath, string path)
    {
        Directory.CreateDirectory(path);
        var command = $"mount -t nfs {server}:{serverPath} {path}";
        await BashHelper.ExecuteCommand(command);
    }

    public async Task Unmount(string path)
    {
        var command = $"umount {path}";
        await BashHelper.ExecuteCommand(command);
    }
}