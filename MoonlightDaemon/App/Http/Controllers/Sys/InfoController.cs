using Microsoft.AspNetCore.Mvc;
using MoonCore.Services;
using MoonlightDaemon.App.Configuration;
using MoonlightDaemon.App.Http.Resources;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers.Sys;

[ApiController]
[Route("system/info")]
public class InfoController : Controller
{
    private readonly SystemService SystemService;
    private readonly ConfigService<ConfigV1> ConfigService;

    public InfoController(SystemService systemService, ConfigService<ConfigV1> configService)
    {
        SystemService = systemService;
        ConfigService = configService;
    }

    [HttpGet]
    public async Task<ActionResult<SystemStatus>> Get()
    {
        return await SystemService.GetStatus();
    }

    [HttpGet("logs")]
    public async Task<ActionResult> GetLogs()
    {
        await using var fs = System.IO.File.Open(ConfigService.Get().Paths.Log, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite);

        using var sr = new StreamReader(fs);
        var content = await sr.ReadToEndAsync();

        return Ok(content);
    }
}