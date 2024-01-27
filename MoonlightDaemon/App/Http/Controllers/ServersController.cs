using Microsoft.AspNetCore.Mvc;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Models.Enums;
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

    [HttpPost("{id:Int}/power/{action}")]
    public async Task<ActionResult> Power(int id, string action)
    {
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");

        if (!Enum.TryParse(action, true, out PowerAction powerAction))
            return BadRequest("Invalid power action");
        
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

        return Ok();
    }

    [HttpPost("{id:int}/subscribe")]
    public async Task<ActionResult> Subscribe(int id)
    {
        var server = await ServerService.GetById(id);

        if (server == null)
            return NotFound("No server with this id found");

        await ServerService.SubscribeToConsole(id);

        return Ok();
    }
}