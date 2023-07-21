using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Http.Middleware;

public class WingsTokenMiddleware
{
    private readonly RequestDelegate Next;
    private readonly WingsTokenService WingsTokenService;

    public WingsTokenMiddleware(
        RequestDelegate next,
        WingsTokenService wingsTokenService)
    {
        Next = next;
        WingsTokenService = wingsTokenService;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (context!.Request.Headers.ContainsKey("Authorization"))
        {
            var token = context.Request.Headers["Authorization"];

            if (token == WingsTokenService.Token)
            {
                await Next(context);
                return;
            }
        }

        context.Response.StatusCode = 401;
    }
}