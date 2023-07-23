namespace MoonlightDaemon.App.ApiClients.Moonlight.Requests;

public class DdosStop
{
    public string Ip { get; set; } = "";
    public long Traffic { get; set; }
}