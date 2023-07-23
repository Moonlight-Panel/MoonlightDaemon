using MoonlightDaemon.App.ApiClients.Moonlight;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Http.Middleware;
using MoonlightDaemon.App.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Starting moonlight daemon");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<BashHelper>();
builder.Services.AddSingleton<MetricsService>();
builder.Services.AddSingleton<WingsConfigService>();
builder.Services.AddSingleton<MountService>();
builder.Services.AddSingleton<WingsConfigService>();
builder.Services.AddSingleton<DockerMetricsService>();
builder.Services.AddSingleton<MoonlightApiHelper>();
builder.Services.AddSingleton<FirewallService>();
builder.Services.AddSingleton<DDosDetectionService>();

var app = builder.Build();

_ = app.Services.GetRequiredService<DDosDetectionService>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseMiddleware<WingsTokenMiddleware>();
app.MapControllers();

app.Run("http://0.0.0.0:8081");