using System.Text.RegularExpressions;
using Docker.DotNet;
using MoonCore.Attributes;
using MoonCore.Helpers;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Http.Resources;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Packets;
using MoonlightDaemon.App.Services.Monitors;

namespace MoonlightDaemon.App.Services;

[Singleton]
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
            // 
        };

        var server = new Server()
        {
            Configuration = configuration,
            ServiceProvider = ServiceProvider,
            State = stateMachine,
            LockHandle = new SemaphoreSlim(1, 1),
            Console = new(),
            FileSystem = new(configuration) // TODO: Ensure stuff like the virtual disk is mounted
        };

        server.Console.OnNewLogMessage += async message =>
        {   
            // Handle online detection if the server is still starting
            if (server.State.State == ServerState.Starting)
            {
                if (Regex.Matches(message, server.Configuration.Image.OnlineDetection).Any())
                    await server.State.TransitionTo(ServerState.Online);
            }
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

                Logger.Info($"Restored server {server.Configuration.Id} and reattached stream");
            }
        }
    }

    public async Task Clear() // This function clears all servers and server events from current cache
    {
        Server[] servers;

        lock (Servers) // Create a copy from the server cache
            servers = Servers.ToArray();

        foreach (var server in servers)
            await ClearServer(server);

        // Dont clear subscribers here as they are needed
        // in a daemon restart and dont exist when its a clean start anyways
        // lock (ConsoleSubscribers)
        // ConsoleSubscribers.Clear();
    }

    public Task ClearServer(Server server) // Clear a specific server from the cache
    {
        server.Console.Close();
        server.Console.OnNewLogMessage.ClearSubscribers();

        lock (Servers)
            Servers.Remove(server);
        
        return Task.CompletedTask;
    }

    public async Task<ServerListItem[]> GetList(bool includeOffline)
    {
        Server[] servers;

        lock (Servers)
        {
            if (includeOffline)
                servers = Servers.ToArray();
            else
            {
                servers = Servers
                    .Where(x => x.State.State != ServerState.Offline)
                    .ToArray();
            }
        }

        List<Task> statsTask = new();
        List<ServerListItem> result = new();

        foreach (var server in servers)
        {
            var item = new ServerListItem()
            {
                Id = server.Configuration.Id,
                State = server.State.State,
                Stats = new()
            };
            
            result.Add(item);
            
            // If the server is offline or in join2start, we are done here
            if(server.State.State == ServerState.Offline || server.State.State == ServerState.Join2Start)
                continue;
            
            // Server is running => get the stats
            statsTask.Add(Task.Run(async () =>
            {
                try
                {
                    item.Stats = await server.GetStats();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }));
        }

        foreach (var task in statsTask)
            await task.WaitAsync(CancellationToken.None);

        return result.ToArray();
    }
}