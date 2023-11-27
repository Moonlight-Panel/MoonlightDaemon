using Docker.DotNet.Models;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Extensions;

public static class EnvironmentDataExtensions
{
    public static CreateContainerParameters ToContainerParameter(this EnvironmentData environmentConfig, ConfigService configService)
    {
        var config = configService.Get();
        var container = new CreateContainerParameters();

        // - Name
        container.Name = environmentConfig.Container.Name;
        container.Hostname = environmentConfig.Container.Name;
        
        // - Image
        container.Image = environmentConfig.DockerImage;

        // - Input, output & error streams and tty
        container.Tty = true;
        container.AttachStderr = true;
        container.AttachStdin = true;
        container.AttachStdout = true;
        container.OpenStdin = true;
        
        // -- Working directory
        container.WorkingDir = environmentConfig.Container.WorkingDirectory;
        
        // - User
        container.User = $"{environmentConfig.Uid}:{environmentConfig.Gid}";
        
        // - Environment
        container.Env = new List<string>();
        foreach (var variable in environmentConfig.Variables)
        {
            container.Env.Add($"{variable.Key}={variable.Value}");
        }
        
        // - Host config
        var hostConfig = new HostConfig();
        container.HostConfig = hostConfig;

        // -- Cap drops
        hostConfig.CapDrop = new List<string>()
        {
            "setpcap", "mknod", "audit_write", "net_raw", "dac_override",
            "fowner", "fsetid", "net_bind_service", "sys_chroot", "setfcap"
        };

        // -- CPU limits
        hostConfig.CPUQuota = environmentConfig.Container.Cpu * 1000;
        hostConfig.CPUPeriod = 100000;
        hostConfig.CPUShares = 1024;

        // -- Memory and swap limits
        var memoryLimit = environmentConfig.Container.Memory;

        // The overhead multiplier gives the container a little bit more memory to prevent crashes
        var memoryOverhead = memoryLimit + (memoryLimit * config.Server.MemoryOverheadMultiplier);

        long swapLimit = -1;

        // If swap is enabled globally and not disabled on this server, set swap
        if (!environmentConfig.Container.DisableSwap && config.Server.EnableSwap)
            swapLimit = (long)(memoryOverhead + memoryOverhead * config.Server.SwapMultiplier);

        // Finalize limits by converting and updating the host config
        hostConfig.Memory = ByteSizeValue.FromMegaBytes((long)memoryOverhead, 1000).Bytes;
        hostConfig.MemoryReservation = ByteSizeValue.FromMegaBytes(memoryLimit, 1000).Bytes;
        hostConfig.MemorySwap = swapLimit == -1 ? swapLimit : ByteSizeValue.FromMegaBytes(swapLimit, 1000).Bytes;

        // -- Other limits
        hostConfig.BlkioWeight = 100;
        hostConfig.PidsLimit = environmentConfig.Container.PidsLimit;
        hostConfig.OomKillDisable = !environmentConfig.Container.EnableOomKill;
        
        // -- DNS
        hostConfig.DNS = config.Docker.DnsServers;

        // -- Tmpfs
        hostConfig.Tmpfs = new Dictionary<string, string>()
        {
            { "/tmp", $"rw,exec,nosuid,size={config.Docker.TmpfsSize}M" }
        };

        // -- Mounts
        hostConfig.Mounts = new List<Mount>();
        foreach (var mount in environmentConfig.Volumes)
        {
            hostConfig.Mounts.Add(new()
            {
                Source = mount.Key,
                Target = mount.Value,
                ReadOnly = false,
                Type = "bind"
            });
        }
        
        // -- Logging
        hostConfig.LogConfig = new()
        {
            Type = "local",
            Config = new Dictionary<string, string>()
        };
        
        // -- Ports
        container.ExposedPorts = new Dictionary<string, EmptyStruct>();
        hostConfig.PortBindings = new Dictionary<string, IList<PortBinding>>();

        foreach (var port in environmentConfig.Ports)
        {
            container.ExposedPorts.Add($"{port}/tcp", new());
            container.ExposedPorts.Add($"{port}/udp", new());

            hostConfig.PortBindings.Add($"{port}/tcp", new List<PortBinding>
            {
                new()
                {
                    HostPort = port.ToString(),
                    HostIP = config.Docker.HostBindIp
                }
            });
            
            hostConfig.PortBindings.Add($"{port}/udp", new List<PortBinding>
            {
                new()
                {
                    HostPort = port.ToString(),
                    HostIP = config.Docker.HostBindIp
                }
            });
        }

        return container;
    }
}