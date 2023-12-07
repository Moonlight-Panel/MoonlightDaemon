using Docker.DotNet;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Services.Monitors;

namespace MoonlightDaemon.App.Services;

public class ServerService
{
    private readonly IServiceProvider ServiceProvider;
    private readonly ContainerMonitorService MonitorService;
    private readonly DockerClient DockerClient;
    private readonly List<Server> Servers = new();

    public ServerService(
        ContainerMonitorService monitorService,
        DockerClient dockerClient,
        IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        MonitorService = monitorService;
        DockerClient = dockerClient;

        MonitorService.OnContainerEvent += async (_, data) =>
        {
            if (data.Action != "die") // Only listen to callbacks we are interested in
                return;

            var container = await DockerClient.Containers.InspectContainerSafeAsync(data.Id);

            if (container == null) // If we are unable to inspect the container, we ignore the event
                return;

            // When the required labels are missing, we ignore the event
            if (!container.Config.Labels.ContainsKey("Software") || !container.Config.Labels.ContainsKey("ServerId"))
                return;

            // If its not a moonlight container, we ignore it
            if (container.Config.Labels["Software"] != "Moonlight-Panel")
                return;

            // If we are unable to parse the server id, we ignore the event
            if (!int.TryParse(container.Config.Labels["ServerId"], out int serverId))
                return;

            var server = await GetById(serverId);

            // If we are unable to find a server with the id, we ignore the event
            if (server == null)
                return;

            await server.HandleExited();
        };
    }

    public Task AddFromConfiguration(ServerConfiguration configuration)
    {
        var stateMachine = new StateMachine<ServerState>(ServerState.Offline);

        stateMachine.AddTransition(ServerState.Offline, ServerState.Installing);
        stateMachine.AddTransition(ServerState.Installing, ServerState.Offline);
        stateMachine.AddTransition(ServerState.Offline, ServerState.Starting);
        stateMachine.AddTransition(ServerState.Starting, ServerState.Offline);
        stateMachine.AddTransition(ServerState.Starting, ServerState.Running);
        stateMachine.AddTransition(ServerState.Running, ServerState.Offline);
        stateMachine.AddTransition(ServerState.Starting, ServerState.Stopping);
        stateMachine.AddTransition(ServerState.Running, ServerState.Stopping);
        stateMachine.AddTransition(ServerState.Stopping, ServerState.Offline);
        stateMachine.AddTransition(ServerState.Stopping, ServerState.Join2Start);
        stateMachine.AddTransition(ServerState.Join2Start, ServerState.Offline);
        stateMachine.AddTransition(ServerState.Join2Start, ServerState.Starting);

        var server = new Server()
        {
            Configuration = configuration,
            ServiceProvider = ServiceProvider,
            State = stateMachine,
            LockHandle = new SemaphoreSlim(1, 1),
            Console = new()
        };

        lock (Servers)
            Servers.Add(server);

        return Task.CompletedTask;
    }

    public Task<Server?> GetById(int id)
    {
        lock (Servers)
        {
            var result = Servers.FirstOrDefault(x => x.Configuration.Id == id);
            return Task.FromResult(result);
        }
    }

    public async Task Restore()
    {
        Logger.Info("Restoring docker containers");

        var containers = await DockerClient.Containers.ListContainersAsync(new());

        if (containers == null)
        {
            Logger.Warn("Unable to get container list from docker");
            return;
        }

        foreach (var container in containers)
        {
            // When the required labels are missing, we ignore the container
            if (!container.Labels.ContainsKey("Software") || !container.Labels.ContainsKey("ServerId"))
                return;

            // If its not a moonlight container, we ignore it
            if (container.Labels["Software"] != "Moonlight-Panel")
                return;

            // If we are unable to parse the server id, we ignore the container
            if (!int.TryParse(container.Labels["ServerId"], out int serverId))
                return;

            var server = await GetById(serverId);

            // If we are unable to find a server with the id, we ignore the event
            if (server == null)
                return;

            // At this point we know the id and the meta data of the container
            // so we can start to restore the container
            if (container.State == "running")
            {
                await server.State.SetState(ServerState.Running);
                await server.Reattach();

                Logger.Info($"Restored server {server.Configuration.Id} and reattached stream");
            }
        }
    }

    public Task Clear()
    {
        lock (Servers)
            Servers.Clear();
        
        //TODO: Remove websocket connections etc
        
        return Task.CompletedTask;
    }
}