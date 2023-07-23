using Microsoft.AspNetCore.Mvc;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers;

[ApiController]
[Route("firewall")]
public class FirewallController : Controller
{
    private readonly FirewallService FirewallService;

    public FirewallController(FirewallService firewallService)
    {
        FirewallService = firewallService;
    }

    [HttpPost("rebuild")]
    public async Task<ActionResult> Rebuild([FromBody] string[] ips)
    {
        await FirewallService.Rebuild(ips);

        return Ok();
    }
}