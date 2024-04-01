using MoonlightDaemon.App.Exceptions;

namespace MoonlightDaemon.App.Http.Middleware;

public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate Next;

    public ExceptionHandlerMiddleware(RequestDelegate next)
    {
        Next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await Next(context);
        }
        catch (UnsafeFileAccessException)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Unsafe file access detected");
        }
        catch (FileNotFoundException)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("File not found");
        }
        catch (DirectoryNotFoundException)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Directory not found");
        }
    }
}