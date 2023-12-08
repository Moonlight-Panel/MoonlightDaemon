using System.Reflection;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Configuration;
using WsPackets.Client;
using WsPackets.Shared;

namespace MoonlightDaemon.App.Services;

// This class is for interacting with the panel
public class MoonlightService
{
    private readonly ConfigService ConfigService;
    private readonly HttpClient Client;

    private WspClient WspClient;

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

    public async Task ReconnectWebsocket()
    {
        try
        {
            // CLose previous connection if any
            if (WspClient != null && WspClient.GetConnectionsCount() > 0)
                await WspClient.Close();

            // Configure websocket connection to client
            var websocketUrl = ConfigService.Get().Remote.Url + "api/servers/ws";
            
            // Switch to websocket protocol urls
            websocketUrl = websocketUrl.Replace("https://", "wss://");
            websocketUrl = websocketUrl.Replace("http://", "ws://");
            
            var assemblyPrefix = "MoonlightDaemon.App.Packets";
            var resolver = new AssemblyTypeResolver(Assembly.GetExecutingAssembly(), assemblyPrefix);
            WspClient = new(websocketUrl, resolver);

            // Add new connection
            await WspClient.AddConnection(options =>
            {
                options.SetRequestHeader("Authorization", ConfigService.Get().Remote.Token);
            });
        }
        catch (Exception e)
        {
            Logger.Fatal("An unhealed exception occured while connecting to the panel via websockets");
            Logger.Fatal(e);
        }
    }

    public async Task SendWsPacket(object data)
    {
        if (WspClient.GetConnectionsCount() < 1)
        {
            try
            {
                await WspClient.AddConnection(options =>
                {
                    options.SetRequestHeader("Authorization", ConfigService.Get().Remote.Token);
                }); 
            }
            catch (Exception e)
            {
                Logger.Fatal("An unhandled error occured while reestablishing a broken ws connection");
                Logger.Fatal(e);
            }
        }

        await WspClient.Send(data);
    }
}