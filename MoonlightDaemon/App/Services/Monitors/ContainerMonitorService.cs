using Docker.DotNet;
using Docker.DotNet.Models;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Events;

namespace MoonlightDaemon.App.Services.Monitors;

public class ContainerMonitorService
{
    public SmartEventHandler<ContainerMonitorEvent> OnContainerEvent { get; set; } = new();
    
    private readonly DockerClient Client;

    public ContainerMonitorService(ConfigService configService)
    {
        Client = new DockerClientConfiguration(
                new Uri(configService.Get().Docker.Socket))
            .CreateClient();

        Task.Run(Work);
    }

    public async Task Work()
    {
        while (true)
        {
            try
            {
                await Client.System.MonitorEventsAsync(
                    new(),
                    new Progress<Message>(Handler)
                );

                Logger.Warn("Container monitor stream exited. Restarting");
            }
            catch (Exception e)
            {
                Logger.Warn("Error while monitoring containers");
                Logger.Warn(e);
                    
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }

    private async void Handler(Message message)
    {
        if(message.Type != "container")
            return;
        
        try
        {
            await OnContainerEvent.Invoke(new ContainerMonitorEvent()
            {
                Id = message.ID,
                Action = message.Action
            });
        }
        catch (Exception e)
        {
            Logger.Warn("Unhandled error while emitting container monitor event");
            Logger.Warn(e);
        }
    }
}