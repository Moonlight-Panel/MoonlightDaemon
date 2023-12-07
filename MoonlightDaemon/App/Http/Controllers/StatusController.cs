using Microsoft.AspNetCore.Mvc;
using MoonlightDaemon.App.Http.Resources;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers;

[ApiController]
[Route("status")]
public class StatusController : Controller
{
    private readonly NodeService NodeService;

    public StatusController(NodeService nodeService)
    {
        NodeService = nodeService;
    }

    [HttpGet]
    public async Task<ActionResult<Status>> Get()
    {
        return Ok(new Status()
        {
            Version = "v2",
            IsBooting = NodeService.IsBooting
        });
    }
}