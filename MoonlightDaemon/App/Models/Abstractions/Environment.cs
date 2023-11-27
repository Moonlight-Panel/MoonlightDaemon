using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Services;
using MoonlightDaemon.App.Services.Monitors;

namespace MoonlightDaemon.App.Models.Abstractions;

public class Environment
{
    public EnvironmentData Configuration { get; private set; }
    public EnvironmentStream Stream { get; private set; } = new();
    
    public EventHandler Exited { get; set; }
    public bool IsRunning { get; private set; } = false;
    public bool HasBeenKilled { get; private set; } = false;
    
    private ContainerStream ContainerStream;
    private readonly IServiceScope ServiceScope;
    private readonly DockerService DockerService;

    public Environment(IServiceScope serviceScope, EnvironmentData configuration)
    {
        ServiceScope = serviceScope;
        Configuration = configuration;
        
        ContainerStream = new();

        // Some often used services should be loaded at the creation of the environment, others can be loaded if required
        DockerService = ServiceScope.ServiceProvider.GetRequiredService<DockerService>();
        
        var containerMonitorService = ServiceScope.ServiceProvider.GetRequiredService<ContainerMonitorService>();
        
        containerMonitorService.OnContainerEvent += async (_, data) =>
        {
            if(Configuration.Container.Id != data.Id) // Ignore events of unknown containers
                return;
            
            if (data.Action == "die") // Handle container exit
            {
                IsRunning = false;
                await ContainerStream.Close();
                await Exited.InvokeAsync();
            }
        };
    }
    
    public async Task Start() // Proxy for docker container start action
    {
        IsRunning = true;
        await DockerService.StartContainer(Configuration.Container.Name);
    }
    
    public async Task Kill() // Proxy for docker container kill action
    {
        HasBeenKilled = true;
        await DockerService.KillContainer(Configuration.Container.Name);
    }

    public async Task<string[]> GetLogs() => await DockerService.GetContainerLogs(Configuration.Container.Name);

    public async Task Recreate() // Creates the environment docker container, delete if it exists
    {
        Logger.Debug($"Recreating environment {Configuration.Container.Name}");
        
        // Load required services
        var volumeHelper = ServiceScope.ServiceProvider.GetRequiredService<VolumeHelper>();
        var configService = ServiceScope.ServiceProvider.GetRequiredService<ConfigService>();

        if (await DockerService.ContainerExists(Configuration.Container.Name)) // Container exists? Delete it
        {
            // Close previous stream if connected
            if (ContainerStream.IsActive)
                await ContainerStream.Close();
            
            // Delete existing container
            await Stream.WriteSystemOutput("Removing previous container");
            await DockerService.RemoveContainer(Configuration.Container.Name);
        }
        
        // Ensuring volumes
        await Stream.WriteSystemOutput("Checking volumes and file permissions");
        foreach (var volume in Configuration.Volumes)
        {
            await volumeHelper.Ensure(volume.Key, Configuration.Uid, Configuration.Gid);
        }
        
        // Pull image if needed
        await Stream.WriteSystemOutput("Downloading docker image");
        
        await DockerService.PullImage(Configuration.DockerImage, async msg =>
        {
            if(msg != null && msg.Progress != null && msg.Progress.Total != 0)
            {
                var percent = Math.Round((float)msg.Progress.Current / msg.Progress.Total * 100);
                await Stream.WriteSystemOutput($"[ {percent}% ] {msg.Status}");
            }
        });

        await Stream.WriteSystemOutput("Downloaded docker image");

        // Build container parameters
        var container = Configuration.ToContainerParameter(configService);

        // Handle override command
        if (Configuration.Container.OverrideCommand != null)
            container.Cmd = Configuration.Container.OverrideCommand.Split(" ");
        
        // Create container
        await Stream.WriteSystemOutput("Creating container");
        var response = await DockerService.CreateContainer(container);

        // Attach the created container
        await Attach();

        // Save the container id in config
        //TODO: Save in environment itself
        Configuration.Container.Id = response.ID;
    }

    // This function can be used to attach to a existing container in order to stream the console
    public async Task Attach()
    {
        // Get the raw docker stream
        var rawStream = await DockerService.AttachContainer(Configuration.Container.Name);
        
        // And attach the container stream to it
        await ContainerStream.Attach(rawStream);
        
        // Then attach the container stream to the environment
        await ContainerStream.AttachToEnvironment(Stream);
    }
}