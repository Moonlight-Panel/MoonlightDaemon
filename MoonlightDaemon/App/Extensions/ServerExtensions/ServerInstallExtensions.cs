using Docker.DotNet;
using MoonCore.Helpers;
using MoonCore.Services;
using MoonlightDaemon.App.Configuration;
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
            
            // Fetch install configuration from panel for the server
            await server.Log("Fetching install configuration");
            var moonlightService = server.ServiceProvider.GetRequiredService<MoonlightService>();
            var configuration = await moonlightService.GetInstallConfiguration(server);

            if (configuration == null)
            {
                await server.Log("Unable to start installation process due to an error while fetching the install configuration from the panel");
                return;
            }
            
            // Ensure the install docker image exists
            try
            {
                await server.Log("Downloading docker image");
                await server.EnsureImageExists(configuration.DockerImage);
                await server.Log("Downloaded docker image");
            }
            catch (Exception e)
            {
                Logger.Error($"An error occured while downloading docker image for install container for server {server.Configuration.Id}: '{configuration.DockerImage}'");
                Logger.Error(e);
                
                await server.Log("Failed to download docker image for installation container. See daemon logs for more information");
                
                return;
            }
            
            // Write the install script to file
            var installScriptPath = PathBuilder.File(server.Configuration.GetInstallVolumePath(), "install.sh");
            await File.WriteAllTextAsync(installScriptPath, configuration.Script.Replace("\r\n", "\n"));

            // Build the container params
            var configService = server.ServiceProvider.GetRequiredService<ConfigService<ConfigV1>>();
            var container =
                server.Configuration.ToInstallContainerParameters(configService, configuration.DockerImage);

            container.Cmd = $"{configuration.Shell} /mnt/install/install.sh".Split(" ");

            // Create the container
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

            try
            {
                // Start container
                await client.Containers.StartContainerAsync(containerName, new());
            }
            catch (Exception e)
            {
                Logger.Error($"Starting the installation of server {server.Configuration.Id} has failed");
                Logger.Error(e);
                
                await client.Containers.RemoveContainerAsync(containerName, new());
                await server.Log("Starting the installation has failed");
                
                throw;
            }

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

            try
            {
                await client.Containers.RemoveContainerAsync($"moonlight-install-{server.Configuration.Id}", new());
                await server.Log("Removed install container");
            }
            catch (DockerContainerNotFoundException)
            {
                /* ignored */
            }
            catch (Exception e)
            {
                Logger.Error($"An error occured while finalizing the installation of the server {server.Configuration.Id}");
                Logger.Error(e);
            }

            await server.State.TransitionTo(ServerState.Offline);
        });
    }
}