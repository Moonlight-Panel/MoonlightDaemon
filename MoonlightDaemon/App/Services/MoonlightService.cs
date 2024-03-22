using MoonCore.Attributes;
using MoonCore.Helpers;
using MoonCore.Services;
using MoonlightDaemon.App.Api.Moonlight.Requests;
using MoonlightDaemon.App.Configuration;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Configuration;

namespace MoonlightDaemon.App.Services;

[Singleton]
// This class is for interacting with the panel
public class MoonlightService
{
    private readonly ConfigService<ConfigV1> ConfigService;
    private readonly HttpClient Client;

    public MoonlightService(ConfigService<ConfigV1> configService)
    {
        ConfigService = configService;

        var remoteUrl = ConfigService.Get().Remote.Url;

        var url = remoteUrl.StartsWith("/") ? remoteUrl : remoteUrl + "/";

        Client = new()
        {
            BaseAddress = new Uri(url + "api/servers/")
        };

        Client.DefaultRequestHeaders.Add("Authorization", ConfigService.Get().Remote.Token);
    }

    public async Task<ServerInstallConfiguration?> GetInstallConfiguration(Server server)
    {
        try
        {
            var configuration =
                await Client.SendHandled<ServerInstallConfiguration, MoonlightException>(HttpMethod.Get,
                    $"{server.Configuration.Id}/install");

            return configuration;
        }
        catch (MoonlightException e)
        {
            Logger.Warn(
                $"An error occured while fetching install configuration for server {server.Configuration.Id} from panel");
            Logger.Warn(e);
        }
        catch (Exception e)
        {
            Logger.Fatal(
                $"An unhandled error occured while fetching install configuration for server {server.Configuration.Id} from panel");
            Logger.Fatal(e);
        }

        return null;
    }

    public async Task<ServerConfiguration?> GetConfiguration(Server server)
    {
        try
        {
            var configuration =
                await Client.SendHandled<ServerConfiguration, MoonlightException>(HttpMethod.Get,
                    $"{server.Configuration.Id}");

            return configuration;
        }
        catch (MoonlightException e)
        {
            Logger.Warn(
                $"An error occured while fetching install configuration for server {server.Configuration.Id} from panel");
            Logger.Warn(e);
        }
        catch (Exception e)
        {
            Logger.Fatal(
                $"An unhandled error occured while fetching install configuration for server {server.Configuration.Id} from panel");
            Logger.Fatal(e);
        }

        return null;
    }

    public async Task ReportBackupStatus(Server server, int backupId, BackupStatus status)
    {
        try
        {
            await Client.SendHandled<MoonlightException>(HttpMethod.Post,
                $"{server.Configuration.Id}/backups/{backupId}", status);
        }
        catch (Exception e)
        {
            Logger.Warn("An error occured while reporting backup status to moonlight");
            Logger.Warn(e);
        }
    }
}