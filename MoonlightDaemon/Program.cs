using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Helpers.LogMigrator;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Abstractions;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Services;
using MoonlightDaemon.App.Services.Monitors;
using Serilog;

// Build app
var builder = WebApplication.CreateBuilder(args);

var configService = new ConfigService();

// Setup loggers
#region Setup logging

var logConfig = new LoggerConfiguration();

logConfig = logConfig.Enrich.FromLogContext()
    .WriteTo.File(configService.Get().Paths.Log)
    .WriteTo.Console(
        outputTemplate:
        "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");

if (builder.Environment.IsDevelopment() || args.Contains("--debug"))
    logConfig = logConfig.MinimumLevel.Debug();
else
    logConfig = logConfig.MinimumLevel.Information();

Log.Logger = logConfig.CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddProvider(new LogMigrateProvider());

var config =
    new ConfigurationBuilder().AddJsonString(
        "{\"LogLevel\":{\"Default\":\"Information\",\"Microsoft.AspNetCore\":\"Warning\"}}");
builder.Logging.AddConfiguration(config.Build());


#endregion

//
Logger.Info("Starting MoonlightDaemon v2");

// Configure kestrel
builder.Services.AddControllers();

// Helpers
builder.Services.AddSingleton<ShellHelper>();
builder.Services.AddSingleton<VolumeHelper>();

// Services
builder.Services.AddSingleton(configService);
builder.Services.AddSingleton<DockerService>();

// Services / Monitors
builder.Services.AddSingleton<ContainerMonitorService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Autostart monitors
var containerMonitorService = app.Services.GetRequiredService<ContainerMonitorService>();
await containerMonitorService.Start();

// Debug
var scope = app.Services.CreateScope();
var server = new Server(scope);

await server.Bind(new ServerData()
{
    Id = 1,
    Startup = "java -jar server.jar",
    Join2Start = false,
    Image = new()
    {
        StopCommand = "stop",
        DockerImage = "moonlightpanel/images:minecraft17"
    },
    Allocations = new()
    {
        new()
        {
            Port = 25565
        }
    },
    Limits = new()
    {
        Cpu = 400,
        Disk = 10240,
        Memory = 4096,
        DisableSwap = false,
        PidsLimit = 100,
        EnableOomKill = false
    },
    MainAllocation = new()
    {
        Port = 25565
    }
});

//server.Console.OnAnyOutput += (_, msg) => Console.WriteLine($"[{server.Configuration.Id}] > {msg}");

//await server.SetState(ServerState.Installing);

Console.ReadLine();

await server.SetState(ServerState.Starting);

var msg = Console.ReadLine();

await server.Console.WriteInput(msg ?? "");

Console.ReadLine();

await server.SetState(ServerState.Stopping);

app.Run();