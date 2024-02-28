using Microsoft.AspNetCore.Mvc;
using MoonlightDaemon.App.Http.Resources;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers.System;

[ApiController]
[Route("system/info")]
public class InfoController : Controller
{
    private readonly SystemService SystemService;

    public InfoController(SystemService systemService)
    {
        SystemService = systemService;
    }

    public async Task<ActionResult<SystemStatus>> Get()
    {
        return await SystemService.GetStatus();
    }
}