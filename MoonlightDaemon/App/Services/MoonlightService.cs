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
    private readonly HttpApiClient<MoonlightException> ApiClient;

    public MoonlightService(HttpApiClient<MoonlightException> apiClient)
    {
        ApiClient = apiClient;
    }

    public async Task<ServerInstallConfiguration?> GetInstallConfiguration(Server server)
    {
        return await ApiClient.Get<ServerInstallConfiguration>($"api/servers/{server.Configuration.Id}/install");
    }

    public async Task<ServerConfiguration?> GetConfiguration(Server server)
    {
        return await ApiClient.Get<ServerConfiguration>($"api/servers/{server.Configuration.Id}");
    }

    public async Task ReportBackupStatus(Server server, int backupId, BackupStatus status)
    {
        await ApiClient.Post($"api/servers/{server.Configuration.Id}/backups/{backupId}", status);
    }
}