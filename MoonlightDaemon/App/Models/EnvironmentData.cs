namespace MoonlightDaemon.App.Models;

public class EnvironmentData
{
    public ContainerData Container { get; set; } = new();
    public Dictionary<string, string> Volumes { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new();
    public List<int> Ports { get; set; } = new();
    public string DockerImage { get; set; } = "";
    public int Uid { get; set; }
    public int Gid { get; set; }
}