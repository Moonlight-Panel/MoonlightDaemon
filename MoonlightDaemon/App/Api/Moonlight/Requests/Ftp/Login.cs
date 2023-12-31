namespace MoonlightDaemon.App.Api.Moonlight.Requests.Ftp;

public class Login
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string IpAddress { get; set; }
    public string ResourceType { get; set; }
    public int ResourceId { get; set; }
}