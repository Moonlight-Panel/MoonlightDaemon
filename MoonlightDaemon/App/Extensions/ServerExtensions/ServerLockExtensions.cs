using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerLockExtensions
{
    public static async Task Lock(this Server server)
    {
        await server.LockHandle.WaitAsync();
    }
    
    public static Task Unlock(this Server server)
    { 
        server.LockHandle.Release();
        
        return Task.CompletedTask;
    }

    public static async Task LockWhile(this Server server, Func<Task> work)
    {
        await server.Lock();

        try
        {
            await work.Invoke();
            await server.Unlock();
        }
        catch (Exception) // To ensure lock will be released on error
        {
            await server.Unlock();
            throw;
        }
    }
}