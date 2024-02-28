using Docker.DotNet;
using MoonCore.Attributes;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Http.Resources;

namespace MoonlightDaemon.App.Services;

[Singleton]
public class SystemService
{
    private readonly DockerClient DockerClient;
    private readonly SystemHelper SystemHelper;
    private readonly HardwareHelper HardwareHelper;

    public SystemService(DockerClient dockerClient, SystemHelper systemHelper, HardwareHelper hardwareHelper)
    {
        DockerClient = dockerClient;
        SystemHelper = systemHelper;
        HardwareHelper = hardwareHelper;
    }

    public async Task<SystemStatus> GetStatus()
    {
        var containers = await DockerClient.Containers.ListContainersAsync(new());
        
        var result = new SystemStatus()
        {
            Containers = containers.Count,
            Version = "Custom build",
            OperatingSystem = await SystemHelper.GetOsName(),
            Hardware = new()
            {
                Cores = await HardwareHelper.GetCpuDetails(),
                Disk = await HardwareHelper.GetDiskDetails(),
                Memory = await HardwareHelper.GetMemoryDetails(),
                Uptime = await HardwareHelper.GetUptime()
            }
        };

        return result;
    }
}