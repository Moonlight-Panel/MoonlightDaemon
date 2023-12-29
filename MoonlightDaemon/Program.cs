using Docker.DotNet;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Helpers.LogMigrator;
using MoonlightDaemon.App.Parsers;
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
builder.Services.AddSingleton<NodeService>();
builder.Services.AddSingleton<MoonlightService>();
builder.Services.AddSingleton<ParseService>();

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

// Add default parsers
var parseService = app.Services.GetRequiredService<ParseService>();

parseService.Register<FileParser>("file");
parseService.Register<PropertiesParser>("properties");

// Send boot signal
var moonlightService = app.Services.GetRequiredService<MoonlightService>();

Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(3));
    await moonlightService.SendBootSignal();
});

app.Run();