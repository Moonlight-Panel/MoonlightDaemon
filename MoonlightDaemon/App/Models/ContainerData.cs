namespace MoonlightDaemon.App.Models;

public class ContainerData
{
    public string Id { get; set; }
    public int Cpu { get; set; }
    public int Memory { get; set; }
    public int Disk { get; set; }
    public int PidsLimit { get; set; }
    public bool EnableOomKill { get; set; }
    public bool DisableSwap { get; set; }
    public string Name { get; set; }
    public string? OverrideCommand { get; set; }
    public string WorkingDirectory { get; set; }
}