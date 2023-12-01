using Docker.DotNet;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerDestroyExtensions
{
    // This function does NOT delete any important data
    // it just removes the container and cleans up some unused stuff
    // to delete a server and its volume, use the Delete() extension
    // method
    public static async Task Destroy(this Server server)
    {
        await server.LockWhile(async () =>
        {
            var containerName = $"moonlight-runtime-{server.Configuration.Id}";
            var client = server.ServiceProvider.GetRequiredService<DockerClient>();

            var container = await client.Containers.InspectContainerSafeAsync(containerName);

            // We can ignore the container if we are unable to find it using inspect
            if (container == null)
                return;

            if (container.State.Running)
                await client.Containers.KillContainerAsync(containerName, new());

            await client.Containers.RemoveContainerAsync(containerName, new());
        });
    }
}