using Docker.DotNet;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerInstallExtensions
{
    public static async Task Reinstall(this Server server)
    {
        await server.LockWhile(async () =>
        {
            // Check server state
            if (!await server.State.CanTransitionTo(ServerState.Installing))
                return;

            // Begin installation
            var containerName = $"moonlight-install-{server.Configuration.Id}";
            var client = server.ServiceProvider.GetRequiredService<DockerClient>();

            await server.Log("Checking volumes and file permissions");

            // We explicitly set it to root(0, 0)
            await server.EnsureRuntimeVolume(0, 0);
            await server.EnsureInstallVolume(0, 0);

            // Remove existing container if it exists
            var existingContainer = await client.Containers.InspectContainerSafeAsync(containerName);
            
            if(existingContainer != null && existingContainer.State.Running)
                return;

            if (existingContainer != null)
            {
                await server.Log("Removing existing container");
                await client.Containers.RemoveContainerAsync(containerName, new());
            }

            await server.Log("Downloading docker image");
            await server.EnsureImageExists("moonlightpanel/images:installerjava");
            await server.Log("Downloaded docker image");

            await server.Log("Fetching install configuration");
            var moonlightService = server.ServiceProvider.GetRequiredService<MoonlightService>();
            var configuration = await moonlightService.GetInstallConfiguration(server);

            if (configuration == null)
            {
                await server.Log("Unable to start installation process due to an error while fetching the install configuration from the panel");
                return;
            }
            
            var installScriptPath = PathBuilder.File(server.Configuration.GetInstallVolumePath(), "install.sh");
            await File.WriteAllTextAsync(installScriptPath, configuration.Script);

            var configService = server.ServiceProvider.GetRequiredService<ConfigService>();
            var container =
                server.Configuration.ToInstallContainerParameters(configService, configuration.DockerImage);

            container.Cmd = $"{configuration.Shell} /mnt/install/install.sh".Split(" ");

            await server.Log("Creating container");
            await client.Containers.CreateContainerAsync(container);

            // Attach to console. Attach stream to console stream
            var stream = await client.Containers.AttachContainerAsync(containerName, true, new()
            {
                Stderr = true,
                Stream = true,
                Stdin = true,
                Stdout = true
            });

            await server.Console.Attach(stream);

            // Start container
            await client.Containers.StartContainerAsync(containerName, new());

            // Set new state and release lock
            await server.State.TransitionTo(ServerState.Installing);
        });
    }

    public static async Task FinalizeInstall(this Server server)
    {
        await server.LockWhile(async () =>
        {
            await server.Log("Finalizing installation");

            // Volume
            var volumePath = server.Configuration.GetInstallVolumePath();
            Directory.Delete(volumePath, true);

            await server.Log("Removed install volume data");

            // Container
            var client = server.ServiceProvider.GetRequiredService<DockerClient>();
            await client.Containers.RemoveContainerAsync($"moonlight-install-{server.Configuration.Id}", new());

            await server.Log("Removed install container");
            
            await server.State.TransitionTo(ServerState.Offline);
        });
    }
}