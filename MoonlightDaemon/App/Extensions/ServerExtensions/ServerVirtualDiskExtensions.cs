using MoonCore.Helpers;
using MoonCore.Services;
using MoonlightDaemon.App.Configuration;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerVirtualDiskExtensions
{
    public static async Task EnsureVirtualDisk(this Server server)
    {
        await server.Log("Checking virtual disk");
        
        var shellHelper = server.ServiceProvider.GetRequiredService<ShellHelper>();
        var configService = server.ServiceProvider.GetRequiredService<ConfigService<ConfigV1>>();

        var volumePath = server.Configuration.GetRuntimeVolumePath();
        var diskPath = $"/var/lib/moonlight/disks/{server.Configuration.Id}.img";
        var fileSystemType = configService.Get().Server.VirtualDiskFileSystem;

        if (!File.Exists(diskPath))
        {
            await server.Log("Creating virtual disk");
            await shellHelper.ExecuteCommand($"dd if=/dev/zero of={diskPath} bs=1M count={server.Configuration.Limits.Disk}");
            
            await server.Log("Formatting virtual disk");
            await shellHelper.ExecuteCommand($"mkfs -t {fileSystemType} {diskPath}");
        }
        
        if (string.IsNullOrEmpty(await shellHelper.ExecuteCommand($"findmnt {volumePath}", true)))
        {
            await server.Log("Mounting virtual disk");
            await shellHelper.ExecuteCommand($"mount -t auto -o loop {diskPath} {volumePath}");
        }
    }

    // This function does NOT delete any important data
    // it just unmounts the virtual disk
    // to delete a virtual disk, use the DeleteVirtualDisk() extension
    public static async Task DestroyVirtualDisk(this Server server)
    {
        var shellHelper = server.ServiceProvider.GetRequiredService<ShellHelper>();

        var volumePath = server.Configuration.GetRuntimeVolumePath();
        
        if (!string.IsNullOrEmpty(await shellHelper.ExecuteCommand($"findmnt {volumePath}", true)))
        {
            await server.Log("Unmounting virtual disk");

            try
            {
                await shellHelper.ExecuteCommand($"umount {volumePath}");
            }
            catch (ShellException e)
            {
                if (!e.Message.Contains("target is busy"))
                    throw;
                
                await server.Log("Unable to unmount virtual disk. Retrying forced");
                await shellHelper.ExecuteCommand($"fuser -mk {volumePath}");
                await shellHelper.ExecuteCommand($"umount {volumePath}");
            }
        }
    }

    public static Task DeleteVirtualDisk(this Server server)
    {
        var diskPath = $"/var/lib/moonlight/disks/{server.Configuration.Id}.img";
        
        if(File.Exists(diskPath))
            File.Delete(diskPath);
        
        return Task.CompletedTask;
    }
}