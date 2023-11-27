using Docker.DotNet;
using Docker.DotNet.Models;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Events;

namespace MoonlightDaemon.App.Services.Monitors;

public class ContainerMonitorService
{
    public EventHandler<ContainerMonitorEvent> OnContainerEvent { get; set; }
    
    private readonly DockerClient Client;

    public ContainerMonitorService(ConfigService configService)
    {
        Client = new DockerClientConfiguration(
                new Uri(configService.Get().Docker.Socket))
            .CreateClient();
    }

    public Task Start()
    {
        Task.Run(async () =>
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
        });

        return Task.CompletedTask;
    }

    private async void Handler(Message message)
    {
        if(message.Type != "container")
            return;
        
        await OnContainerEvent.InvokeAsync(new ContainerMonitorEvent()
        {
            Id = message.ID,
            Action = message.Action
        });
    }
}