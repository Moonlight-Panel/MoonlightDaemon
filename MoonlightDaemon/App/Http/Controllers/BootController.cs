using Microsoft.AspNetCore.Mvc;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers;

[ApiController]
[Route("boot")]
public class BootController : Controller
{
    private readonly NodeService NodeService;

    public BootController(NodeService nodeService)
    {
        NodeService = nodeService;
    }

    [HttpPost]
    public async Task<ActionResult> Boot()
    {
        await NodeService.StartBoot();
        return Ok();
    }

    [HttpPost("servers")]
    public async Task<ActionResult> Servers([FromBody] ServerConfiguration[] configurations)
    {
        await NodeService.AddBootServers(configurations);
        return Ok();
    }

    [HttpPost("restore")]
    public async Task<ActionResult> Restore()
    {
        await NodeService.FinishBoot();
        return Ok();
    }
}