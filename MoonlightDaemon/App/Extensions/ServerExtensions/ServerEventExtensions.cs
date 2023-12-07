using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Enums;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerEventExtensions
{
    public static async Task HandleExited(this Server server)
    {
        if (server.State.State == ServerState.Offline)
        {
            await server.Destroy();
        }
        else if (server.State.State == ServerState.Installing)
        {
            await server.FinalizeInstall();
        }
        else
        {
            if (server.State.State != ServerState.Stopping)
            {
                // Handle crash here
                Logger.Debug("Server crashed");
            }
            
            await server.Destroy();
            await server.State.TransitionTo(ServerState.Offline);
        }
    }
}