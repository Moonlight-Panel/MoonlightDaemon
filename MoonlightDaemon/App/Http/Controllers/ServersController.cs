using Microsoft.AspNetCore.Mvc;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers;

[ApiController]
[Route("servers")]
public class ServersController : Controller
{
    private readonly ServerService ServerService;

    public ServersController(ServerService serverService)
    {
        ServerService = serverService;
    }

    [HttpPost]
    public async Task<ActionResult> Sync([FromBody] ServerConfiguration configuration)
    {
        var existingServer = await ServerService.GetById(configuration.Id);

        if (existingServer != null) // TODO: Update configuration
            return Ok();

        await ServerService.AddFromConfiguration(configuration);
        return Ok();
    }
}