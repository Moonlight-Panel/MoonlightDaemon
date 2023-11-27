using Docker.DotNet;
using Docker.DotNet.Models;

namespace MoonlightDaemon.App.Services;

public class DockerService
{
    private readonly ConfigService ConfigService;
    private readonly DockerClient Client;

    public DockerService(ConfigService configService)
    {
        ConfigService = configService;
        
        Client = new DockerClientConfiguration(
                new Uri(configService.Get().Docker.Socket))
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

    public async Task RemoveContainer(string name) => await Client.Containers.RemoveContainerAsync(name, new());

    public async Task<CreateContainerResponse> CreateContainer(CreateContainerParameters parameters) =>
        await Client.Containers.CreateContainerAsync(parameters);

    public async Task StartContainer(string name) => await Client.Containers.StartContainerAsync(name, new());

    public async Task KillContainer(string name) => await Client.Containers.KillContainerAsync(name, new());

    public async Task StopContainer(string name) => await Client.Containers.StopContainerAsync(name,
        new()
        {
            WaitBeforeKillSeconds = 3600
        });

    public async Task<MultiplexedStream> AttachContainer(string name) => await Client.Containers.AttachContainerAsync(
        name, true, new ContainerAttachParameters()
        {
            Stderr = true,
            Stream = true,
            Stdout = true,
            Stdin = true
        });

    public async Task PullImage(string image, Action<JSONMessage>? onProgress = null)
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
            new Progress<JSONMessage>(message =>
            {
                if(onProgress != null)
                    onProgress.Invoke(message);
            }));
    }
}