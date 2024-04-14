using Docker.DotNet;
using Docker.DotNet.Models;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerPowerExtensions
{
    public static async Task Start(this Server server)
    {
        await server.LockWhile(async () =>
        {
            if (!await server.State.CanTransitionTo(ServerState.Starting))
                return;
            
            // Get latest data from panel
            await server.Log("Fetching server data");

            var moonlightService = server.ServiceProvider.GetRequiredService<MoonlightService>();
            var configuration = await moonlightService.GetConfiguration(server);

            if (configuration != null)
                server.Configuration = configuration;
            else
                await server.Log("Failed to fetch server data. Trying to start anyways");

            // Ensure container is here
            await server.Recreate();
            
            // Parse configuration files
            var parseService = server.ServiceProvider.GetRequiredService<ParseService>();
            await parseService.Process(server);
            
            // Ensure file permissions after parsing file and modifying file permissions because of it 
            await server.EnsureRuntimeVolume(); //TODO: Reconsider where to put the chown command to reduce bash calls

            // Start container
            var client = server.ServiceProvider.GetRequiredService<DockerClient>();

            await client.Containers.StartContainerAsync($"moonlight-runtime-{server.Configuration.Id}", new());

            await server.State.TransitionTo(ServerState.Starting);
        });
    }

    public static async Task Stop(this Server server)
    {
        await server.LockWhile(async () =>
        {
            if (!await server.State.CanTransitionTo(ServerState.Stopping))
                return;

            var client = server.ServiceProvider.GetRequiredService<DockerClient>();

            var containerName = $"moonlight-runtime-{server.Configuration.Id}";
            
            var container =
                await client.Containers.InspectContainerSafeAsync(containerName);

            if (container == null)
            {
                await server.Log("Unable to inspect docker container");
                return;
            }

            if (container.State.Running)
            {
                var stopCmd = server.Configuration.Image.StopCommand;
                
                if (stopCmd.StartsWith("^"))
                {
                    
                    // Remove the "^"
                    var signal = stopCmd.Substring(1, stopCmd.Length - 1);
                    signal = signal.ToUpper();

                    // Handle ^C
                    if (signal == "C")
                        signal = "SIGINT";
                    
                    // Send signal
                    await client.Containers.KillContainerAsync(containerName, new ContainerKillParameters()
                    {
                        Signal = signal
                    });
                }
                else
                    await server.Console.SendCommand(stopCmd);
                
                await server.State.TransitionTo(ServerState.Stopping);
                return;
            }

            // This may not be called at any point of time, as the daemon should
            // automatically detected if a server is offline without this check here
            // but just to be sure, we have included it here
            if(await server.State.CanTransitionTo(ServerState.Offline))
                await server.State.TransitionTo(ServerState.Offline);
        });
    }

    public static async Task Kill(this Server server)
    {
        await server.LockWhile(async () =>
        {
            if(!await server.State.CanTransitionTo(ServerState.Offline))
                return;
            
            var containerName = $"moonlight-runtime-{server.Configuration.Id}";
            var client = server.ServiceProvider.GetRequiredService<DockerClient>();

            var container = await client.Containers.InspectContainerSafeAsync(containerName);
            
            if (container == null)
            {
                await server.Log("Unable to inspect docker container");
                return;
            }

            if (container.State.Running)
                await client.Containers.KillContainerAsync(containerName, new());

            await server.State.TransitionTo(ServerState.Offline);
        });
    }
}