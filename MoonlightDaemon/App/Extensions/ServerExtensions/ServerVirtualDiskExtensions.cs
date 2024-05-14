using Mono.Unix;
using Mono.Unix.Native;
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
            
            server.FileSystem.Dispose();

            try
            {
                await shellHelper.ExecuteCommand($"umount {volumePath}");
            }
            catch (ShellException e)
            {
                if (!e.Message.Contains("target is busy"))
                    throw;
                
                await server.Log("Unable to unmount virtual disk. Retrying forced");
                
                // If we land up here we might still have open file descriptors for this virtual disk
                // so we need to go through every fd pointing to it from our own process in order to ensure its
                // not our fault and try to resolve it if we are the cause the umount does not work

                int counter = 0;
                foreach (var fdSymlink in Directory.GetDirectories("/proc/self/fd"))
                {
                    var fdTarget = UnixPath.ReadLink(fdSymlink);
                    
                    if(!fdTarget.StartsWith(server.Configuration.GetRuntimeVolumePath()))
                        continue;
                    
                    var parts = fdSymlink.Split("/");
                    
                    if(parts.Length == 0)
                        continue;

                    var fdString = parts.Last();
                    
                    if(!int.TryParse(fdString, out int fd))
                        continue;
                    
                    Syscall.close(fd);
                    counter++;
                }
                
                Logger.Info($"Closed all file descriptors using a path inside the virtual disk. Trying to umount now ({counter})");
                
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