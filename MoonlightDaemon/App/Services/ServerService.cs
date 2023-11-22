using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Services.Servers;

namespace MoonlightDaemon.App.Services;

public class ServerService
{
    private readonly IServiceScopeFactory ServiceScopeFactory;
    
    private readonly Dictionary<int, StateMachine<ServerState>> StateMachines = new();

    public ServerService(IServiceScopeFactory serviceScopeFactory)
    {
        ServiceScopeFactory = serviceScopeFactory;
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

        stateMachine = new(ServerState.Offline);

        await stateMachine.AddTransition(ServerState.Offline, ServerState.Starting, async () =>
        {
            var scope = ServiceScopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ServerStartService>();
            await service.Perform(server);
        });

        await stateMachine.AddTransition(ServerState.Starting, ServerState.Running);

        lock (StateMachines)
            StateMachines.Add(server.Id, stateMachine);

        return stateMachine;
    }
    
    private Task<StateMachine<ServerState>?> GetStateMachine(Server server)
    {
        StateMachine<ServerState>? stateMachine = null;

        lock (StateMachines)
        {
            if (StateMachines.Any(x => x.Key == server.Id))
                stateMachine = StateMachines
                    .First(x => x.Key == server.Id).Value;
        }

        return Task.FromResult(stateMachine);
    }
}