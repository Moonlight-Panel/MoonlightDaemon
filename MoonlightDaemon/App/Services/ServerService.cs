using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Services.Monitors;
using MoonlightDaemon.App.Services.Servers;

namespace MoonlightDaemon.App.Services;

public class ServerService
{
    private readonly IServiceScopeFactory ServiceScopeFactory;
    private readonly ContainerMonitorService ContainerMonitorService;

    private readonly Dictionary<int, StateMachine<ServerState>> StateMachines = new();
    private readonly Dictionary<int, Server> ServerCache = new();

    public ServerService(IServiceScopeFactory serviceScopeFactory, ContainerMonitorService containerMonitorService)
    {
        ServiceScopeFactory = serviceScopeFactory;
        ContainerMonitorService = containerMonitorService;

        ContainerMonitorService.OnContainerEvent += (_, data) =>
        {
            Task.Run(async () =>
            {
                try
                {
                    if (data.Action == "die")
                    {
                        var stateMachine = await GetStateMachine(data.Id);

                        if(stateMachine == null)
                            return;

                        await stateMachine.TransitionTo(ServerState.Offline);
                    }
                }
                catch (IllegalStateException e)
                {
                    Logger.Warn($"Illegal state has been thown while processing container event {data.Action} for server {data.Id}: {e}");
                }
                catch (Exception e)
                {
                    Logger.Fatal($"An unhandled error occured while processing container action {data.Action} for server {data.Id}");
                    Logger.Fatal(e);
                }
            });
        };
    }

    public async Task SetServerState(int id, ServerState serverState)
    {
        var stateMachine = await EnsureStateMachine(id);
        await stateMachine.TransitionTo(serverState);
    }

    public async Task<ServerState> GetServerState(int id)
    {
        var stateMachine = await GetStateMachine(id);

        if (stateMachine == null)
            return ServerState.Offline;

        return stateMachine.State;
    }

    #region Cache

    public Task<Server?> LoadServerFromCacheUnsafe(int id)
    {
        lock (ServerCache)
        {
            if (!ServerCache.ContainsKey(id))
                return Task.FromResult<Server?>(null);

            return Task.FromResult<Server?>(ServerCache[id]);
        }
    }
    
    public async Task<Server> LoadServerFromCache(int id)
    {
        var result = await LoadServerFromCacheUnsafe(id);

        if (result == null) //TODO: add default enabled option to request server from the panel
            throw new ArgumentException("The requested server was not found in the cache");

        return result;
    }
    
    public Task StoreServerInCache(Server server)
    {
        lock (ServerCache)
        {
            ServerCache[server.Id] = server;
        }
        
        return Task.CompletedTask;
    }

    #endregion
    
    private async Task<StateMachine<ServerState>> EnsureStateMachine(int id)
    {
        var stateMachine = await GetStateMachine(id);

        if (stateMachine != null)
            return stateMachine;

        // Build new state machine
        stateMachine = new(ServerState.Offline);

        stateMachine.OnTransitioning += (_, x) => Logger.Debug($"Transitioning to {x}");
        stateMachine.OnTransitioned += (_, x) => Logger.Debug($"Transitioned to {x}");

        await stateMachine.AddTransition(ServerState.Offline, ServerState.Starting, async () =>
        {
            var scope = ServiceScopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ServerStartService>();
            var server = await LoadServerFromCache(id);
            await service.Perform(server);
        });

        await stateMachine.AddTransition(ServerState.Starting, ServerState.Running);
        await stateMachine.AddTransition(ServerState.Running, ServerState.Offline);
        await stateMachine.AddTransition(ServerState.Starting, ServerState.Offline);

        await stateMachine.AddTransition(ServerState.Offline, ServerState.Installing, async () =>
        {
            var scope = ServiceScopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ServerInstallService>();
            var server = await LoadServerFromCache(id);
            await service.Perform(server);
        });
        
        await stateMachine.AddTransition(ServerState.Installing, ServerState.Offline, async () =>
        {
            var scope = ServiceScopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ServerInstallService>();
            var server = await LoadServerFromCache(id);
            await service.Complete(server);
        });

        lock (StateMachines)
            StateMachines.Add(id, stateMachine);

        return stateMachine;
    }

    private Task<StateMachine<ServerState>?> GetStateMachine(int id)
    {
        StateMachine<ServerState>? stateMachine = null;

        lock (StateMachines)
        {
            if (StateMachines.Any(x => x.Key == id))
                stateMachine = StateMachines
                    .First(x => x.Key == id).Value;
        }

        return Task.FromResult(stateMachine);
    }
}