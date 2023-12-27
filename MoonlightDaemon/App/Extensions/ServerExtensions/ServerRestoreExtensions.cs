using Docker.DotNet;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerRestoreExtensions
{
    public static async Task Reattach(this Server server)
    {
        var client = server.ServiceProvider.GetRequiredService<DockerClient>();
        var containerName = $"moonlight-runtime-{server.Configuration.Id}";

        // Load previous logs
        var logStream = await client.Containers.GetContainerLogsAsync(containerName, true, new ()
        {
            ShowStderr = true,
            ShowStdout = true
        });

        // Read and restore log messages
        if (logStream != null)
        {
            var outputStreams = await logStream.ReadOutputToEndAsync(CancellationToken.None);
            
            // Process stdout
            foreach (var line in outputStreams.stdout.Split("\n").Where(x => !string.IsNullOrEmpty(x)))
                await server.Console.WriteLine(line);
            
            // Then stderr
            foreach (var line in outputStreams.stderr.Split("\n").Where(x => !string.IsNullOrEmpty(x)))
                await server.Console.WriteLine(line);
            
            // With ignoring empty lines to reduce packets
        }
        
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