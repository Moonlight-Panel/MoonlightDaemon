using Docker.DotNet.Models;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Services.Servers;

public class ServerStartService
{
    private readonly ConfigService ConfigService;
    private readonly DockerService DockerService;
    private readonly VolumeHelper VolumeHelper;

    public ServerStartService(ConfigService configService, DockerService dockerService, VolumeHelper volumeHelper)
    {
        ConfigService = configService;
        DockerService = dockerService;
        VolumeHelper = volumeHelper;
    }

    public async Task Perform(Server server)
    {
        var containerName = $"moonlight-{server.Id}";
        var config = ConfigService.Get();

        // Ensure server volume is created and the permissions are set correctly
        Logger.Debug($"Ensuring server volume is ready for server {server.Id}");
        await VolumeHelper.EnsureServerVolume(server);

        // Check if container already exists, if yes we remove it to ensure every option is
        // set correctly by recreating it 
        if (await DockerService.ContainerExists(containerName))
        {
            Logger.Debug($"Removing existing container: {containerName}");
            await DockerService.RemoveContainer(containerName);
        }

        // Pulling down the docker image used to start the server
        Logger.Debug($"Pulling docker image: {server.Image.DockerImage}");
        await DockerService.PullDockerImage(server.Image.DockerImage, msg =>
        {
            var percent = Math.Round(msg.Progress.Current / msg.Progress.Total * 100D);
            Logger.Debug($"[ {percent}% ] {msg.Status}");
        });


        // Configure docker container
        Logger.Debug($"Configuring container {containerName}");
        CreateContainerParameters container = new();

        #region Configure container

        // - Names
        container.Name = containerName;
        container.Domainname = containerName;

        // - Labels
        container.Labels = new Dictionary<string, string>();
        container.Labels.Add("Software", "Moonlight-Panel");
        container.Labels.Add("ServerId", server.Id.ToString());

        // - Image
        container.Image = server.Image.DockerImage;

        // - Input, output & error streams and tty
        container.Tty = true;
        container.AttachStderr = true;
        container.AttachStdin = true;
        container.AttachStdout = true;

        // - User
        var tempConfig = ConfigService.GetTemp();
        container.User = $"{tempConfig.Uid}:{tempConfig.Gid}";

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
        hostConfig.CPUQuota = server.Limits.Cpu * 1000;
        hostConfig.CPUPeriod = 100000;
        hostConfig.CPUShares = 1024;

        // -- Memory and swap limits
        var memoryLimit = server.Limits.Memory;

        // The overhead multiplier gives the container a little bit more memory to prevent crashes
        var memoryOverhead = memoryLimit + (memoryLimit * config.Server.MemoryOverheadMultiplier);

        long swapLimit = -1;

        // If swap is enabled globally and not disabled on this server, set swap
        if (!server.Limits.DisableSwap && config.Server.EnableSwap)
            swapLimit = (long)(memoryOverhead + memoryOverhead * config.Server.SwapMultiplier);

        // Finalize limits by converting and updating the host config
        hostConfig.Memory = ByteSizeValue.FromMegaBytes((long)memoryOverhead, 1000).Bytes;
        hostConfig.MemoryReservation = ByteSizeValue.FromMegaBytes(memoryLimit, 1000).Bytes;
        hostConfig.MemorySwap = swapLimit == -1 ? swapLimit : ByteSizeValue.FromMegaBytes(swapLimit, 1000).Bytes;

        // -- Other limits
        hostConfig.BlkioWeight = 100;
        hostConfig.PidsLimit = server.Limits.Pids;
        hostConfig.OomKillDisable = !server.Limits.EnableOomKill;

        // -- DNS
        hostConfig.DNS = config.Docker.DnsServers;

        // -- Tmpfs
        hostConfig.Tmpfs = new Dictionary<string, string>()
        {
            { "/tmp", $"rw,exec,nosuid,size={config.Docker.TmpfsSize}M" }
        };

        // -- Mount
        // Additional mounts can be added here later
        hostConfig.Mounts = new List<Mount>()
        {
            new()
            {
                Source = PathBuilder.Dir(config.Storage.VolumePath, server.Id.ToString()),
                Target = "/home/container",
                ReadOnly = false,
                Type = "bind"
            }
        };

        // -- Logging
        hostConfig.LogConfig = new()
        {
            Type = "local",
            Config = new Dictionary<string, string>()
        };

        // -- Ports
        container.ExposedPorts = new Dictionary<string, EmptyStruct>();
        hostConfig.PortBindings = new Dictionary<string, IList<PortBinding>>();

        foreach (var allocation in server.Allocations)
        {
            container.ExposedPorts.Add($"{allocation.Port}/tcp", new());
            container.ExposedPorts.Add($"{allocation.Port}/udp", new());

            hostConfig.PortBindings.Add($"{allocation.Port}/tcp", new List<PortBinding>
            {
                new()
                {
                    HostPort = allocation.Port.ToString(),
                    HostIP = config.Docker.HostBindIp
                }
            });
            
            hostConfig.PortBindings.Add($"{allocation.Port}/udp", new List<PortBinding>
            {
                new()
                {
                    HostPort = allocation.Port.ToString(),
                    HostIP = config.Docker.HostBindIp
                }
            });
        }

        #endregion

        Logger.Debug($"Creating container {containerName}");
        await DockerService.CreateContainer(container);
        
        Logger.Debug($"Starting container {containerName}");
        await DockerService.StartContainer(containerName);
    }
}