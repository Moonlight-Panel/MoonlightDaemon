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
                        var stateMachine = await GetStateMachineById(data.Id);

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

    public async Task SetServerState(Server server, ServerState serverState)
    {
        var stateMachine = await EnsureStateMachine(server);
        await stateMachine.TransitionTo(serverState);
    }

    public async Task<ServerState> GetServerState(Server server)
    {
        var stateMachine = await GetStateMachine(server);

        if (stateMachine == null)
            return ServerState.Offline;

        return stateMachine.State;
    }

    // Utils
    private async Task<StateMachine<ServerState>> EnsureStateMachine(Server server)
    {
        var stateMachine = await GetStateMachine(server);

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
            await service.Perform(server);
        });

        await stateMachine.AddTransition(ServerState.Starting, ServerState.Running);
        await stateMachine.AddTransition(ServerState.Running, ServerState.Offline);
        await stateMachine.AddTransition(ServerState.Starting, ServerState.Offline);

        lock (StateMachines)
            StateMachines.Add(server.Id, stateMachine);

        return stateMachine;
    }

    private Task<StateMachine<ServerState>?> GetStateMachine(Server server) => GetStateMachineById(server.Id);

    private Task<StateMachine<ServerState>?> GetStateMachineById(int id)
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