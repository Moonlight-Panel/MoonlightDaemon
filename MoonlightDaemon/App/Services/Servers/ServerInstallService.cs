using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Services.Servers;

public class ServerInstallService
{
    private readonly VolumeHelper VolumeHelper;
    private readonly ConfigService ConfigService;
    private readonly DockerService DockerService;

    public ServerInstallService(VolumeHelper volumeHelper, DockerService dockerService, ConfigService configService)
    {
        VolumeHelper = volumeHelper;
        DockerService = dockerService;
        ConfigService = configService;
    }

    public async Task Perform(Server server)
    {
        var containerName = $"moonlight-{server.Id}";
        var config = ConfigService.Get();

        // Ensure server and install volume is created and the permissions are set correctly
        Logger.Debug($"Ensuring server and install volume is ready for server {server.Id}");
        await VolumeHelper.EnsureServerVolume(server);
        await VolumeHelper.EnsureInstallVolume(server);
        
        
    }

    public async Task Complete(Server server)
    {
        
    }
}