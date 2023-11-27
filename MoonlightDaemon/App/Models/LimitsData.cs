namespace MoonlightDaemon.App.Models;

public class LimitsData
{
    public int Cpu { get; set; }
    public int Memory { get; set; }
    public int Disk { get; set; }
    public int PidsLimit { get; set; }
    public bool EnableOomKill { get; set; }
    public bool DisableSwap { get; set; }
}