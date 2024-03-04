using Microsoft.AspNetCore.Mvc;
using MoonCore.Helpers;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers.System;

[ApiController]
[Route("system/boot")]
public class BootController : Controller
{
    private readonly BootService BootService;

    public BootController(BootService bootService)
    {
        BootService = bootService;
    }

    public async Task<ActionResult> Boot()
    {
        Logger.Info("Received boot signal from moonlight");
        
        await BootService.Boot();

        return Ok();
    }
}