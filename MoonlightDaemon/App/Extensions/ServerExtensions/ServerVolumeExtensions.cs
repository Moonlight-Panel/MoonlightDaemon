using MoonCore.Helpers;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerVolumeExtensions
{
    //TODO: Make the 998 dynamicly loaded
    public static async Task EnsureRuntimeVolume(this Server server) => await EnsureRuntimeVolume(server, 998, 998);
    
    public static async Task EnsureRuntimeVolume(this Server server, int uid, int gid)
    {
        var wasMissing = !Directory.Exists(server.Configuration.GetRuntimeVolumePath());
        
        var volumeHelper = server.ServiceProvider.GetRequiredService<VolumeHelper>();

        // TODO: Make uid and gid dynamic loaded by temp config
        await volumeHelper.Ensure(server.Configuration.GetRuntimeVolumePath(), uid, gid);
        
        // Hook for virtual disks
        if (server.Configuration.Limits.UseVirtualDisk)
            await server.EnsureVirtualDisk();

        if (wasMissing)
        {
            server.FileSystem.Dispose();
            server.FileSystem = new(server.Configuration);
        }
    }

    public static async Task EnsureInstallVolume(this Server server) => await EnsureInstallVolume(server, 0, 0);

    public static async Task EnsureInstallVolume(this Server server, int uid, int gid)
    {
        var volumeHelper = server.ServiceProvider.GetRequiredService<VolumeHelper>();
        await volumeHelper.Ensure(server.Configuration.GetInstallVolumePath(), uid, gid);
    }
}