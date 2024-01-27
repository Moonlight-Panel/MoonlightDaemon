using Microsoft.AspNetCore.Mvc;
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
        await BootService.Boot();

        return Ok();
    }
}