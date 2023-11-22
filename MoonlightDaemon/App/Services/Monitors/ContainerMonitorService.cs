using Docker.DotNet;
using Docker.DotNet.Models;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Services.Monitors;

public class ContainerMonitorService
{
    public EventHandler<ContainerEvent> OnContainerEvent { get; set; }

    private readonly DockerClient Client;
    private readonly Dictionary<string, int> IdMappings = new();

    public ContainerMonitorService()
    {
        Client = new DockerClientConfiguration(
                new Uri("unix:///var/run/docker.sock"))
            .CreateClient();
    }

    public Task Start()
    {
        Task.Run(async () =>
        {
            Logger.Info("Starting docker event monitoring");
            await Client.System.MonitorEventsAsync(
                new(),
                new Progress<Message>(DockerEventHandler),
                CancellationToken.None
            );
        });

        return Task.CompletedTask;
    }

    private async void DockerEventHandler(Message message)
    {
        if (message.Type != "container") // This listener will only handle container updates
            return;

        if (message.Action == "create") // Create => register in id mappings to reduce inspect actions
        {
            var container = await Client.Containers.InspectContainerAsync(message.ID);

            if (!container.Config.Labels.ContainsKey("Software")) // No software label => unable to check if assotiated to moonlight
                return;

            if (container.Config.Labels["Software"] != "Moonlight-Panel") // Not created by moonlight => ignore the event
                return;

            if (!container.Config.Labels.ContainsKey("ServerId")) // Somehow no server id attached => ignore the event with a warning
            {
                Logger.Warn($"Container {container.Name} was created by moonlight but has no server id attached to it. Ignoring container event. Please resolve this issue!");
                return;
            }

            int serverId = int.Parse(container.Config.Labels["ServerId"]);

            lock (IdMappings)
            {
                if (IdMappings.ContainsKey(container.ID)) // Handler for out of sync cache
                {
                    Logger.Warn("A container with the same id has been found in in mapping cache when processing a 'create' event. Maybe a 'destroy' event has been missed. Overwriting id mapping");
                    IdMappings[container.ID] = serverId;
                }
                else
                    IdMappings.Add(container.ID, serverId);
            }
        }

        int id;

        lock (IdMappings)
        {
            if (!IdMappings.ContainsKey(message.ID)) // Unknown container => ignore event
                return;

            id = IdMappings[message.ID];
        }

        // All checks have passed => emit event
        OnContainerEvent.Invoke(this, new()
        {
            Id = id,
            Action = message.Action
        });
    }
}