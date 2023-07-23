using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Middleware;

public class WingsTokenMiddleware
{
    private readonly RequestDelegate Next;
    private readonly WingsConfigService WingsConfigService;

    public WingsTokenMiddleware(
        RequestDelegate next,
        WingsConfigService wingsConfigService)
    {
        Next = next;
        WingsConfigService = wingsConfigService;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (context!.Request.Headers.ContainsKey("Authorization"))
        {
            var token = context.Request.Headers["Authorization"];

            if (token == WingsConfigService.Token)
            {
                await Next(context);
                return;
            }
        }

        context.Response.StatusCode = 401;
    }
}