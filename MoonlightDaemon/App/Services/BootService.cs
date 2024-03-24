using System.Net.Sockets;
using System.Net.WebSockets;
using MoonCore.Attributes;
using MoonCore.Helpers;
using MoonCore.Services;
using MoonlightDaemon.App.Configuration;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Models.Configuration;

namespace MoonlightDaemon.App.Services;

[Singleton]
public class BootService
{
    private readonly HttpApiClient<MoonlightException> ApiClient;
    private readonly ServerService ServerService;
    private readonly ConfigService<ConfigV1> ConfigService;
    private readonly MoonlightService MoonlightService;

    public BootService(
        HttpApiClient<MoonlightException> apiClient,
        ServerService serverService,
        ConfigService<ConfigV1> configService, MoonlightService moonlightService)
    {
        ApiClient = apiClient;
        ServerService = serverService;
        ConfigService = configService;
        MoonlightService = moonlightService;
    }

    public async Task Boot()
    {
        try
        {
            await Start();
            await FetchServers();
            await Finish();
        }
        catch (HttpRequestException e)
        {
            if (e.InnerException is SocketException socketException)
                Logger.Warn($"Unable to reach the panel in order to start booting: {socketException.Message}");
            else
            {
                Logger.Warn("An unknown error occured while booting");
                Logger.Warn(e);

                throw;
            }
        }
        catch (Exception e)
        {
            Logger.Warn("An unknown error occured while booting");
            Logger.Warn(e);
            
            throw;
        }
    }

    private async Task Start() // This method performs some cleanup operation, in case the node was booted before
    {
        Logger.Info("Preparing for boot");

        await ApiClient.Post("api/servers/node/notify/start");
        await ServerService.Clear();
    }

    private async Task FetchServers()
    {
        Logger.Info("Fetching servers from moonlight");
        
        // Load config
        var remoteConfig = ConfigService.Get().Remote;
        
        // Build websocket
        var webSocket = new ClientWebSocket();
        webSocket.Options.SetRequestHeader("Authorization", remoteConfig.Token);

        // Connect to remote endpoint
        var remoteUrl = remoteConfig.Url;
        
        // Replace http(s) with ws(s)
        remoteUrl = remoteUrl.Replace("https://", "wss://");
        remoteUrl = remoteUrl.Replace("http://", "ws://");
        remoteUrl = remoteUrl.EndsWith("/") ? remoteUrl : remoteUrl + "/";
        
        await webSocket.ConnectAsync(new Uri(remoteUrl + "api/servers/ws"), CancellationToken.None);
        
        // Setup ws packet connection
        var wsPacketConnection = new WsPacketConnection(webSocket);
        
        // Register packets
        await wsPacketConnection.RegisterPacket<int>("amount");
        await wsPacketConnection.RegisterPacket<ServerConfiguration>("serverConfiguration");
        
        // STart receiving the servers
        var amount = await wsPacketConnection.Receive<int>();
        Logger.Info($"About to receive {amount} servers");

        for (int i = 0; i < amount; i++)
        {
            var configuration = await wsPacketConnection.Receive<ServerConfiguration>();
            
            if(configuration == null)
                continue;

            await ServerService.AddFromConfiguration(configuration);
            
            Logger.Info($"Loaded server {configuration.Id} [{i + 1}/{amount}]");
        }

        Logger.Info("Fetched servers. Closing websocket connection");
        await wsPacketConnection.Close();
    }

    private async Task Finish()
    {
        Logger.Info("Finishing boot");
        await ServerService.Restore();
        
        await ApiClient.Post("api/servers/node/notify/finish");
    }
}