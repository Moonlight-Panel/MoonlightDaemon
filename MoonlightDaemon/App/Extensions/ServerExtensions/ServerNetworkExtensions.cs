using Docker.DotNet;
using Docker.DotNet.Models;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerNetworkExtensions
{
    public static async Task ConnectToNetwork(this Server server)
    {
        var dockerClient = server.ServiceProvider.GetRequiredService<DockerClient>();

        var networkId = $"moonlight-network-{server.Configuration.Network.Id}";
        var containerId = $"moonlight-runtime-{server.Configuration.Id}";
        
        try
        {
            await dockerClient.Networks.InspectNetworkAsync(networkId);
        }
        catch (DockerNetworkNotFoundException)
        {
            await dockerClient.Networks.CreateNetworkAsync(new()
            {
                Name = networkId,
                Driver = "bridge"
            });
        }

        await dockerClient.Networks.ConnectNetworkAsync(networkId, new()
        {
            Container = containerId
        });
    }

    public static async Task DisconnectFromNetwork(this Server server)
    {
        var dockerClient = server.ServiceProvider.GetRequiredService<DockerClient>();

        var networkId = $"moonlight-network-{server.Configuration.Network.Id}";
        var containerId = $"moonlight-runtime-{server.Configuration.Id}";
        
        try
        {
            var network = await dockerClient.Networks.InspectNetworkAsync(networkId);

            // Disconnect from network
            await dockerClient.Networks.DisconnectNetworkAsync(networkId, new()
            {
                Container = containerId
            });
            
            // Delete network if no container is using it
            if (!network.Containers.Any())
                await dockerClient.Networks.DeleteNetworkAsync(networkId);
            
        }
        catch (DockerNetworkNotFoundException)
        {
            // Network does not exist? So we are done here
        }
    }
}