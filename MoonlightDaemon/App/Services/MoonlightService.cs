using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Configuration;

namespace MoonlightDaemon.App.Services;

// This class is for interacting with the panel
public class MoonlightService
{
    private readonly ConfigService ConfigService;
    private readonly HttpClient Client;

    public MoonlightService(ConfigService configService)
    {
        ConfigService = configService;

        Client = new()
        {
            BaseAddress = new Uri(ConfigService.Get().Remote.Url + "api/servers/")
        };
        
        Client.DefaultRequestHeaders.Add("Authorization", ConfigService.Get().Remote.Token);
    }

    public async Task<ServerInstallConfiguration?> GetInstallConfiguration(Server server)
    {
        try
        {
            var configuration =
                await Client.SendHandled<ServerInstallConfiguration, MoonlightException>(HttpMethod.Get,
                    $"install/{server.Configuration.Id}");

            return configuration;
        }
        catch (MoonlightException e)
        {
            Logger.Warn($"An error occured while fetching install configuration for server {server.Configuration.Id} from panel");
            Logger.Warn(e);
        }
        catch (Exception e)
        {
            Logger.Fatal($"An unhandled error occured while fetching install configuration for server {server.Configuration.Id} from panel");
            Logger.Fatal(e);
        }

        return null;
    }

    public async Task SendBootSignal()
    {
        try
        {
            await Client.SendHandled<MoonlightException>(HttpMethod.Post, "boot");
        }
        catch (MoonlightException e)
        {
            Logger.Warn("An error occured while sending the boot signal to the panel");
            Logger.Warn(e);
        }
        catch (Exception e)
        {
            Logger.Fatal("An unhandled error occured while sending the boot signal to the panel");
            Logger.Fatal(e);
        }
    }
}