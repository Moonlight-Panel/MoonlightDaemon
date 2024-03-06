using Docker.DotNet;
using Docker.DotNet.Models;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerStatsExtension
{
    public static async Task<ServerStats> GetStats(this Server server)
    {
        var client = server.ServiceProvider.GetRequiredService<DockerClient>();
        var containerName = $"moonlight-runtime-{server.Configuration.Id}";

        ServerStats result = new();
        TaskCompletionSource completion = new();
            
        await client.Containers.GetContainerStatsAsync(containerName, new()
        {
            Stream = false
        }, new Progress<ContainerStatsResponse>(response =>
        {
            try
            {
                result = response.ToServerStats();
                completion.SetResult();
            }
            catch (Exception)
            {
                completion.SetResult();
            }
        }), CancellationToken.None);

        await completion.Task;

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
            catch (NullReferenceException) { /* sometimes the last state update has empty components. thats why we ignore it here */ }
        }), cancellation.Token);

        return cancellation;
    }
}