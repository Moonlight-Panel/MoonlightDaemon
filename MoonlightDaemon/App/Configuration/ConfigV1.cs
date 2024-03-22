namespace MoonlightDaemon.App.Configuration;

public class ConfigV1
{
    public DockerData Docker { get; set; } = new();
    public PathsData Paths { get; set; } = new();
    public ServerData Server { get; set; } = new();
    public RemoteData Remote { get; set; } = new();
    public FtpData Ftp { get; set; } = new();
    public HttpData Http { get; set; } = new();
    
    public class FtpData
    {
        public int MaxActiveConnections { get; set; } = 0;
        public int Port { get; set; } = 2021;
        public int InactivityCheckInterval { get; set; } = 60;
        public int MaxConnectionsPerUser { get; set; } = 10;
    }
    
    public class ServerData
    {
        public float MemoryOverheadMultiplier { get; set; } = 0.05f;
        public bool EnableSwap { get; set; } = true;
        public float SwapMultiplier { get; set; } = 2f;
        public string BackupProvider { get; set; } = "file";
        public string VirtualDiskFileSystem { get; set; } = "ext4";
    }
    
    public class DockerData
    {
        public string Socket { get; set; } = "unix:///var/run/docker.sock";
        public List<string> DnsServers { get; set; } = new();
        public int TmpfsSize { get; set; } = 100;
        public string HostBindIp { get; set; } = "0.0.0.0";
    }
    
    public class PathsData
    {
        public string Log { get; set; } = "/var/log/moonlight/daemon.log";
    }
    
    public class RemoteData
    {
        public string Url { get; set; } = "http://localhost:5132/";
        public string Token { get; set; } = "";
    }
    
    public class HttpData
    {
        public bool UseSsl { get; set; } = false;
        public int HttpPort { get; set; } = 8080;
        public string Fqdn { get; set; } = "";
    }
}