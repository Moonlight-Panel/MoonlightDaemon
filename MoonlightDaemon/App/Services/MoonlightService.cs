using System.Net.Sockets;
using System.Reflection;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Packets.Client;
using WsPackets.Client;
using WsPackets.Shared;

namespace MoonlightDaemon.App.Services;

// This class is for interacting with the panel
public class MoonlightService
{
    private readonly ConfigService ConfigService;
    private readonly IServiceProvider ServiceProvider;
    private readonly HttpClient Client;

    private WspClient WspClient;

    public MoonlightService(ConfigService configService, IServiceProvider serviceProvider)
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
        catch (HttpRequestException requestException)
        {
            if (requestException.InnerException is SocketException socketException)
            {
                // If the panel was offline and will start again this error will be resolved when the panel sends a boot signal to the nodes
                Logger.Warn($"Unable to start boot process from this device. Panel is unreachable: {socketException.Message}");
            }
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

            // Configure event handlers
            WspClient.OnPacket += HandlePacket;

            // Add new connection
            var connection = await WspClient.AddConnection(options =>
            {
                options.SetRequestHeader("Authorization", ConfigService.Get().Remote.Token);
            });

            connection.OnLogError += message => Logger.Warn($"WsPackets: {message}");
        }
        catch (Exception e)
        {
            Logger.Fatal("An unhealed exception occured while connecting to the panel via websockets");
            Logger.Fatal(e);
        }
    }

    private async void HandlePacket(object? _, object data)
    {
        if (data is ServerConsoleSubscribe serverConsoleSubscribe)
        {
            var serverService = ServiceProvider.GetRequiredService<ServerService>();
            await serverService.SubscribeToConsole(serverConsoleSubscribe.Id);
        }

        if (data is ServerPowerAction serverPowerAction)
        {
            var serverService = ServiceProvider.GetRequiredService<ServerService>();
            
            var server = await serverService.GetById(serverPowerAction.Id);

            if (server == null)
            {
                Logger.Warn($"Received power action for non existing server with id {serverPowerAction.Id}");
                return;
            }

            switch (serverPowerAction.Action)
            {
                case PowerAction.Install:
                    await server.Reinstall();
                    break;
                
                case PowerAction.Start:
                    await server.Start();
                    break;
                
                case PowerAction.Stop:
                    await server.Stop();
                    break;
                
                case PowerAction.Kill:
                    await server.Kill();
                    break;
            }
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