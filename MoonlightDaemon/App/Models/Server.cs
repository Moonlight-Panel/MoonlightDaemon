namespace MoonlightDaemon.App.Models;

public class Server
{
    public int Id { get; set; }
    public Limits Limits { get; set; } = new();
    public List<Allocation> Allocations { get; set; } = new();
    public Image Image { get; set; } = new();
}