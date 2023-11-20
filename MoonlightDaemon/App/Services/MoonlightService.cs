using Docker.DotNet.Models;
using MoonlightDaemon.App.Helpers;

namespace MoonlightDaemon.App.Services;

public class MoonlightService
{
    private readonly ShellHelper ShellHelper;
    private readonly ConfigService ConfigService;
    
    public string Username { get; set; }
    public int Uid { get; set; }
    public int Gid { get; set; }

    public MoonlightService(ShellHelper shellHelper, ConfigService configService)
    {
        ShellHelper = shellHelper;
        ConfigService = configService;
    }

    public async Task Initialize()
    {
        Logger.Info("Initializing");
        await EnsureMoonlightUser();
    }

    private async Task EnsureMoonlightUser()
    {
        Logger.Debug("Ensuring moonlight user has been created");
        var passwdLines = await File.ReadAllLinesAsync("/etc/passwd");
        string? username = null;
        
        if (passwdLines.Any(x => x.StartsWith("pterodactyl")))
        {
            ConfigService.Get().PterodactylMode = true;
            ConfigService.Save();
            
            Logger.Info("Detected pterodactyl user. Switching to pterodactyl compatibility mode");
            username = "pterodactyl";
        }

        if (passwdLines.Any(x => x.StartsWith("moonlight")))
            username = "moonlight";

        if (username == null)
        {
            await ShellHelper.ExecuteCommand("useradd --system --no-create-home --shell /usr/sbin/nologin moonlight");
            username = "moonlight";
            Logger.Info("Created missing moonlight user");
        }

        Username = username;
        passwdLines = await File.ReadAllLinesAsync("/etc/passwd");

        if (!passwdLines.Any(x => x.StartsWith(username)))
        {
            Logger.Fatal($"Unable to find '{username}' in /etc/passwd");
            Environment.Exit(1);
            return;
        }

        var parts = passwdLines.First(x => x.StartsWith(username)).Split(":");
        Uid = int.Parse(parts[2]);
        Gid = int.Parse(parts[3]);
        Logger.Debug($"Using '{username}' for Uid {Uid} and Gid {Gid}");
    }
}