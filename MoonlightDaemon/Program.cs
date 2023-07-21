using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Http.Middleware;
using MoonlightDaemon.App.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<BashHelper>();
builder.Services.AddSingleton<MetricsService>();
builder.Services.AddSingleton<WingsTokenService>();
builder.Services.AddSingleton<MountService>();
builder.Services.AddSingleton<WingsTokenService>();
builder.Services.AddSingleton<DockerMetricsService>();

var app = builder.Build();

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