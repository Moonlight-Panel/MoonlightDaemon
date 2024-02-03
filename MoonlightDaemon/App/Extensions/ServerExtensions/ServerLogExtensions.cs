using MoonCore.Helpers;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerLogExtensions
{
    public static async Task Log(this Server server, string message)
    {
        Logger.Debug($"[Server {server.Configuration.Id}] {message}");

        await server.Console.WriteLine("\x1b[38;5;16;48;5;135m\x1b[39m\x1b[1m Moonlight \x1b[0m " + message);
    }
}