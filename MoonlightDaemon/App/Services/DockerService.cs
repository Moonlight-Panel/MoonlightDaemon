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
        try
        {
            var result = await Client.Containers.InspectContainerAsync(name);
            return result.State.Running;
        }
        catch (DockerContainerNotFoundException)
        {
            return false;
        }
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

    public async Task PullDockerImage(string image, Action<JSONMessage>? onProgress = null)
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

    public async Task CreateContainer(CreateContainerParameters parameters)
    {
        await Client.Containers.CreateContainerAsync(parameters);
    }

    public async Task StartContainer(string id)
    {
        await Client.Containers.StartContainerAsync(id, new());
    }
}