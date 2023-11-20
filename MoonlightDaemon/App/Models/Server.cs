namespace MoonlightDaemon.App.Models;

public class Server
{
    public int Id { get; set; }
    public string DockerImage { get; set; }
    public List<int> Ports { get; set; }
    public int Cpu { get; set; }
    public int Memory { get; set; }
    public int PidsLimit { get; set; }
}