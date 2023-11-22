using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Helpers.LogMigrator;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Services;
using MoonlightDaemon.App.Services.Monitors;
using MoonlightDaemon.App.Services.Servers;
using Serilog;

// Build app
var builder = WebApplication.CreateBuilder(args);
var configService = new ConfigService();

// Setup loggers
var logConfig = new LoggerConfiguration();

logConfig = logConfig.Enrich.FromLogContext()
    .WriteTo.File(configService.Get().Storage.LogPath)
    .WriteTo.Console(
        outputTemplate:
        "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");

if (builder.Environment.IsDevelopment())
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

//
Logger.Info("Starting MoonlightDaemon");

// Configure kestrel
builder.Services.AddControllers();

// Helpers
builder.Services.AddSingleton<ShellHelper>();
builder.Services.AddSingleton<VolumeHelper>();

// Services
builder.Services.AddSingleton(configService);
builder.Services.AddSingleton<MoonlightService>();
builder.Services.AddSingleton<ServerService>();
builder.Services.AddSingleton<DockerService>();

// Services / Servers
builder.Services.AddSingleton<ServerStartService>();

// Services / Monitors
builder.Services.AddSingleton<ContainerMonitorService>();

var app = builder.Build();

// Autostart services
var moonlightService = app.Services.GetRequiredService<MoonlightService>();
await moonlightService.Initialize();

var containerMonitorService = app.Services.GetRequiredService<ContainerMonitorService>();
await containerMonitorService.Start();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Logger.Info("Starting http server");

Task.Run(async () => {
    var server = new Server();
    server.Id = 1;
    
    server.Image = new()
    {
        DockerImage = "moonlightpanel/images:minecraft17"
    };

    server.Allocations = new()
    {
        new()
        {
            Port = 25565
        }
    };

    server.Limits = new()
    {
        Cpu = 100,
        DisableSwap = false,
        EnableOomKill = false,
        Memory = 4096,
        Pids = 100,
        Storage = 10240
    };

    var serverService = app.Services.GetRequiredService<ServerService>();
    await serverService.SetServerState(server, ServerState.Starting);
});

app.Run();