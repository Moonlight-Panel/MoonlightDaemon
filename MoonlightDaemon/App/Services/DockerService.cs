using Docker.DotNet;
using Docker.DotNet.Models;
using MoonlightDaemon.App.Helpers;

namespace MoonlightDaemon.App.Services;

public class DockerService
{
    private readonly ConfigService ConfigService;
    private readonly MoonlightService MoonlightService;
    private readonly ShellHelper ShellHelper;

    private readonly DockerClient Client;

    public DockerService(ConfigService configService, MoonlightService moonlightService, ShellHelper shellHelper)
    {
        ConfigService = configService;
        MoonlightService = moonlightService;
        ShellHelper = shellHelper;

        Client = new DockerClientConfiguration(
                new Uri("unix:///var/run/docker.sock"))
            .CreateClient();
    }

    public async Task<bool> ContainerExists(string name)
    {
        try
        {
            await Client.Containers.InspectContainerAsync(name);
            return true;
        }
        catch (DockerContainerNotFoundException)
        {
            return false;
        }
    }

    public async Task<bool> IsContainerRunning(string name)
    {
        var result = await Client.Containers.InspectContainerAsync(name);
        return result.State.Running;
    }

    public async Task RemoveContainer(string name)
    {
        await Client.Containers.RemoveContainerAsync(name, new()
        {
            Force = false,
            RemoveLinks = false,
            RemoveVolumes = false
        });
    }

    public async Task PullDockerImage(string image)
    {
        var parts = image.Split(":");

        string name, tag;

        if (parts.Length < 2)
        {
            name = parts[0];
            tag = "latest";
        }
        else
        {
            name = parts[0];
            tag = parts[1];
        }
        
        await Client.Images.CreateImageAsync(new ImagesCreateParameters()
            {
                FromImage = name,
                Tag = tag
            }, new AuthConfig(),
            new Progress<JSONMessage>((msg) => { Logger.Debug("Pull output: " + msg.Status); }));
    }

    public async Task CreateRuntimeContainer(string name, string image, List<int> ports, int cpu, int memory,
        int pidsLimit, Dictionary<string, string> mounts)
    {
        var config = ConfigService.Get();
        var hostConfig = new HostConfig();

        // Ports
        var exposedPorts = new Dictionary<string, EmptyStruct>();
        hostConfig.PortBindings = new Dictionary<string, IList<PortBinding>>();

        foreach (var port in ports)
        {
            exposedPorts.Add($"{port}/tcp", new());
            exposedPorts.Add($"{port}/udp", new());

            hostConfig.PortBindings.Add($"{port}/tcp", new List<PortBinding>()
            {
                new()
                {
                    HostPort = port.ToString(),
                    HostIP = config.Docker.HostBindIp
                }
            });

            hostConfig.PortBindings.Add($"{port}/udp", new List<PortBinding>()
            {
                new()
                {
                    HostPort = port.ToString(),
                    HostIP = ConfigService.Get().Docker.HostBindIp
                }
            });
        }

        // Memory and swap

        var memoryLimit = memory;
        // The overhead multiplier gives the container a little bit more memory to prevent crashes
        var memoryOverhead = memoryLimit + (memoryLimit * config.Server.MemoryOverheadMultiplier);
        var swapLimit = config.Server.EnableSwap
            ? memoryOverhead + (memoryOverhead * config.Server.SwapMultiplier)
            : -1;

        hostConfig.Memory = ByteSizeValue.FromMegaBytes((long)memoryOverhead, 1000).Bytes;
        hostConfig.MemoryReservation = ByteSizeValue.FromMegaBytes(memoryLimit, 1000).Bytes;
        hostConfig.MemorySwap =
            swapLimit == -1 ? (long)swapLimit : ByteSizeValue.FromMegaBytes((long)swapLimit, 1000).Bytes;

        // CPU limit
        hostConfig.CPUQuota = cpu * 1000;
        hostConfig.CPUPeriod = 100000;
        hostConfig.CPUShares = 1024;

        // Other resource config options
        hostConfig.BlkioWeight = 100;
        hostConfig.PidsLimit = pidsLimit;
        hostConfig.OomKillDisable = true; // TODO: make config option

        // Tmpfs
        hostConfig.Tmpfs = new Dictionary<string, string>()
        {
            {
                "/tmp", $"rw,exec,nosuid,size={config.Docker.TmpfsSize}M"
            }
        };

        // Cap drops
        hostConfig.CapDrop = new List<string>()
        {
            "setpcap", "mknod", "audit_write", "net_raw", "dac_override",
            "fowner", "fsetid", "net_bind_service", "sys_chroot", "setfcap"
        };

        hostConfig.ReadonlyRootfs = true;

        // Mounts
        var mountList = mounts.Select(x => new Mount()
        {
            Source = x.Key,
            Target = x.Value,
            ReadOnly = false,
            Type = "bind"
        }).ToList();

        hostConfig.Mounts = mountList;

        Logger.Debug("Chowning directory mounts");
        
        foreach (var mount in mounts)
        {
            await ShellHelper.ExecuteCommand($"chown -R {MoonlightService.Uid}:{MoonlightService.Gid} {mount.Key}");
        }
        
        Logger.Debug("Chowned directory mounts");
        
        // DNS
        hostConfig.DNS = config.Docker.DnsServers;

        // Log config
        hostConfig.LogConfig = new()
        {
            Type = "local",
            Config = new Dictionary<string, string>()
        };

        // Create the container
        await Client.Containers.CreateContainerAsync(new()
        {
            Name = name,
            WorkingDir = "/home/container/",
            Image = image,
            ExposedPorts = exposedPorts,
            HostConfig = hostConfig,
            AttachStderr = true,
            AttachStdin = true,
            AttachStdout = true,
            Tty = true,
            OpenStdin = true,
            Hostname = name,
            Env = new List<string>()
            {
                "STARTUP=java -jar server.jar"
            },
            User = $"{MoonlightService.Uid}:{MoonlightService.Gid}"
        });
    }
}