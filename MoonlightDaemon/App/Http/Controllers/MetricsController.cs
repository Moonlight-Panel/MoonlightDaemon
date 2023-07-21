using Microsoft.AspNetCore.Mvc;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Http.Resources;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers;

[ApiController]
[Route("metrics")]
public class MetricsController : Controller
{
    private readonly MetricsService MetricsService;
    private readonly DockerMetricsService DockerMetricsService;

    public MetricsController(MetricsService metricsService, DockerMetricsService dockerMetricsService)
    {
        MetricsService = metricsService;
        DockerMetricsService = dockerMetricsService;
    }

    [HttpGet("cpu")]
    public async Task<ActionResult<CpuMetrics>> GetCpuMetrics()
    {
        try
        {
            var data = new CpuMetrics()
            {
                CpuModel = await MetricsService.GetCpuModel(),
                CpuUsage = await MetricsService.GetCpuUsage()
            };

            return Ok(data);
        }
        catch (BashException e)
        {
            return Problem(e.Message);
        }
    }
    
    [HttpGet("memory")]
    public async Task<ActionResult<MemoryMetrics>> GetMemoryMetrics()
    {
        try
        {
            var data = new MemoryMetrics()
            {
                Used = await MetricsService.GetUsedMemory(),
                Total = await MetricsService.GetTotalMemory()
            };

            return Ok(data);
        }
        catch (BashException e)
        {
            return Problem(e.Message);
        }
    }
    
    [HttpGet("disk")]
    public async Task<ActionResult<DiskMetrics>> GetDiskMetrics()
    {
        try
        {
            var data = new DiskMetrics()
            {
                Used = await MetricsService.GetUsedDisk(),
                Total = await MetricsService.GetTotalDisk()
            };

            return Ok(data);
        }
        catch (BashException e)
        {
            return Problem(e.Message);
        }
    }

    [HttpGet("system")]
    public async Task<ActionResult<SystemMetrics>> GetSystemMetrics()
    {
        try
        {
            var data = new SystemMetrics()
            {
                Uptime = await MetricsService.GetUptime(),
                OsName = await MetricsService.GetOsName()
            };

            return Ok(data);
        }
        catch (BashException e)
        {
            return Problem(e.Message);
        }
    }

    [HttpGet("docker")]
    public async Task<ActionResult<DockerMetrics>> GetDockerMetrics()
    {
        try
        {
            var data = new DockerMetrics()
            {
                Containers = await DockerMetricsService.GetContainers()
            };

            return Ok(data);
        }
        catch (BashException e)
        {
            return Problem(e.Message);
        }
    }
}