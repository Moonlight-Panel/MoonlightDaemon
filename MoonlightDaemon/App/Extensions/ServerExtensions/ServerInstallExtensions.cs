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

            try
            {
                var result = await client.Containers.InspectContainerSafeAsync(containerName);

                if (result == null)
                    throw new IOException("An unknown error has occured while inspecting container");

                if (result.State.Running) // Container already existing
                    return;

                await server.Log("Removing existing container");
                await client.Containers.RemoveContainerAsync(containerName, new());
            }
            catch (DockerContainerNotFoundException)
            {
            }

            await server.Log("Downloading docker image");
            await server.EnsureImageExists("moonlightpanel/images:installerjava");
            await server.Log("Downloaded docker image");

            await server.Log("Fetching install script");
            var installScriptPath = PathBuilder.File(server.Configuration.GetInstallVolumePath(), "install.sh");
            await File.WriteAllTextAsync(installScriptPath,
                "ls -la; whoami; curl -o server.jar https://storage.endelon-hosting.de/mlv2/server.jar");

            var configService = server.ServiceProvider.GetRequiredService<ConfigService>();
            var container =
                server.Configuration.ToInstallContainerParameters(configService, "moonlightpanel/images:installerjava");

            container.Cmd = "/bin/bash /mnt/install/install.sh".Split(" ");

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
        });
    }
}