namespace MoonlightDaemon.App.Models.Configuration;

public class ServerConfiguration
{
    public int Id { get; set; }
    public string StartupCommand { get; set; } = "";
    public AllocationData MainAllocation { get; set; }
    public LimitsData Limits { get; set; } = new();
    public ImageData Image { get; set; } = new();
    public List<AllocationData> Allocations { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new();
    public string ParseConfigurations { get; set; } = "[]"; 
    
    public class LimitsData
    {
        public int Cpu { get; set; }
        public int Memory { get; set; }
        public int Disk { get; set; }
        public bool DisableSwap { get; set; }
        public int PidsLimit { get; set; } = 100;
        public bool EnableOomKill { get; set; }
    }
    
    public class ImageData
    {
        public string DockerImage { get; set; }
        public string StopCommand { get; set; }
        public string OnlineDetection { get; set; }
    }
    
    public class AllocationData
    {
        public int Port { get; set; }
    }
}