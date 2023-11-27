using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions;

public static class ServerDataExtensions
{
    public static EnvironmentData ToRuntimeEnvironmentData(this ServerData serverData)
    {
        return new()
        {
            Ports = serverData.Allocations.Select(x => x.Port).ToList(),
            DockerImage = serverData.Image.DockerImage,
            Container = new()
            {
                Name = $"moonlight-runtime-{serverData.Id}",
                Cpu = serverData.Limits.Cpu,
                Memory = serverData.Limits.Memory,
                Disk = serverData.Limits.Disk,
                DisableSwap = serverData.Limits.DisableSwap,
                PidsLimit = serverData.Limits.PidsLimit,
                EnableOomKill = serverData.Limits.EnableOomKill,
                WorkingDirectory = "/home/container"
            },
            Volumes = new()
            {
                {
                    $"/var/lib/moonlight/volumes/{serverData.Id}",
                    "/home/container"
                }
            },
            Variables = new()
            {
                {
                    "STARTUP",
                    serverData.Startup
                }
            }
        };
    }

    public static EnvironmentData ToInstallEnvironmentData(this ServerData serverData)
    {
        return new()
        {
            Container = new()
            {
                Name = $"moonlight-install-{serverData.Id}",
                Cpu = serverData.Limits.Cpu,
                Memory = serverData.Limits.Memory,
                Disk = serverData.Limits.Disk,
                DisableSwap = serverData.Limits.DisableSwap,
                PidsLimit = serverData.Limits.PidsLimit,
                EnableOomKill = serverData.Limits.EnableOomKill,
                OverrideCommand = "/bin/bash /mnt/install/install.sh",
                WorkingDirectory = "/mnt/server"
            },
            DockerImage = "moonlightpanel/images:installerjava",
            Ports = serverData.Allocations.Select(x => x.Port).ToList(),
            Volumes = new()
            {
                {
                    $"/var/lib/moonlight/volumes/{serverData.Id}",
                    "/mnt/server"
                },
                {
                    $"/var/lib/moonlight/install/{serverData.Id}",
                    "/mnt/install"
                }
            }
        };
    }
}