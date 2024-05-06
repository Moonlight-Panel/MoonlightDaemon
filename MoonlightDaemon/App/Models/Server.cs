using MoonCore.Helpers;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Configuration;
using MoonlightDaemon.App.Models.Enums;

namespace MoonlightDaemon.App.Models;

public class Server
{
    public ServerConfiguration Configuration { get; set; }
    public IServiceProvider ServiceProvider { get; set; }
    public StateMachine<ServerState> State { get; set; }
    public SemaphoreSlim LockHandle { get; set; }
    public ServerConsole Console { get; set; }
    public ChrootFileSystem FileSystem { get; set; }
}