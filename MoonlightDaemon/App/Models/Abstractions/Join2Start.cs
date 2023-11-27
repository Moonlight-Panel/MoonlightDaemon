using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Enums;

namespace MoonlightDaemon.App.Models.Abstractions;

public class Join2Start
{
    private readonly int Port;
    private StateMachine<ServerState> StateMachine;

    public Join2Start(int port, StateMachine<ServerState> stateMachine)
    {
        Port = port;
        StateMachine = stateMachine;
    }

    public async Task Start()
    {
        
    }

    public async Task Stop()
    {
        
    }
}