using Docker.DotNet;
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

            // Ensure container is here
            await server.Recreate();
            
            // Parse configuration files
            var parseService = server.ServiceProvider.GetRequiredService<ParseService>();
            await parseService.Process(server);

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

            var container =
                await client.Containers.InspectContainerSafeAsync($"moonlight-runtime-{server.Configuration.Id}");

            if (container == null)
            {
                await server.Log("Unable to inspect docker container");
                return;
            }

            if (container.State.Running)
            {
                await server.Console.SendCommand(server.Configuration.Image.StopCommand);
                await server.State.TransitionTo(ServerState.Stopping);
                return;
            }

            // This may not be called at any point of time, as the daemon should
            // automatically detected if a server is offline without this check here
            // but just to be sure, we have included it here
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