namespace MoonlightDaemon.App.ApiClients.Moonlight.Requests;

public class DdosStart
{
    public string Ip { get; set; } = "";
    public long Packets { get; set; }
}