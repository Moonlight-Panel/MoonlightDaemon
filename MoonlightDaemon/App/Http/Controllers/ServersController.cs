using Microsoft.AspNetCore.Mvc;
using MoonCore.Helpers;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Http.Requests;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Packets;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers;

[ApiController]
[Route("servers")]
public class ServersController : Controller
{
    private readonly ServerService ServerService;
    private readonly HttpApiClient<MoonlightException> HttpApiClient;

    public ServersController(ServerService serverService, HttpApiClient<MoonlightException> httpApiClient)
    {
        ServerService = serverService;
        HttpApiClient = httpApiClient;
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
    public async Task<ActionResult> Ws(int id)
    {
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");

        if (!HttpContext.WebSockets.IsWebSocketRequest)
            return BadRequest("Only websocket connections are allowed at this endpoint");

        var websocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var packetConnection = new WsPacketConnection(websocket);

        await packetConnection.RegisterPacket<string>("output");
        await packetConnection.RegisterPacket<ServerState>("state");
        await packetConnection.RegisterPacket<ServerStats>("stats");

        // Transfer current state
        await packetConnection.Send(server.State.State);

        // Transfer cached messages
        foreach (var message in await server.Console.GetAllLogMessages())
            await packetConnection.Send(message);

        CancellationTokenSource? statsCancel = default;

        async Task HandleStateChange(ServerState state)
        {
            await packetConnection.Send(state);

            if (state == ServerState.Starting)
            {
                statsCancel = await server.GetStatsStream(async stats =>
                {
                    await packetConnection.Send(stats);
                });
            }
        }
        
        async Task HandleNewMessage(string message)
        {
            await packetConnection.Send(message);
        }

        server.State.OnTransitioned += HandleStateChange;
        server.Console.OnNewLogMessage += HandleNewMessage;

        if (server.State.State != ServerState.Offline && server.State.State != ServerState.Join2Start)
        {
            statsCancel = await server.GetStatsStream(async stats =>
            {
                await packetConnection.Send(stats);
            });
        }

        await packetConnection.WaitForClose();

        server.State.OnTransitioned -= HandleStateChange;
        server.Console.OnNewLogMessage -= HandleNewMessage;
        
        if(statsCancel != null)
            statsCancel.Cancel();
        
        return Ok();
    }
}