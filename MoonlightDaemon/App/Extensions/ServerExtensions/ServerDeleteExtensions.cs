using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions.ServerExtensions;

public static class ServerDeleteExtensions
{
    public static async Task Delete(this Server server)
    {
        await server.Destroy();
        
        await server.LockWhile(async () =>
        {
            if(Directory.Exists(server.Configuration.GetInstallVolumePath()))
                Directory.Delete(server.Configuration.GetInstallVolumePath(), true);
            
            if(Directory.Exists(server.Configuration.GetRuntimeVolumePath()))
                Directory.Delete(server.Configuration.GetRuntimeVolumePath(), true);
        });
    }
}