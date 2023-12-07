using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Configuration;

namespace MoonlightDaemon.App.Services;

public class NodeService
{
    private readonly List<ServerConfiguration> BootServerConfigurations = new();
    private readonly ServerService ServerService;

    public NodeService(ServerService serverService)
    {
        ServerService = serverService;
    }

    public bool IsBooting { get; private set; } = false;
    

    public async Task StartBoot()
    {
        Logger.Info("Starting remote boot");
        
        IsBooting = true;

        lock (BootServerConfigurations)
            BootServerConfigurations.Clear();
        
    }

    public async Task AddBootServers(ServerConfiguration[] configurations)
    {
        Logger.Info($"Receiving {configurations.Length} server configurations");
        
        lock (BootServerConfigurations)
        {
            BootServerConfigurations.AddRange(configurations);
        }
    }

    public async Task FinishBoot()
    {
        Logger.Info("Received boot finish signal. Finalizing boot configuration");
        
        ServerConfiguration[] configurations;

        lock (BootServerConfigurations)
        {
            configurations = BootServerConfigurations.ToArray();
            BootServerConfigurations.Clear();
        }
        
        Logger.Info("Removing existing servers");
        await ServerService.Clear();

        Logger.Info("Loading server configurations");
        foreach (var serverConfiguration in configurations)
            await ServerService.AddFromConfiguration(serverConfiguration);

        await ServerService.Restore();

        IsBooting = false;
    }
}