using Docker.DotNet;
using Docker.DotNet.Models;
using MoonlightDaemon.App.Helpers;

namespace MoonlightDaemon.App.Services.Monitors;

public class ContainerMonitorService : IHostedService
{
    private readonly DockerClient Client;

    public ContainerMonitorService()
    {
        Client = new DockerClientConfiguration(
                new Uri("unix:///var/run/docker.sock"))
            .CreateClient();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Logger.Info("Starting docker event monitoring");
        await Client.System.MonitorEventsAsync(
            new(),
            new Progress<Message>(DockerEventHandler),
            cancellationToken
        );
    }

    private async void DockerEventHandler(Message message)
    {
        Logger.Debug($"Event > ID: {message.ID} Status: {message.Status} Action: {message.Action}");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}