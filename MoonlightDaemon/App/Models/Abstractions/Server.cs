using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Enums;

namespace MoonlightDaemon.App.Models.Abstractions;

public class Server
{
    public Environment RuntimeEnvironment { get; private set; }
    public Environment InstallEnvironment { get; private set; }
    public ServerConsole Console { get; private set; } = new();
    public ServerData Configuration { get; set; }

    private readonly IServiceScope ServiceScope;
    private StateMachine<ServerState> StateMachine;
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
        
        StateMachine.OnTransitioned += (_, state) => Logger.Debug($"Transitioned to {state}");
        StateMachine.OnTransitioning += (_, state) => Logger.Debug($"Transitioning to {state}");

        await StateMachine.AddTransition(ServerState.Offline, ServerState.Starting, async () =>
        {
            await SetupRuntimeEnvironment();
            await RuntimeEnvironment.Start();
        });

        #region Installing

        await StateMachine.AddTransition(ServerState.Offline, ServerState.Installing, async () =>
        {
            Directory.CreateDirectory($"/var/lib/moonlight/install/{Configuration.Id}");
            await File.WriteAllTextAsync($"/var/lib/moonlight/install/{Configuration.Id}/install.sh", "ls; pwd; curl -o server.jar https://storage.endelon-hosting.de/mlv2/server.jar; ls");

            var shellService = ServiceScope.ServiceProvider.GetRequiredService<ShellHelper>();
            await shellService.ExecuteCommand($"chmod +x /var/lib/moonlight/install/{Configuration.Id}/install.sh");

            InstallEnvironment = new(ServiceScope, new()
            {
                Gid = 0,
                Uid = 0,
                Container = new()
                {
                    Name = $"moonlight-install-{Configuration.Id}",
                    Cpu = Configuration.Limits.Cpu,
                    Memory = Configuration.Limits.Memory,
                    Disk = Configuration.Limits.Disk,
                    DisableSwap = Configuration.Limits.DisableSwap,
                    PidsLimit = Configuration.Limits.PidsLimit,
                    EnableOomKill = Configuration.Limits.EnableOomKill,
                    OverrideCommand = "/bin/bash /mnt/install/install.sh",
                    WorkingDirectory = "/mnt/server"
                },
                DockerImage = "moonlightpanel/images:installerjava",
                Ports = new()
                {
                    25565
                },
                Volumes = new()
                {
                    {
                        $"/var/lib/moonlight/volumes/{Configuration.Id}",
                        "/mnt/server"
                    },
                    {
                        $"/var/lib/moonlight/install/{Configuration.Id}",
                        "/mnt/install"
                    }
                }
            });

            InstallEnvironment.Exited += async (_, _) => { await StateMachine.TransitionTo(ServerState.Offline); };

            await InstallEnvironment.Stream.AttachToConsole(Console);

            await InstallEnvironment.Recreate();
            await InstallEnvironment.Start();
        });

        await StateMachine.AddTransition(ServerState.Installing, ServerState.Offline, async () =>
        {
            // Handle install finish here
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
            async () => { await RuntimeEnvironment.SendInput(Configuration.Image.StopCommand); });

        await StateMachine.AddTransition(ServerState.Running, ServerState.Stopping,
            async () => // requested server to stop
            {
                await RuntimeEnvironment.SendInput(Configuration.Image.StopCommand);
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

        // Destroy environment
        await RuntimeEnvironment.Destory();
        RuntimeEnvironment = null!;
    }

    private async Task SetupRuntimeEnvironment()
    {
        RuntimeEnvironment = new(ServiceScope, new()
        {
            Gid = 998,
            Uid = 998,
            Ports = new()
            {
                25565
            },
            DockerImage = Configuration.Image.DockerImage,
            Container = new()
            {
                Name = $"moonlight-runtime-{Configuration.Id}",
                Cpu = Configuration.Limits.Cpu,
                Memory = Configuration.Limits.Memory,
                Disk = Configuration.Limits.Disk,
                DisableSwap = Configuration.Limits.DisableSwap,
                PidsLimit = Configuration.Limits.PidsLimit,
                EnableOomKill = Configuration.Limits.EnableOomKill,
                WorkingDirectory = "/home/container"
            },
            Volumes = new()
            {
                {
                    $"/var/lib/moonlight/volumes/{Configuration.Id}",
                    "/home/container"
                }
            },
            Variables = new()
            {
                {
                    "STARTUP",
                    Configuration.Startup
                }
            }
        });

        RuntimeEnvironment.Exited += async (_, _) =>
        {
            if (RuntimeEnvironment.HasBeenKilled)
                return;

            if (Configuration.Join2Start)
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