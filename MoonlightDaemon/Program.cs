using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Helpers.LogMigrator;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Enums;
using MoonlightDaemon.App.Services;
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

// Services
builder.Services.AddSingleton(configService);
builder.Services.AddSingleton<MoonlightService>();
builder.Services.AddSingleton<ServerService>();
builder.Services.AddSingleton<DockerService>();

// Services / Servers
builder.Services.AddSingleton<ServerStartService>();

var app = builder.Build();

var moonlightService = app.Services.GetRequiredService<MoonlightService>();
await moonlightService.Initialize();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Task.Run(async () =>
{
    try
    {
        await Task.Delay(TimeSpan.FromSeconds(1));

        var demoServer = new Server()
        {
            Id = 1,
            DockerImage = "moonlightpanel/images:minecraft17",
            Cpu = 400,
            Memory = 4096,
            Ports = new List<int> { 25565 },
            PidsLimit = 100
        };

        var serverService = app.Services.GetRequiredService<ServerService>();
        Logger.Debug("Current server state: " + await serverService.GetServerState(demoServer));
        Logger.Debug("Setting sever state to starting");
        await serverService.SetServerState(demoServer, ServerState.Starting);
        Logger.Debug("Current server state: " + await serverService.GetServerState(demoServer));
    }
    catch (Exception e)
    {
        Logger.Fatal("Error while debugging");
        Logger.Fatal(e);
    }
});

Logger.Info("Starting http server");
app.Run();