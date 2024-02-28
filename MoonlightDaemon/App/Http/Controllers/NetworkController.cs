using Microsoft.AspNetCore.Mvc;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Controllers;

[ApiController]
[Route("network")]
public class NetworkController : Controller
{
    private readonly NetworkService NetworkService;

    public NetworkController(NetworkService networkService)
    {
        NetworkService = networkService;
    }

    [HttpGet]
    public async Task<ActionResult> Get()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            return BadRequest();

        var websocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

        // Register the websocket as a client
        var client = await NetworkService.ConnectionManager.AddClient(websocket);
        
        // Wait for the client to finish
        await client.PacketConnection.WaitForClose();

        return Ok();
    }
}