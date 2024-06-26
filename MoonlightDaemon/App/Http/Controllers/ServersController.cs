using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;
using MoonCore.Helpers;
using MoonCore.Services;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Http.Requests;
using MoonlightDaemon.App.Http.Resources;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers;

[ApiController]
[Route("servers")]
public class ServersController : Controller
{
    private readonly ServerService ServerService;
    private readonly BackupService BackupService;
    private readonly MoonlightService MoonlightService;
    private readonly HttpApiClient<MoonlightException> HttpApiClient;
    private readonly JwtService<DaemonJwtType> JwtService;
    private readonly BootService BootService;

    public ServersController(
        ServerService serverService,
        HttpApiClient<MoonlightException> httpApiClient,
        BackupService backupService,
        MoonlightService moonlightService,
        JwtService<DaemonJwtType> jwtService,
        BootService bootService)
    {
        ServerService = serverService;
        HttpApiClient = httpApiClient;
        BackupService = backupService;
        MoonlightService = moonlightService;
        JwtService = jwtService;
        BootService = bootService;
    }

    [HttpPost("{id:int}/sync")]
    public async Task<ActionResult> Sync(int id)
    {
        // Fetch the latest configuration
        var configuration = await HttpApiClient.Get<ServerConfiguration>($"api/servers/{id}");
        
        // Update configuration
        var existingServer = await ServerService.GetById(configuration.Id);

        if (existingServer != null) // Update existing server configuration if a server configuration is already cached
            existingServer.Configuration = configuration;
        else
            await ServerService.AddFromConfiguration(configuration);
        
        return Ok();
    }

    // IMPORTANT: "action" is reserved keyword in asp.net
    // no we need to call the parameter "actionEnum"
    [HttpPost("{id:int}/power/{actionEnum:alpha}")]
    public async Task<ActionResult> Power(int id, string actionEnum, [FromQuery] bool runAsync = false)
    {
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");

        if (!Enum.TryParse(actionEnum, true, out PowerAction powerAction))
            return BadRequest("Invalid power action");

        if (runAsync)
        {
            Task.Run(async () =>
            {
                switch (powerAction)
                {
                    case PowerAction.Install:
                        await server.Reinstall();
                        break;
                    case PowerAction.Kill:
                        await server.Kill();
                        break;
                    case PowerAction.Start:
                        await server.Start();
                        break;
                    case PowerAction.Stop:
                        await server.Stop();
                        break;
                }
            });
        }
        else
        {
            switch (powerAction)
            {
                case PowerAction.Install:
                    await server.Reinstall();
                    break;
                case PowerAction.Kill:
                    await server.Kill();
                    break;
                case PowerAction.Start:
                    await server.Start();
                    break;
                case PowerAction.Stop:
                    await server.Stop();
                    break;
            }
        }
        
        return Ok();
    }

    [HttpGet("{id:int}/state")]
    public async Task<ActionResult<string>> GetState(int id)
    {
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");

        return server.State.State.ToString();
    }
    
    [HttpGet("{id:int}/stats")]
    public async Task<ActionResult<ServerStats>> GetStats(int id)
    {
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");

        if (server.State.State == ServerState.Offline || server.State.State == ServerState.Installing)
            return new ServerStats();

        return await server.GetStats();
    }

    [HttpPost("{id:int}/command")]
    public async Task<ActionResult> Command(int id, [FromBody] SendCommand command)
    {
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");
        
        if(server.State.State != ServerState.Offline && server.State.State != ServerState.Join2Start)
            await server.Console.SendCommand(command.Command);

        return Ok();
    }
    
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");

        await server.Delete();
        await ServerService.ClearServer(server);
        
        return Ok();
    }

    [HttpGet("{id:int}/ws")]
    public async Task Ws(int id)
    {
        if (!BootService.IsBooted)
        {
            Response.StatusCode = 503;
            return;
        }
        
        var server = await ServerService.GetById(id);

        if (server == null)
        {
            Response.StatusCode = 404;
            await Response.WriteAsync("No server with this id found");
            return;
        }

        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("Only websocket connections are allowed at this endpoint");
            return;
        }

        var websocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var serverWs = new WsServerConsole(websocket, server);

        await serverWs.Work();
        
        Logger.Debug("Ws exited");

        /*
        var websocketStream = new AdvancedWebsocketStream(websocket);

        try
        {
            websocketStream.RegisterPacket<string>(1);
            websocketStream.RegisterPacket<ServerState>(2);
            websocketStream.RegisterPacket<ServerStats>(3);

            // Transfer current state
            await websocketStream.SendPacket(server.State.State);

            // Transfer cached messages
            // In order to prevent a slow loading console on slow internet connections,
            // we build all log messages into a multiple message update packets.

            var messages = await server.Console.GetAllLogMessages();

            foreach (var messageChunk in messages.Chunk(20))
            {
                var combinedMessage = "";

                foreach (var message in messageChunk)
                    combinedMessage += message + "\n";

                await websocketStream.SendPacket(combinedMessage);
            }

            // Stats
            CancellationTokenSource? statsCancel = default;

            async Task HandleStateChange(ServerState state)
            {
                try
                {
                    await websocketStream.SendPacket(state);
                }
                catch (Exception e)
                {
                    Logger.Warn("An error occured while sending state packet");
                    Logger.Warn(e);

                    await websocketStream.Close();
                }

                if (state == ServerState.Starting)
                {
                    statsCancel = await server.GetStatsStream(async stats =>
                    {
                        try
                        {
                            await websocketStream.SendPacket(stats);
                        }
                        catch (Exception e)
                        {
                            Logger.Warn("An error occured while sending stats packet");
                            Logger.Warn(e);

                            await websocketStream.Close();
                        }
                    });
                }
            }

            async Task HandleNewMessage(string message)
            {
                try
                {
                    await websocketStream.SendPacket(message);
                }
                catch (Exception e)
                {
                    Logger.Warn("An error occured while sending message packet");
                    Logger.Warn(e);

                    Logger.Debug(websocket.State.ToString());
                    await websocket.CloseAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None);
                }
            }

            server.State.OnTransitioned += HandleStateChange;
            server.Console.OnNewLogMessage += HandleNewMessage;

            if (server.State.State != ServerState.Offline && server.State.State != ServerState.Join2Start)
            {
                statsCancel = await server.GetStatsStream(async stats =>
                {
                    try
                    {
                        await websocketStream.SendPacket(stats);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn("An error occured while sending stats packet");
                        Logger.Warn(e);

                        await websocket.CloseAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None);
                    }
                });
            }

            await websocketStream.WaitForClose();

            server.State.OnTransitioned -= HandleStateChange;
            server.Console.OnNewLogMessage -= HandleNewMessage;

            if (statsCancel != null)
                statsCancel.Cancel();
        }
        catch (Exception e)
        {
            if(websocket.State == WebSocketState.Open)
                await websocket.CloseAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None);

            Logger.Warn("Closed:");
            Logger.Warn(e);
        }*/
    }

    [HttpPost("{id:int}/backups/{backupId:int}")]
    public async Task<ActionResult> CreateBackup(int id, int backupId)
    {
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");

        Task.Run(async () =>
        {
            try
            {
                var result = await BackupService.Create(server, backupId);
                
                await MoonlightService.ReportBackupStatus(server, backupId, new()
                {
                    Successful = true,
                    Size = result.Size
                });
            }
            catch (Exception e)
            {
                Logger.Warn($"An error occured while creating backup for server {server.Configuration.Id}");
                Logger.Warn(e);

                await MoonlightService.ReportBackupStatus(server, backupId, new()
                {
                    Successful = false,
                    Size = 0
                });
            }
        });

        return Ok();
    }
    
    [HttpDelete("{id:int}/backups/{backupId:int}")]
    public async Task<ActionResult> DeleteBackup(int id, int backupId)
    {
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");

        await BackupService.Delete(server, backupId);

        return Ok();
    }

    [HttpGet("{id:int}/backups/{backupId:int}")]
    public async Task<ActionResult> DownloadBackup(int id, int backupId, [FromQuery] string downloadToken)
    {
        // Jwt validation
        if (!await JwtService.Validate(downloadToken, DaemonJwtType.BackupDownload))
            return BadRequest("Invalid jwt");

        var data = await JwtService.Decode(downloadToken);

        if (!data.ContainsKey("BackupId"))
            return BadRequest("Backup id missing in jwt payload");

        if (!int.TryParse(data["BackupId"], out int jwtBackupId))
            return BadRequest("Unable to parse backup id");

        if (backupId != jwtBackupId)
            return StatusCode(403);
        
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");
        
        // Now we can start the download

        try
        {
            var download = await BackupService.GetDownload(server, backupId);

            return File(download.Stream, download.ContentType, download.FileName);
        }
        catch (Exception e)
        {
            Logger.Warn("An error occured while downloading backup");
            Logger.Warn(e);

            return Problem();
        }
    }

    [HttpPatch("{id:int}/backups/{backupId:int}")]
    public async Task<ActionResult> RestoreBackup(int id, int backupId)
    {
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");

        if (server.State.State != ServerState.Offline)
            return BadRequest();

        await server.LockWhile(async () =>
        {
            try
            {
                await BackupService.Restore(server, backupId);
            }
            catch (Exception e)
            {
                Logger.Warn("A error occured while restoring backup");
                Logger.Warn(e);
            }
        });

        return Ok();
    }

    [HttpGet("list")]
    public async Task<ActionResult<ServerListItem>> List([FromQuery] bool includeOffline = false)
    {
        return Ok(await ServerService.GetList(includeOffline));
    }
}