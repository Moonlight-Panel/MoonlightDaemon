namespace MoonlightDaemon.App.Extensions;

public static class EventHandlerExtensions
{
    public static Task InvokeAsync(this EventHandler? handler)
    {
        if(handler == null)
            return Task.CompletedTask;
        
        var tasks = handler
            .GetInvocationList()
            .Select(x => new Task(() => x.DynamicInvoke(null, null)))
            .ToArray();

        foreach (var task in tasks)
        {
            task.Start();
        }
        
        return Task.CompletedTask;
    }

    public static Task InvokeAsync<T>(this EventHandler<T>? handler, T? data = default(T))
    {
        if(handler == null)
            return Task.CompletedTask;
        
        var tasks = handler
            .GetInvocationList()
            .Select(x => new Task(() => x.DynamicInvoke(null, data)))
            .ToArray();

        foreach (var task in tasks)
        {
            task.Start();
        }

        return Task.CompletedTask;
    }
}