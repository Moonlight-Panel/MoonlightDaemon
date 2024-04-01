using FubarDev.FtpServer;
using FubarDev.FtpServer.AccountManagement;
using FubarDev.FtpServer.FileSystem;
using MoonCore.Attributes;
using MoonCore.Helpers;
using MoonCore.Services;
using MoonlightDaemon.App.Configuration;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Helpers.Ftp;

namespace MoonlightDaemon.App.Services;

[Singleton]
public class FtpService : IDisposable
{
    private readonly Dictionary<string, int> Sessions = new();
    
    private readonly ConfigService<ConfigV1> ConfigService;
    private readonly HttpApiClient<MoonlightException> HttpApiClient;
    private readonly ServerService ServerService;

    private IFtpServerHost Server;

    public FtpService(
        ConfigService<ConfigV1> configService,
        ServerService serverService,
        HttpApiClient<MoonlightException> httpApiClient)
    {
        ConfigService = configService;
        ServerService = serverService;
        HttpApiClient = httpApiClient;
    }

    public async Task Start()
    {
        Logger.Info("Starting ftp server");
        
        var config = ConfigService.Get().Ftp;

        // Setup di for ftp server itself
        var services = new ServiceCollection();

        // Configure ftp services...
        services.AddFtpServer(_ => {});
        services.AddSingleton<IMembershipProvider, FtpAuthenticator>();
        services.AddSingleton<IAccountDirectoryQuery, FtpAccountDirectoryQuery>();
        services.AddSingleton<IFileSystemClassFactory, FtpFileSystemClassFactory>();
        
        // ... and add linking services (e.g. ConfigService)
        services.AddSingleton(ConfigService);
        services.AddSingleton(ServerService);
        services.AddSingleton(HttpApiClient);
        services.AddSingleton(this);

        // Configure the ftp server
        services.Configure<FtpServerOptions>(options =>
        {
            options.ServerAddress = "0.0.0.0";
            options.Port = config.Port;
            options.MaxActiveConnections = config.MaxActiveConnections;
            options.ConnectionInactivityCheckInterval =
                TimeSpan.FromSeconds(config.InactivityCheckInterval);
        });

        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        // Get and start the ftp server
        Server = serviceProvider.GetRequiredService<IFtpServerHost>();
        await Server.StartAsync(CancellationToken.None);
        
        Logger.Info($"Ftp server listening on 0.0.0.0:{config.Port}");
    }

    public Task<bool> RegisterSession(string identifier)
    {
        var config = ConfigService.Get().Ftp;
        
        lock (Sessions)
        {
            if (Sessions.ContainsKey(identifier) && Sessions[identifier] >= config.MaxConnectionsPerServerAndUser)
                return Task.FromResult(false);
            
            if (Sessions.ContainsKey(identifier))
                Sessions[identifier] += 1;
            else
                Sessions.Add(identifier, 1);
        }
        
        return Task.FromResult(true);
    }

    public Task UnregisterSession(string identifier)
    {
        lock (Sessions)
        {
            if (Sessions.ContainsKey(identifier))
            {
                Sessions[identifier] =- 1;

                if (Sessions[identifier] < 1)
                    Sessions.Remove(identifier);
            }
        }
        
        return Task.CompletedTask;
    }

    public async void Dispose()
    {
        Logger.Info("Stopping ftp server");
        await Server.StopAsync(CancellationToken.None);
    }
}