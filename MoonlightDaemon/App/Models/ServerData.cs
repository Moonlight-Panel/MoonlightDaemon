namespace MoonlightDaemon.App.Models;

public class ServerData
{
    public int Id { get; set; }
    public string Startup { get; set; } = "";
    public ImageData Image { get; set; } = new();
    public LimitsData Limits { get; set; } = new();
    public AllocationData MainAllocation { get; set; }
    public List<AllocationData> Allocations { get; set; } = new();
    public bool Join2Start { get; set; }
}