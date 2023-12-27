using System.Text.RegularExpressions;
using Docker.DotNet;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Packets.Server;
using MoonlightDaemon.App.Services.Monitors;

namespace MoonlightDaemon.App.Services;

public class ServerService
{
    private readonly IServiceProvider ServiceProvider;
    private readonly ContainerMonitorService MonitorService;
    private readonly DockerClient DockerClient;
    private readonly MoonlightService MoonlightService;

    private readonly List<Server> Servers = new();
    private readonly Dictionary<int, DateTime> ConsoleSubscribers = new();

    public ServerService(
        ContainerMonitorService monitorService,
        DockerClient dockerClient,
        IServiceProvider serviceProvider,
        MoonlightService moonlightService)
    {
        ServiceProvider = serviceProvider;
        MoonlightService = moonlightService;
        MonitorService = monitorService;
        DockerClient = dockerClient;

        MonitorService.OnContainerEvent += async data =>
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

        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(1));

                lock (ConsoleSubscribers)
                {
                    var idsOverLimit = ConsoleSubscribers
                        .Where(x => (DateTime.UtcNow - x.Value).TotalMinutes > 15)
                        .Select(x => x.Key);

                    foreach (var i in idsOverLimit)
                    {
                        ConsoleSubscribers.Remove(i);
                        Logger.Debug($"Removed console subscriber for {i}");
                    }
                }
            }
        });
    }

    public Task AddFromConfiguration(ServerConfiguration configuration)
    {
        var stateMachine = new StateMachine<ServerState>(ServerState.Offline);

        stateMachine.AddTransition(ServerState.Offline, ServerState.Installing);
        stateMachine.AddTransition(ServerState.Installing, ServerState.Offline);
        stateMachine.AddTransition(ServerState.Offline, ServerState.Starting);
        stateMachine.AddTransition(ServerState.Starting, ServerState.Offline);
        stateMachine.AddTransition(ServerState.Starting, ServerState.Online);
        stateMachine.AddTransition(ServerState.Online, ServerState.Offline);
        stateMachine.AddTransition(ServerState.Starting, ServerState.Stopping);
        stateMachine.AddTransition(ServerState.Online, ServerState.Stopping);
        stateMachine.AddTransition(ServerState.Stopping, ServerState.Offline);
        stateMachine.AddTransition(ServerState.Stopping, ServerState.Join2Start);
        stateMachine.AddTransition(ServerState.Join2Start, ServerState.Offline);
        stateMachine.AddTransition(ServerState.Join2Start, ServerState.Starting);

        stateMachine.OnTransitioned += async state =>
        {
            await MoonlightService.SendWsPacket(new ServerStateUpdate()
            {
                Id = configuration.Id,
                State = state
            });
        };

        var server = new Server()
        {
            Configuration = configuration,
            ServiceProvider = ServiceProvider,
            State = stateMachine,
            LockHandle = new SemaphoreSlim(1, 1),
            Console = new()
        };

        server.Console.OnNewLogMessage += async message =>
        {
            // Handle online detection if the server is still starting
            if (server.State.State == ServerState.Starting)
            {
                if (Regex.Matches(message, server.Configuration.Image.OnlineDetection).Any())
                    await server.State.TransitionTo(ServerState.Online);
            }


            lock (ConsoleSubscribers)
            {
                if (!ConsoleSubscribers.ContainsKey(server.Configuration.Id))
                    return;
            }

            await MoonlightService.SendWsPacket(new ServerConsoleMessage()
            {
                Id = server.Configuration.Id,
                Message = message
            });
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
                await server.State.SetState(ServerState.Online);
                await server.Reattach();

                // Notify moonlight about the restored server state
                await MoonlightService.SendWsPacket(new ServerStateUpdate()
                {
                    Id = server.Configuration.Id,
                    State = ServerState.Online
                });

                Logger.Info($"Restored server {server.Configuration.Id} and reattached stream");
            }
        }
    }

    public Task Clear() // This function clears all servers and server events from current cache
    {
        lock (Servers)
        {
            foreach (var server in Servers)
            {
                server.Console.Close();
                server.Console.OnNewLogMessage.ClearSubscribers();
            }

            Servers.Clear();
        }

        // Dont clear subscribers here as they are needed
        // in a daemon restart and dont exist when its a clean start anyways
        // lock (ConsoleSubscribers)
        // ConsoleSubscribers.Clear();

        return Task.CompletedTask;
    }

    public async Task SubscribeToConsole(int id)
    {
        bool wasAlreadyAdded;

        lock (ConsoleSubscribers)
        {
            wasAlreadyAdded = ConsoleSubscribers
                .Where(x => (DateTime.UtcNow - x.Value).TotalMinutes < 15)
                .Any(x => x.Key == id);
        }

        // Add/update console subscriber time
        lock (ConsoleSubscribers)
            ConsoleSubscribers[id] = DateTime.UtcNow;

        // When the subscription is new, we want to stream all previous messages to restore console history
        if (!wasAlreadyAdded)
        {
            var server = await GetById(id);

            if (server == null)
                return;

            var messages = await server.Console.GetAllLogMessages();

            foreach (var message in messages)
            {
                await MoonlightService.SendWsPacket(new ServerConsoleMessage()
                {
                    Id = id,
                    Message = message
                });
            }
        }
    }
}