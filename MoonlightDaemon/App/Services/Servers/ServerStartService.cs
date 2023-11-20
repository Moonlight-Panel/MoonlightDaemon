using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Services.Servers;

public class ServerStartService
{
    private readonly ConfigService ConfigService;
    private readonly DockerService DockerService;

    public ServerStartService(ConfigService configService, DockerService dockerService)
    {
        ConfigService = configService;
        DockerService = dockerService;
    }

    public async Task Perform(Server server)
    {
        var volumePath = PathBuilder.Dir(ConfigService.Get().Storage.VolumePath, server.Id.ToString());
        var containerName = $"moonlight-{server.Id}";
        
        // Creating volume for server
        Logger.Debug($"Ensuring volume for server {server.Id}");
        Directory.CreateDirectory(volumePath);
        
        // Check if container already exists
        if (await DockerService.ContainerExists(containerName))
        {
            Logger.Debug($"Removing existing container: {containerName}");
            await DockerService.RemoveContainer(containerName);
        }
        
        Logger.Debug($"Pulling docker image: {server.DockerImage}");
        await DockerService.PullDockerImage(server.DockerImage);
        
        Logger.Debug("Creating runtime container");
        await DockerService.CreateRuntimeContainer(
            containerName,
            server.DockerImage,
            server.Ports,
            server.Cpu,
            server.Memory,
            server.PidsLimit,
            new Dictionary<string, string>()
            {
                { volumePath, "/home/container/" }
            }
        );
    }
}