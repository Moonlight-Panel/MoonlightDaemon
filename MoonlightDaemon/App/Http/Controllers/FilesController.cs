using Microsoft.AspNetCore.Mvc;
using MoonCore.Helpers;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Http.Resources;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers;

[Route("files")]
[ApiController]
public class FilesController : Controller
{
    private readonly ServerService ServerService;
    private readonly FileArchiveService FileArchiveService;

    public FilesController(ServerService serverService, FileArchiveService fileArchiveService)
    {
        ServerService = serverService;
        FileArchiveService = fileArchiveService;
    }

    [HttpGet("{serverId}/list")]
    public async Task<ActionResult<FileEntry[]>> List(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        var result = new List<FileEntry>();

        result.AddRange(fileSystem.List(path).Select(x => new FileEntry()
        {
            Size = x.Size,
            IsDirectory = x.IsDirectory,
            IsFile = x.IsFile,
            LastModifiedAt = x.LastChanged,
            Name = x.Name
        }));
        
        return result.ToArray();
    }

    [HttpDelete("{serverId}/deleteFile")]
    public async Task<ActionResult> DeleteFile(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        fileSystem.Remove(path);

        return Ok();
    }

    [HttpDelete("{serverId}/deleteDirectory")]
    public async Task<ActionResult> DeleteDirectory(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        fileSystem.Remove(path);

        return Ok();
    }

    [HttpPost("{serverId}/move")]
    public async Task<ActionResult> Move(int serverId, [FromQuery] string from, [FromQuery] string to) // TODO: Add separate move functions for files and folders
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        fileSystem.Move(from, to);

        return Ok();
    }

    [HttpPost("{serverId}/createDirectory")]
    public async Task<ActionResult> CreateDirectory(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        fileSystem.CreateDirectory(path);

        return Ok();
    }

    [HttpPost("{serverId}/createFile")]
    public async Task<ActionResult> CreateFile(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        fileSystem.CreateFile(path);

        return Ok();
    }

    [HttpGet("{serverId}/readFile")]
    public async Task<ActionResult<string>> ReadFile(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        return fileSystem.ReadFile(path);
    }

    [HttpPost("{serverId}/writeFile")]
    public async Task<ActionResult> WriteFile(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        using var sr = new StreamReader(Request.Body);
        var content = await sr.ReadToEndAsync();

        fileSystem.WriteFile(path, content);

        return Ok();
    }

    [HttpGet("{serverId}/readFileStream")]
    public async Task<ActionResult> ReadFileStream(int serverId, [FromQuery] string path)
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        return File(fileSystem.OpenFileReadStream(path), "application/octet-stream");
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

        fileSystem.WriteStreamToFile(path, stream);

        return Ok();
    }

    [HttpPost("{serverId}/archive")]
    public async Task<ActionResult> Archive(
        int serverId,
        [FromQuery] string path,
        [FromBody] string[] files,
        [FromQuery] string provider = "tar.gz")
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        await FileArchiveService.Archive(fileSystem, provider, path, files);

        return Ok();
    }
    
    [HttpPost("{serverId}/extract")]
    public async Task<ActionResult> Extract(
        int serverId,
        [FromQuery] string path,
        [FromQuery] string destination,
        [FromQuery] string provider = "tar.gz")
    {
        var fileSystem = await GetFileSystem(serverId);

        if (fileSystem == null)
            return NotFound();

        await FileArchiveService.UnArchive(fileSystem, provider, path, destination);

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