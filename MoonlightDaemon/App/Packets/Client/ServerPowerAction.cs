using MoonlightDaemon.App.Models.Enums;

namespace MoonlightDaemon.App.Packets.Client;

public class ServerPowerAction
{
    public int Id { get; set; }
    public PowerAction Action { get; set; }
}