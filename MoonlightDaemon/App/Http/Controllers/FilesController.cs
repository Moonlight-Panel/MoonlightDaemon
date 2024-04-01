using Microsoft.AspNetCore.Mvc;
using MoonCore.Abstractions;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Http.Resources;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers;

[Route("files")]
[ApiController]
public class FilesController : Controller
{
    private readonly ServerService ServerService;

    public FilesController(ServerService serverService)
    {
        ServerService = serverService;
    }

    [HttpGet("{serverId}/list")]
    public async Task<ActionResult<FileEntry[]>> List(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        return await fileSystem.List(path);
    }

    [HttpDelete("{serverId}/deleteFile")]
    public async Task<ActionResult> DeleteFile(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        await fileSystem.DeleteFile(path);

        return Ok();
    }

    [HttpDelete("{serverId}/deleteDirectory")]
    public async Task<ActionResult> DeleteDirectory(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        await fileSystem.DeleteDirectory(path);

        return Ok();
    }

    [HttpPost("{serverId}/move")]
    public async Task<ActionResult> Move(int serverId, [FromQuery] string from, [FromQuery] string to)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        await fileSystem.Move(from, to);

        return Ok();
    }

    [HttpPost("{serverId}/createDirectory")]
    public async Task<ActionResult> CreateDirectory(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        await fileSystem.CreateDirectory(path);

        return Ok();
    }

    [HttpPost("{serverId}/createFile")]
    public async Task<ActionResult> CreateFile(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        await fileSystem.CreateDirectory(path);

        return Ok();
    }
    
    [HttpGet("{serverId}/readFile")]
    public async Task<ActionResult<string>> ReadFile(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        return await fileSystem.ReadFile(path);
    }

    [HttpPost("{serverId}/writeFile")]
    public async Task<ActionResult> WriteFile(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        using var sr = new StreamReader(Request.Body);
        var content = await sr.ReadToEndAsync();

        await fileSystem.WriteFile(path, content);

        return Ok();
    }

    [HttpGet("{serverId}/readFileStream")]
    public async Task<ActionResult> ReadFileStream(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        return File(await fileSystem.ReadFileStream(path), "application/octet-stream");
    }

    [HttpPost("{serverId}/writeFileStream")]
    public async Task<ActionResult> WriteFileStream(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        if (!Request.Form.Files.Any())
            return BadRequest("No file in request found");

        if (Request.Form.Count > 1)
            return BadRequest("Too much files in request. Maximum is one file per request");

        var file = Request.Form.Files.First();
        await using var stream = file.OpenReadStream();

        await fileSystem.WriteFileStream(path, stream);
        
        return Ok();
    }

    private async Task<ServerFileSystem?> GetFileSystem(int serverId)
    {
        var server = await ServerService.GetById(serverId);

        if (server == null)
            return null;

        return server.FileSystem;
    }
}