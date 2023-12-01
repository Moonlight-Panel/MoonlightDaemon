using Docker.DotNet;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Helpers.LogMigrator;
using MoonlightDaemon.App.Models.Configuration;
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
builder.Services.AddSingleton<ServerService>();

// Services / Monitors
builder.Services.AddSingleton<ContainerMonitorService>();

// Docker Client
builder.Services.AddSingleton(
    new DockerClientConfiguration(
        new Uri(
            configService.Get().Docker.Socket
        )
    ).CreateClient()
);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Auto start background services
app.Services.GetRequiredService<ContainerMonitorService>();

var serverService = app.Services.GetRequiredService<ServerService>();

await serverService.AddFromConfiguration(new ServerConfiguration()
{
    Id = 1,
    StartupCommand = "java -jar server.jar",
    Image = new()
    {
        DockerImage = "moonlightpanel/images:minecraft17",
        StopCommand = "stop"
    },
    Limits = new()
    {
        Cpu = 400,
        Memory = 4096,
        Disk = 10240
    },
    Allocations = new()
    {
        new()
        {
            Port = 25565
        }
    },
    MainAllocation = new()
    {
        Port = 25565
    }
});

await serverService.Restore();

var server = await serverService.GetById(1);

if (server == null)
    throw new Exception(":(");

server.Console.OnNewLogMessage += (_, line) =>
{
    Console.WriteLine(line);
};

server.State.OnTransitioned += (_, state) => Logger.Debug($"Transitioned to {state}");

Console.ReadLine();

if (server.State.State == ServerState.Offline)
{
    await server.Start();
    Console.ReadLine();
}

await server.Stop();
/*
var server = await serverService.GetById(1);

if (server == null)
    throw new Exception(":(");

server.Stream.OnOutput += (_, line) =>
{
    Console.WriteLine(line);
};

server.State.OnTransitioned += (_, state) => Logger.Debug($"Transitioned to {state}");

await server.Start();

Console.ReadLine();

await server.Kill();
*/
app.Run();