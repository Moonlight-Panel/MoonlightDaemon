using Docker.DotNet;
using MoonCore.Helpers;
using MoonCore.Extensions;
using MoonCore.Services;
using MoonlightDaemon.App.Configuration;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Parsers;
using MoonlightDaemon.App.Services;

Directory.CreateDirectory("/etc/moonlight");
Directory.CreateDirectory("/var/lib/moonlight");
Directory.CreateDirectory("/var/lib/moonlight/volumes");

// Build app
var builder = WebApplication.CreateBuilder(args);

var configService = new ConfigService<ConfigV1>("/etc/moonlight/config.json");

// Setup logger
Logger.Setup(logInConsole: true, logInFile: true, logPath: configService.Get().Paths.Log,
    isDebug: args.Contains("--debug"));
builder.Logging.MigrateToMoonCore();

var config =
    new ConfigurationBuilder().AddJsonString(
        "{\"LogLevel\":{\"Default\":\"Information\",\"Microsoft.AspNetCore\":\"Warning\"}}");
builder.Logging.AddConfiguration(config.Build());

//
Logger.Info("Starting MoonlightDaemon v2");

// Configure kestrel
builder.Services.AddControllers();

// Services
builder.Services.AddSingleton(configService);
builder.Services.ConstructMoonCoreDi<Program>();

// Docker Client
builder.Services.AddSingleton(
    new DockerClientConfiguration(
        new Uri(
            configService.Get().Docker.Socket
        )
    ).CreateClient()
);

// Http API Client
builder.Services.AddSingleton(
    new HttpApiClient<MoonlightException>(
        configService.Get().Remote.Url,
        configService.Get().Remote.Token
    )
);

var app = builder.Build();

app.UseRouting();
app.MapControllers();

// Auto start background services
app.Services.StartBackgroundServices<Program>();

// Add default parsers
var parseService = app.Services.GetRequiredService<ParseService>();

parseService.Register<FileParser>("file");
parseService.Register<PropertiesParser>("properties");

// Run delayed tasks
Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(3));

    // Boot
    var bootService = app.Services.GetRequiredService<BootService>();
    await bootService.Boot();

    // Start ftp server
    var ftpService = app.Services.GetRequiredService<FtpService>();
    await ftpService.Start();
});

app.Run();