using Docker.DotNet.Models;
using MoonlightDaemon.App.Helpers;

namespace MoonlightDaemon.App.Services;

public class MoonlightService
{
    private readonly ShellHelper ShellHelper;
    private readonly ConfigService ConfigService;

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

        // Check for pterodactyl user to check if we need to enable the pterodactyl
        // mode for compatibility
        if (passwdLines.Any(x => x.StartsWith("pterodactyl")))
        {
            ConfigService.Get().PterodactylMode = true;
            ConfigService.Save();

            Logger.Info("Detected pterodactyl user. Switching to pterodactyl compatibility mode");
            username = "pterodactyl";
        }

        // Check for moonlight's own user
        if (passwdLines.Any(x => x.StartsWith("moonlight")))
            username = "moonlight";

        // No user found, so we create one
        if (username == null)
        {
            await ShellHelper.ExecuteCommand("useradd --system --no-create-home --shell /usr/sbin/nologin moonlight");
            username = "moonlight";
            Logger.Info("Created missing moonlight user");
        }

        // Now we read out the passwd file again to find the gui and uid
        passwdLines = await File.ReadAllLinesAsync("/etc/passwd");

        if (!passwdLines.Any(x =>
                x.StartsWith(username))) // Username not found, unable to operate further, so its a crash
        {
            Logger.Fatal($"Unable to find '{username}' in /etc/passwd");
            Environment.Exit(1);
            return;
        }

        // Parse passwd line to get uid and gid
        var parts = passwdLines.First(x => x.StartsWith(username)).Split(":");
        var uid = int.Parse(parts[2]);
        var gid = int.Parse(parts[3]);

        // Store values in memory using the temp config
        var tempConfig = ConfigService.GetTemp();
        tempConfig.Username = username;
        tempConfig.Uid = uid;
        tempConfig.Gid = gid;

        Logger.Debug($"Using '{tempConfig.Username}' with Uid {tempConfig.Uid} and Gid {tempConfig.Gid} for file permissions");
    }
}