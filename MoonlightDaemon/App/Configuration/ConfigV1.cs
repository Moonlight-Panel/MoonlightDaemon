namespace MoonlightDaemon.App.Configuration;

public class ConfigV1
{
    public bool PterodactylMode { get; set; } = false;
    public StorageData Storage { get; set; } = new();
    public DockerData Docker { get; set; } = new();
    public ServerData Server { get; set; } = new();
    
    public class StorageData
    {
        public string LogPath { get; set; } = "/var/log/moonlight";
        public string VolumePath { get; set; } = "/var/lib/moonlight/volumes";
    }
    
    public class DockerData
    {
        public string HostBindIp { get; set; } = "0.0.0.0";

        public List<string> DnsServers { get; set; } = new();

        public int TmpfsSize { get; set; } = 100;
    }
    
    public class ServerData
    {
        public float MemoryOverheadMultiplier { get; set; } = 0.05f;
        public bool EnableSwap { get; set; } = true;
        public float SwapMultiplier { get; set; } = 2f;
    }
}