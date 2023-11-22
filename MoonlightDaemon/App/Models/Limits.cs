namespace MoonlightDaemon.App.Models;

public class Limits
{
    public int Cpu { get; set; }
    public int Memory { get; set; }
    public int Storage { get; set; }
    public int Pids { get; set; }
    public bool DisableSwap { get; set; }
    public bool EnableOomKill { get; set; }
}