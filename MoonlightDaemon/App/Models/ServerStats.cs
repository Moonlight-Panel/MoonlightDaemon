namespace MoonlightDaemon.App.Models;

public class ServerStats
{
    public long MemoryUsage { get; set; }
    public long MemoryTotal { get; set; }
    public double CpuUsage { get; set; }
    public long IoRead { get; set; }
    public long IoWrite { get; set; }
    public long NetRead { get; set; }
    public long NetWrite { get; set; }
}