using MoonCore.Attributes;

namespace MoonlightDaemon.App.Helpers;

[Singleton]
public class VolumeHelper
{
    private readonly ShellHelper ShellHelper;

    public VolumeHelper(ShellHelper shellHelper)
    {
        ShellHelper = shellHelper;
    }

    public async Task Ensure(string path, int uid, int gid)
    {
        // Ensure directory actually exists
        Directory.CreateDirectory(path);
        
        // Ensure file permissions are set correctly
        await ShellHelper.ExecuteCommand($"chown -R {uid}:{gid} {path}");
    }
}