using MoonlightDaemon.App.Models.Enums;

namespace MoonlightDaemon.App.Packets.Server;

public class ServerStateUpdate
{
    public int Id { get; set; }
    public ServerState State { get; set; }
}