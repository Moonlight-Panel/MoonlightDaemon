using Docker.DotNet;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerRestoreExtensions
{
    public static async Task Reattach(this Server server)
    {
        var client = server.ServiceProvider.GetRequiredService<DockerClient>();
        
        // Attach to console. Attach stream to console stream
        var stream = await client.Containers.AttachContainerAsync($"moonlight-runtime-{server.Configuration.Id}", true, new()
        {
            Stderr = true,
            Stream = true,
            Stdin = true,
            Stdout = true
        });

        await server.Console.Attach(stream);
    }
}