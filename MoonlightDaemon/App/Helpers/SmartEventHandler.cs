namespace MoonlightDaemon.App.Helpers;

public class SmartEventHandler
{
    private readonly List<Func<Task>> Subscribers = new();

    public static SmartEventHandler operator +(SmartEventHandler handler, Func<Task> callback)
    {
        lock (handler.Subscribers)
            handler.Subscribers.Add(callback);

        return handler;
    }

    public static SmartEventHandler operator -(SmartEventHandler handler, Func<Task> callback)
    {
        lock (handler.Subscribers)
            handler.Subscribers.Remove(callback);

        return handler;
    }

    public Task Invoke()
    {
        List<Func<Task>> callbacks;

        lock (Subscribers)
            callbacks = Subscribers.ToList();

        foreach (var callback in callbacks)
        {
            Task.Run(async () =>
            {
                try
                {
                    await callback.Invoke();
                }
                catch (Exception e)
                {
                    Logger.Warn("An unhandled error occured while processing an api callback");
                    Logger.Warn(e);
                }
            });
        }

        return Task.CompletedTask;
    }
    
    public Task ClearSubscribers()
    {
        lock (Subscribers)
            Subscribers.Clear();
        
        return Task.CompletedTask;
    }
}

public class SmartEventHandler<T>
{
    private readonly List<Func<T, Task>> Subscribers = new();

    public static SmartEventHandler<T> operator +(SmartEventHandler<T> handler, Func<T, Task> callback)
    {
        lock (handler.Subscribers)
            handler.Subscribers.Add(callback);

        return handler;
    }

    public static SmartEventHandler<T> operator -(SmartEventHandler<T> handler, Func<T, Task> callback)
    {
        lock (handler.Subscribers)
            handler.Subscribers.Remove(callback);

        return handler;
    }

    public Task Invoke(T? data = default(T))
    {
        List<Func<T, Task>> callbacks;

        lock (Subscribers)
            callbacks = Subscribers.ToList();

        foreach (var callback in callbacks)
        {
            Task.Run(async () =>
            {
                try
                {
                    await callback.Invoke(data!);
                }
                catch (Exception e)
                {
                    Logger.Warn("An unhandled error occured while processing an api callback");
                    Logger.Warn(e);
                }
            });
        }

        return Task.CompletedTask;
    }

    public Task ClearSubscribers()
    {
        lock (Subscribers)
            Subscribers.Clear();
        
        return Task.CompletedTask;
    }
}