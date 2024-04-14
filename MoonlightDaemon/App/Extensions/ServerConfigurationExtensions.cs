using Docker.DotNet.Models;
using MoonCore.Helpers;
using MoonCore.Services;
using MoonlightDaemon.App.Configuration;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Extensions;

public static class ServerConfigurationExtensions
{
    public static CreateContainerParameters ToRuntimeContainerParameters(this ServerConfiguration configuration, ConfigService<ConfigV1> configService)
    {
        var container = new CreateContainerParameters();
        
        ApplySharedConfiguration(container, configuration, configService);

        // - Name
        var name = $"moonlight-runtime-{configuration.Id}";
        container.Name = name;
        container.Hostname = name;
        
        // - Image
        container.Image = configuration.Image.DockerImage;
        
        // - Env
        container.Env = configuration
            .GetEnvironmentVariables()
            .Select(x => $"{x.Key}={x.Value}")
            .ToList();

        // - Input, output & error streams and tty
        container.Tty = true;
        container.AttachStderr = true;
        container.AttachStdin = true;
        container.AttachStdout = true;
        container.OpenStdin = true;
        
        // -- Working directory
        container.WorkingDir = "/home/container";
        
        // - User
        //TODO: use config service
        container.User = $"998:998";

        // -- Mounts
        container.HostConfig.Mounts = new List<Mount>();
        
        container.HostConfig.Mounts.Add(new()
        {
            Source = configuration.GetRuntimeVolumePath(),
            Target = "/home/container",
            ReadOnly = false,
            Type = "bind"
        });
        
        // -- Ports
        var config = configService.Get();

        if (!configuration.Network.DisablePublic)
        {
            container.ExposedPorts = new Dictionary<string, EmptyStruct>();
            container.HostConfig.PortBindings = new Dictionary<string, IList<PortBinding>>();

            foreach (var port in configuration.Allocations.Select(x => x.Port))
            {
                container.ExposedPorts.Add($"{port}/tcp", new());
                container.ExposedPorts.Add($"{port}/udp", new());

                container.HostConfig.PortBindings.Add($"{port}/tcp", new List<PortBinding>
                {
                    new()
                    {
                        HostPort = port.ToString(),
                        HostIP = config.Docker.HostBindIp
                    }
                });
            
                container.HostConfig.PortBindings.Add($"{port}/udp", new List<PortBinding>
                {
                    new()
                    {
                        HostPort = port.ToString(),
                        HostIP = config.Docker.HostBindIp
                    }
                });
            }
        }

        return container;
    }
    
    public static CreateContainerParameters ToInstallContainerParameters(this ServerConfiguration configuration, ConfigService<ConfigV1> configService, string image)
    {
        var container = new CreateContainerParameters();
        
        ApplySharedConfiguration(container, configuration, configService);

        // - Name
        var name = $"moonlight-install-{configuration.Id}";
        container.Name = name;
        container.Hostname = name;
        
        // - Image
        container.Image = image;
        
        // - Env
        container.Env = configuration
            .GetEnvironmentVariables()
            .Select(x => $"{x.Key}={x.Value}")
            .ToList();
        
        // -- Working directory
        container.WorkingDir = "/mnt/server";
        
        // - User
        // Note: Some images might not work if we set a user here
        //container.User = "0:0";

        // -- Mounts
        container.HostConfig.Mounts = new List<Mount>();
        
        container.HostConfig.Mounts.Add(new()
        {
            Source = configuration.GetRuntimeVolumePath(),
            Target = "/mnt/server",
            ReadOnly = false,
            Type = "bind"
        });
        
        container.HostConfig.Mounts.Add(new()
        {
            Source = configuration.GetInstallVolumePath(),
            Target = "/mnt/install",
            ReadOnly = false,
            Type = "bind"
        });

        return container;
    }

    private static void ApplySharedConfiguration(CreateContainerParameters container, ServerConfiguration configuration, ConfigService<ConfigV1> configService)
    {
        var config = configService.Get();
        
        // - Input, output & error streams and tty
        container.Tty = true;
        container.AttachStderr = true;
        container.AttachStdin = true;
        container.AttachStdout = true;
        container.OpenStdin = true;
        
        /*
         * Unused, as we apply variables in the runtime/install specifics methods
         * 
        
        // - Environment
        container.Env = new List<string>();
        foreach (var variable in configuration.Variables)
        {
            container.Env.Add($"{variable.Key}={variable.Value}");
        }
        
        */
        
        // - Host config
        container.HostConfig = new HostConfig();

        // -- Cap drops
        container.HostConfig.CapDrop = new List<string>()
        {
            "setpcap", "mknod", "audit_write", "net_raw", "dac_override",
            "fowner", "fsetid", "net_bind_service", "sys_chroot", "setfcap"
        };

        // -- CPU limits
        container.HostConfig.CPUQuota = configuration.Limits.Cpu * 1000;
        container.HostConfig.CPUPeriod = 100000;
        container.HostConfig.CPUShares = 1024;

        // -- Memory and swap limits
        var memoryLimit = configuration.Limits.Memory;

        // The overhead multiplier gives the container a little bit more memory to prevent crashes
        var memoryOverhead = memoryLimit + (memoryLimit * config.Server.MemoryOverheadMultiplier);

        long swapLimit = -1;

        // If swap is enabled globally and not disabled on this server, set swap
        if (!configuration.Limits.DisableSwap && config.Server.EnableSwap)
            swapLimit = (long)(memoryOverhead + memoryOverhead * config.Server.SwapMultiplier);

        // Finalize limits by converting and updating the host config
        container.HostConfig.Memory = ByteSizeValue.FromMegaBytes((long)memoryOverhead, 1000).Bytes;
        container.HostConfig.MemoryReservation = ByteSizeValue.FromMegaBytes(memoryLimit, 1000).Bytes;
        container.HostConfig.MemorySwap = swapLimit == -1 ? swapLimit : ByteSizeValue.FromMegaBytes(swapLimit, 1000).Bytes;

        // -- Other limits
        container.HostConfig.BlkioWeight = 100;
        container.HostConfig.PidsLimit = configuration.Limits.PidsLimit;
        container.HostConfig.OomKillDisable = !configuration.Limits.EnableOomKill;
        
        // -- DNS
        container.HostConfig.DNS = config.Docker.DnsServers;

        // -- Tmpfs
        container.HostConfig.Tmpfs = new Dictionary<string, string>()
        {
            { "/tmp", $"rw,exec,nosuid,size={config.Docker.TmpfsSize}M" }
        };
        
        // -- Logging
        container.HostConfig.LogConfig = new()
        {
            Type = "json-file", // We need to use this provider, as the GetLogs endpoint needs it
            Config = new Dictionary<string, string>()
        };
        
        // - Labels
        container.Labels = new Dictionary<string, string>();
        
        container.Labels.Add("Software", "Moonlight-Panel");
        container.Labels.Add("ServerId", configuration.Id.ToString());
    }
    public static string GetRuntimeVolumePath(this ServerConfiguration configuration) => $"/var/lib/moonlight/volumes/{configuration.Id}";
    public static string GetInstallVolumePath(this ServerConfiguration configuration) => $"/var/lib/moonlight/install/{configuration.Id}";
    
    // Its recommended to use this method for the variables to include the dynamic environment variables like SERVER_PORT
    public static Dictionary<string, string> GetEnvironmentVariables(this ServerConfiguration configuration)
    {
        var result = new Dictionary<string, string>();

        // Default environment variables
        //TODO: Add timezone, add server ip
        result.Add("STARTUP", configuration.Image.StartupCommand);
        result.Add("SERVER_MEMORY", configuration.Limits.Memory.ToString());
        //result.Add("SERVER_IP", configService.Get().Docker.HostBindIp);
        result.Add("SERVER_PORT", configuration.MainAllocation.Port.ToString());

        
        // Handle additional allocation variables
        int i = 1;
        foreach (var additionalAllocation in configuration.Allocations)
        {
            result.Add($"ML_PORT_{i}", additionalAllocation.Port.ToString());
            i++;
        }

        // Copy variables as env vars
        foreach (var variable in configuration.Variables)
            result.Add(variable.Key, variable.Value);

        return result;
    }
}