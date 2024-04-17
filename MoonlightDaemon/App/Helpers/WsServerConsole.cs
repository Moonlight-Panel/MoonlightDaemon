using System.Net.WebSockets;
using MoonCore.Helpers;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Enums;

namespace MoonlightDaemon.App.Helpers;

public class WsServerConsole
{
    private readonly WebSocket WebSocket;
    private readonly Server Server;
    private readonly AdvancedWebsocketStream WebsocketStream;
    private readonly TaskCompletionSource WaitTask = new();

    private CancellationTokenSource? StatsStreamCancellation;

    public WsServerConsole(WebSocket webSocket, Server server)
    {
        WebSocket = webSocket;
        Server = server;
        WebsocketStream = new(WebSocket);
    }

    public async Task Work()
    {
        // Setup networking
        WebsocketStream.RegisterPacket<string>(1);
        WebsocketStream.RegisterPacket<ServerState>(2);
        WebsocketStream.RegisterPacket<ServerStats>(3);

        // Send initial data
        await InitialUpdate();

        // Subscribe to stats and init callbacks

        // Only start the stats stream if the runtime container exists
        await InitCallbacks();
        
        if (Server.State.State != ServerState.Offline && Server.State.State != ServerState.Join2Start &&
            Server.State.State != ServerState.Installing)
            await MonitorStats();

        await WaitTask.Task;
    }

    private async Task Quit()
    {
        // Unsubscribe callbacks
        await DestroyCallbacks();

        // Cancel stats monitoring
        if (StatsStreamCancellation != null)
            StatsStreamCancellation.Cancel();

        // Close websocket connection
        if (WebSocket.State == WebSocketState.Open)
        {
            try
            {
                await WebSocket.CloseOutputAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None);
            }
            catch (Exception e)
            {
                Logger.Warn("An error occured while quiting and closing the websocket connection:");
                Logger.Warn(e);
            }
        }

        // Quit the "Work" method
        WaitTask.SetResult();
    }

    private async Task InitialUpdate()
    {
        // Current state
        await WebsocketStream.SendPacket(Server.State.State);

        // Console messages
        var messages = await Server.Console.GetAllLogMessages();

        foreach (var messageChunk in messages.Chunk(20))
        {
            var combinedMessage = "";

            foreach (var message in messageChunk)
                combinedMessage += message + (message != messageChunk.Last() ? "\n" : "");

            await WebsocketStream.SendPacket(combinedMessage);
        }
    }

    private async Task MonitorStats()
    {
        StatsStreamCancellation = await Server.GetStatsStream(async stats =>
        {
            try
            {
                await WebsocketStream.SendPacket(stats);
            }
            catch (WebSocketException)
            {
                await Quit();
            }
            catch (Exception e)
            {
                if (Server.State.State == ServerState.Offline || Server.State.State == ServerState.Join2Start ||
                    Server.State.State == ServerState.Installing)
                {
                    // If an error occured and we are now offline, join2start or installing, we want to stop monitoring the stats,
                    // as the runtime container does no longer exist. We stop it with the cancellation token but NOT quit the websocket connection.
                    // The stats callback should start the monitoring again when the server starts

                    if (StatsStreamCancellation != null)
                        StatsStreamCancellation.Cancel();

                    return;
                }

                Logger.Warn("Server stats stream error:");
                Logger.Warn(e);
            }
        });
    }

    private Task InitCallbacks()
    {
        Server.Console.OnNewLogMessage += OnConsoleMessage;
        Server.State.OnTransitioned += OnStateChanged;

        return Task.CompletedTask;
    }

    private Task DestroyCallbacks()
    {
        Server.Console.OnNewLogMessage -= OnConsoleMessage;
        Server.State.OnTransitioned -= OnStateChanged;

        return Task.CompletedTask;
    }

    #region Callbacks

    private async Task OnStateChanged(ServerState state)
    {
        try
        {
            await WebsocketStream.SendPacket(state);

            // If the server starts from an offline state, the monitor for stats needs to be enabled, thats why we
            // check for this here and start the monitor again, but only if the monitor was never created (null check) or has been stopped (check for cancellation)
            if (state == ServerState.Starting)
            {
                if(StatsStreamCancellation != null && !StatsStreamCancellation.IsCancellationRequested)
                    return;
                
                try
                {
                    await MonitorStats();
                }
                catch (Exception e)
                {
                    Logger.Warn(
                        "An error occured while reactivating stats monitoring. Stats are unavailable for this connection now");
                    Logger.Warn(e);
                }
            }
        }
        catch (WebSocketException)
        {
            await Quit();
        }
        catch (Exception e)
        {
            Logger.Warn("Server state stream error:");
            Logger.Warn(e);
        }
    }

    private async Task OnConsoleMessage(string message)
    {
        try
        {
            await WebsocketStream.SendPacket(message);
        }
        catch (WebSocketException)
        {
            await Quit();
        }
        catch (Exception e)
        {
            Logger.Warn("Server console stream error:");
            Logger.Warn(e);
        }
    }

    #endregion
}