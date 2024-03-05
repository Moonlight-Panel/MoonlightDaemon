using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Enums;

namespace MoonlightDaemon.App.Http.Resources;

public class ServerListItem
{
    public int Id { get; set; }
    public ServerState State { get; set; }
    public ServerStats Stats { get; set; }
}