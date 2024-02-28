using BackgroundService = MoonCore.Abstractions.BackgroundService;

namespace MoonlightDaemon.App.Services.Monitors;

public class SystemMonitorService : BackgroundService
{
    private readonly NetworkService NetworkService;
    private readonly SystemService SystemService;

    public SystemMonitorService(NetworkService networkService, SystemService systemService)
    {
        NetworkService = networkService;
        SystemService = systemService;
    }

    public override async Task Run()
    {
        while (!Cancellation.IsCancellationRequested)
        {
            var status = await SystemService.GetStatus();

            await NetworkService.Status.Emit(status);
            
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}