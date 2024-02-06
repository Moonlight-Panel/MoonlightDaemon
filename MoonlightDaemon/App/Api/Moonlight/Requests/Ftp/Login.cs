namespace MoonlightDaemon.App.Api.Moonlight.Requests.Ftp;

public class Login
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string IpAddress { get; set; }
    public int ServerId { get; set; }
}