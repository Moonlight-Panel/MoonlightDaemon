using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Helpers;

public class VolumeHelper
{
    private readonly ShellHelper ShellHelper;
    private readonly ConfigService ConfigService;

    public VolumeHelper(ShellHelper shellHelper, ConfigService configService)
    {
        ShellHelper = shellHelper;
        ConfigService = configService;
    }

    public async Task EnsureServerVolume(Server server)
    {
        var path = PathBuilder.Dir(ConfigService.Get().Storage.VolumePath, server.Id.ToString());
        
        // Ensure directory actually exists
        Directory.CreateDirectory(path);
        
        // Ensure file permissions are set correctly
        var tempConfig = ConfigService.GetTemp();
        await ShellHelper.ExecuteCommand($"chown -R {tempConfig.Uid}:{tempConfig.Gid} {path}");
    }

    public async Task EnsureInstallVolume(Server server)
    {
        var path = PathBuilder.Dir(ConfigService.Get().Storage.InstallPath, server.Id.ToString());
        
        // Ensure directory actually exists
        Directory.CreateDirectory(path);
        
        // Ensure file permissions are set correctly
        var tempConfig = ConfigService.GetTemp();
        await ShellHelper.ExecuteCommand($"chown -R {tempConfig.Uid}:{tempConfig.Gid} {path}");
    }
}