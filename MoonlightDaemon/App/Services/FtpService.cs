using FubarDev.FtpServer;
using FubarDev.FtpServer.AccountManagement;
using FubarDev.FtpServer.FileSystem;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Helpers.Ftp;

namespace MoonlightDaemon.App.Services;

public class FtpService : IDisposable
{
    private readonly ConfigService ConfigService;
    private readonly ServerService ServerService;

    private IFtpServerHost Server;

    public FtpService(ConfigService configService, ServerService serverService)
    {
        ConfigService = configService;
        ServerService = serverService;
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

        // Configure the ftp server
        services.Configure<FtpServerOptions>(options =>
        {
            options.ServerAddress = "0.0.0.0";
            options.Port = config.Port;
            options.MaxActiveConnections = 100;
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

    public async void Dispose()
    {
        Logger.Info("Stopping ftp server");
        await Server.StopAsync(CancellationToken.None);
    }
}