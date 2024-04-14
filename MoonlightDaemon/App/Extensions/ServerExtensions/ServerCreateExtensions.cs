using Docker.DotNet;
using Docker.DotNet.Models;
using MoonCore.Services;
using MoonlightDaemon.App.Configuration;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerCreateExtensions
{
    public static async Task Recreate(this Server server)
    {
        var containerName = $"moonlight-runtime-{server.Configuration.Id}";
        var client = server.ServiceProvider.GetRequiredService<DockerClient>();

        await server.Log("Checking volumes and file permissions");
        await server.EnsureRuntimeVolume();
        
        // Remove existing container if it exists
        var existingContainer = await client.Containers.InspectContainerSafeAsync(containerName);
            
        if(existingContainer != null && existingContainer.State.Running)
            return;

        if (existingContainer != null)
        {
            await server.Log("Removing existing container");
            await client.Containers.RemoveContainerAsync(containerName, new());
        }

        if (server.Configuration.Image.PullDockerImage)
        {
            await server.Log("Downloading docker image");
            await server.EnsureImageExists(server.Configuration.Image.DockerImage);
            await server.Log("Downloaded docker image");
        }
        else
            await server.Log("Skipping docker image download");

        var configService = server.ServiceProvider.GetRequiredService<ConfigService<ConfigV1>>();
        var container = server.Configuration.ToRuntimeContainerParameters(configService);
        
        await server.Log("Creating container");
        await client.Containers.CreateContainerAsync(container);
        
        // Check and connect container to network
        if (server.Configuration.Network.Enable)
        {
            await server.Log("Ensuring network connection");
            await server.ConnectToNetwork();
        }
        
        // Attach to console. Attach stream to console stream
        var stream = await client.Containers.AttachContainerAsync(containerName, true, new()
        {
            Stderr = true,
            Stream = true,
            Stdin = true,
            Stdout = true
        });

        await server.Console.Attach(stream);
    }

    // We dont use the server image here as we want to use this function for the installer here as well
    public static async Task EnsureImageExists(this Server server, string image)
    {
        var client = server.ServiceProvider.GetRequiredService<DockerClient>();
        
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
        
        await client.Images.CreateImageAsync(new ImagesCreateParameters()
            {
                FromImage = name,
                Tag = tag
            }, new AuthConfig(),
            new Progress<JSONMessage>(async message =>
            {
                if(message != null && message.Progress != null && message.Progress.Total != 0)
                {
                    var percent = Math.Round((float)message.Progress.Current / message.Progress.Total * 100);
                    await server.Log($"[ {percent}% ] {message.Status}");
                }
            }));
    }
}