namespace MoonlightDaemon.App.Services;

public class WingsTokenService
{
    public readonly string Token;
    public readonly string Id;

    public WingsTokenService()
    {
        var lines = File.ReadAllLines("/etc/pterodactyl/config.yml");
        
        var line = lines.First(x => x.StartsWith("token: "));
        Token = line.Replace("token: ", "");
        
        line = lines.First(x => x.StartsWith("token_id: "));
        Id = line.Replace("token_id: ", "");
    }
}