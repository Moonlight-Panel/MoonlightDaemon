using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Enums;

namespace MoonlightDaemon.App.Models.Abstractions;

public class Server
{
    public Environment RuntimeEnvironment { get; private set; }
    public Environment InstallEnvironment { get; private set; }
    public ServerConsole Console { get; private set; } = new();
    public ServerData Configuration { get; private set; }
    public StateMachine<ServerState> StateMachine { get; private set; }

    private readonly IServiceScope ServiceScope;
    private Join2Start Join2Start;

    public Server(IServiceScope serviceScope)
    {
        ServiceScope = serviceScope;
    }

    public async Task Bind(ServerData serverData) // Bind a server handler to a specific server configuration
    {
        Configuration = serverData;

        await SetupStateMachine();

        Console = new();
    }

    public async Task SetState(ServerState state) => await StateMachine.TransitionTo(state);

    private async Task SetupStateMachine()
    {
        StateMachine = new(ServerState.Offline);
        
        await StateMachine.AddTransition(ServerState.Offline, ServerState.Starting, async () =>
        {
            await SetupRuntimeEnvironment();
            await RuntimeEnvironment.Start();
        });

        #region Installing

        await StateMachine.AddTransition(ServerState.Offline, ServerState.Installing, async () =>
        {
            await Console.WriteSystemOutput("Preparing for installation");
            Directory.CreateDirectory($"/var/lib/moonlight/install/{Configuration.Id}");

            await Console.WriteSystemOutput("Fetched install script");
            await File.WriteAllTextAsync($"/var/lib/moonlight/install/{Configuration.Id}/install.sh",
                "ls; pwd; curl -o server.jar https://storage.endelon-hosting.de/mlv2/server.jar; ls");

            var shellService = ServiceScope.ServiceProvider.GetRequiredService<ShellHelper>();
            await shellService.ExecuteCommand($"chmod +x /var/lib/moonlight/install/{Configuration.Id}/install.sh");

            await Console.WriteSystemOutput("Starting installation");
            var config = Configuration.ToInstallEnvironmentData();

            config.Uid = 0;
            config.Gid = 0;

            InstallEnvironment = new(ServiceScope, config);

            InstallEnvironment.Exited += async (_, _) => { await StateMachine.TransitionTo(ServerState.Offline); };

            await InstallEnvironment.Stream.AttachToConsole(Console);

            await InstallEnvironment.Recreate();
            await InstallEnvironment.Start();
        });

        await StateMachine.AddTransition(ServerState.Installing, ServerState.Offline, async () =>
        {
            Logger.Debug("Cleaning up install volume");
            await Console.WriteSystemOutput("Cleaning up install volume");
            Directory.Delete($"/var/lib/moonlight/install/{Configuration.Id}", true);
        });

        #endregion

        await StateMachine.AddTransition(ServerState.Starting, ServerState.Running); // (detected keyword in console)

        #region Handle crash

        await StateMachine.AddTransition(ServerState.Starting, ServerState.Offline, async () => // Crash while starting
        {
            await HandleCrash();
        });

        await StateMachine.AddTransition(ServerState.Running, ServerState.Offline, async () => // Crash while running
        {
            await HandleCrash();
        });

        #endregion

        #region Request stopping

        await StateMachine.AddTransition(ServerState.Starting, ServerState.Stopping,
            async () => { await Console.WriteInput(Configuration.Image.StopCommand); });

        await StateMachine.AddTransition(ServerState.Running, ServerState.Stopping,
            async () => // requested server to stop
            {
                await Console.WriteInput(Configuration.Image.StopCommand);
            });

        #endregion

        await StateMachine.AddTransition(ServerState.Stopping, ServerState.Offline,
            async () => // Exited or request to kill
            {
                await DestroyRuntimeEnvironment();
            });

        #region Join2Start

        await StateMachine.AddTransition(ServerState.Stopping, ServerState.Join2Start, async () =>
        {
            await DestroyRuntimeEnvironment();

            Join2Start = new(Configuration.MainAllocation.Port, StateMachine);
            await Join2Start.Start();
        });

        await StateMachine.AddTransition(ServerState.Join2Start, ServerState.Offline, async () =>
        {
            await Join2Start.Stop();
            Join2Start = null!;
        });

        await StateMachine.AddTransition(ServerState.Join2Start, ServerState.Starting, async () =>
        {
            await Join2Start.Stop();
            Join2Start = null!;

            await SetupRuntimeEnvironment();
            await RuntimeEnvironment.Start();
        });

        #endregion
    }

    private async Task DestroyRuntimeEnvironment()
    {
        // In order to go from stopping to offline, we need to kill the container
        // the normal stopping though sending the stop command has been done from running to stopping
        if (RuntimeEnvironment.IsRunning)
            await RuntimeEnvironment.Kill();

        // Reset
        RuntimeEnvironment = null!;
    }

    private async Task SetupRuntimeEnvironment()
    {
        var config = Configuration.ToRuntimeEnvironmentData();

        config.Uid = 998;
        config.Gid = 998;

        RuntimeEnvironment = new(ServiceScope, config);

        RuntimeEnvironment.Exited += async (_, _) =>
        {
            if (RuntimeEnvironment.HasBeenKilled)
                return;

            if (Configuration.Join2Start && StateMachine.State == ServerState.Stopping)
                await StateMachine.TransitionTo(ServerState.Join2Start);
            else
                await StateMachine.TransitionTo(ServerState.Offline);
        };

        await RuntimeEnvironment.Stream.AttachToConsole(Console);

        await RuntimeEnvironment.Recreate();
    }

    private async Task HandleCrash()
    {
        Logger.Debug("Crashed :/");
    }
}