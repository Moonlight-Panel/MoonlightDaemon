using MoonCore.Services;
using MoonlightDaemon.App.Configuration;

namespace MoonlightDaemon.App.Http.Middleware;

public class TokenAuthenticationMiddleware
{
    private readonly RequestDelegate Next;
    private readonly ConfigService<ConfigV1> ConfigService;

    public TokenAuthenticationMiddleware(RequestDelegate next, ConfigService<ConfigV1> configService)
    {
        Next = next;
        ConfigService = configService;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey("Authorization"))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Authorization header is missing");
            return;
        }

        var token = context.Request.Headers["Authorization"];
        
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Authorization header is missing");
            return;
        }

        if (token != ConfigService.Get().Remote.Token)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Authorization header is missing");
            return;
        }
        
        await Next(context);
    }
}