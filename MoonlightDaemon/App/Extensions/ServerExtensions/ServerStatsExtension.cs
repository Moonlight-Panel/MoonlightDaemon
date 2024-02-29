using Docker.DotNet;
using Docker.DotNet.Models;
using MoonCore.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Packets;
using Newtonsoft.Json;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerStatsExtension
{
    public static async Task<ServerStats?> GetStats(this Server server)
    {
        var client = server.ServiceProvider.GetRequiredService<DockerClient>();
        var containerName = $"moonlight-runtime-{server.Configuration.Id}";

        ServerStats? result = default;
            
        await client.Containers.GetContainerStatsAsync(containerName, new()
        {
            Stream = false
        }, new Progress<ContainerStatsResponse>(response =>
        {
            result = response.ToServerStats();
        }), CancellationToken.None);

        return result;
    }

    public static async Task<CancellationTokenSource> GetStatsStream(this Server server, Func<ServerStats, Task> handle)
    {
        var cancellation = new CancellationTokenSource();
        
        var client = server.ServiceProvider.GetRequiredService<DockerClient>();
        var containerName = $"moonlight-runtime-{server.Configuration.Id}";
            
        await client.Containers.GetContainerStatsAsync(containerName, new()
        {
            OneShot = false,
            Stream = true
        }, new Progress<ContainerStatsResponse>(async response =>
        {
            try
            {
                await handle.Invoke(response.ToServerStats());
            }
            catch (Exception e)
            {
                Logger.Info(JsonConvert.SerializeObject(response));
            }
        }), cancellation.Token);

        return cancellation;
    }
}