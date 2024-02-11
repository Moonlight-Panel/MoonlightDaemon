using System.Net.Sockets;
using System.Net.WebSockets;
using MoonCore.Attributes;
using MoonCore.Helpers;
using MoonCore.Services;
using MoonlightDaemon.App.Configuration;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Packets;
using Newtonsoft.Json;

namespace MoonlightDaemon.App.Services;

[Singleton]
// This class is for interacting with the panel
public class MoonlightService
{
    private readonly ConfigService<ConfigV1> ConfigService;
    private readonly IServiceProvider ServiceProvider;
    private readonly HttpClient Client;
    
    // ws
    private ClientWebSocket ClientWebSocket;
    private WsPacketConnection WsPacketConnection;

    public MoonlightService(ConfigService<ConfigV1> configService, IServiceProvider serviceProvider)
    {
        ConfigService = configService;
        ServiceProvider = serviceProvider;

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
                    $"{server.Configuration.Id}/install");

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

    public async Task SendPacket(object data)
    {
        try
        {
            await WsPacketConnection!.Send(data);
        }
        catch (Exception e)
        {
            Logger.Warn("An unhandled error occured while sending packet to moonlight");
            Logger.Warn(e);
        }
    }

    public async Task ReconnectWs()
    {
        if (WsPacketConnection != null)
            await WsPacketConnection.Close();

        var remoteConfig = ConfigService.Get().Remote;

        // Setup connection
        ClientWebSocket = new ClientWebSocket();
        ClientWebSocket.Options.SetRequestHeader("Authorization", remoteConfig.Token);
        
        // Connect to remote endpoint
        var remoteUrl = remoteConfig.Url;
        
        // Replace http(s) with ws(s)
        remoteUrl = remoteUrl.Replace("https://", "wss://");
        remoteUrl = remoteUrl.Replace("http://", "ws://");
        
        await ClientWebSocket.ConnectAsync(new Uri(remoteUrl + "api/servers/node/ws"), CancellationToken.None);
        
        // Setup ws packet connection
        WsPacketConnection = new WsPacketConnection(ClientWebSocket);

        await WsPacketConnection.RegisterPacket<ServerStateUpdate>("serverStateUpdate");
        await WsPacketConnection.RegisterPacket<ServerOutputMessage>("serverOutputMessage");
        
        // Done ;)
    }
}