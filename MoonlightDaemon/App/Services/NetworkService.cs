using MoonCore.Attributes;
using MoonCore.Helpers;
using MoonlightDaemon.App.Http.Resources;

namespace MoonlightDaemon.App.Services;

[Singleton]
public class NetworkService
{
    public readonly NetworkEventConnection ConnectionManager = new();

    public readonly NetworkEvent<SystemStatus> Status;
    
    public NetworkService()
    {
        Status = new("system/status", ConnectionManager);
    }
}