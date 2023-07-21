using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using Newtonsoft.Json;

namespace MoonlightDaemon.App.Services;

public class DockerMetricsService
{
    private readonly BashHelper BashHelper;

    public DockerMetricsService(BashHelper bashHelper)
    {
        BashHelper = bashHelper;
    }

    public async Task<Container[]> GetContainers()
    {
        List<Container> containers = new();

        var content = await BashHelper.ExecuteCommand("docker stats --no-stream --format \"{{ json . }}\"");
        
        string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (string line in lines)
        {
            var x = ParseLine(line);

            if (x != null)
            {
                containers.Add(x);
            }
        }
        
        return containers.ToArray();
    }
    
    private Container? ParseLine(string line)
    {
        try
        {
            var stats = new Container();
            var raw = JsonConvert.DeserializeObject<DockerStatsRaw>(line);

            if (raw == null)
                return null;
        
            // Memory
            try
            {
                var usedMem = raw.MemUsage.Split("/")[0].Trim();
                var usedMemS = usedMem
                    .Replace("KiB", "")
                    .Replace("GiB", "")
                    .Replace("MiB", "");
                var usedMemD = (long)float.Parse(usedMemS);

                var multiplicator = 1;

                if (usedMem.Contains("KiB"))
                {
                    multiplicator = 1024;
                }
            
                if (usedMem.Contains("KiB"))
                {
                    multiplicator = 1024 * 1024;
                }
            
                if (usedMem.Contains("GiB"))
                {
                    multiplicator = 1024 * 1024 * 1024;
                }

                stats.Memory = usedMemD * multiplicator;
            }
            catch (Exception e)
            {
                stats.Memory = 0;
            }
        
            // CPU
            try
            {
                var usedCpu = raw.CpuPerc.Replace("%", "");
                stats.Cpu = double.Parse(usedCpu);
            }
            catch (Exception e)
            {
                stats.Cpu = 0;
            }
        
            // Network In
            try
            {
                var usedIn = raw.NetIo.Split("/")[0].Trim();
                var usedInS = usedIn
                    .Replace("kB", "")
                    .Replace("MB", "")
                    .Replace("GB", "");
                var usedInD = (long)float.Parse(usedInS);

                var multiplicator = 1;

                if (usedIn.Contains("kB"))
                {
                    multiplicator = 1024;
                }
            
                if (usedIn.Contains("MB"))
                {
                    multiplicator = 1024 * 1024;
                }
            
                if (usedIn.Contains("GB"))
                {
                    multiplicator = 1024 * 1024 * 1024;
                }

                stats.NetworkIn = usedInD * multiplicator;
            }
            catch (Exception e)
            {
                stats.NetworkIn = 0;
            }
        
            // Network Out
            try
            {
                var usedIn = raw.NetIo.Split("/")[1].Trim();
                var usedInS = usedIn
                    .Replace("kB", "")
                    .Replace("MB", "")
                    .Replace("GB", "");
                var usedInD = (long)float.Parse(usedInS);

                var multiplicator = 1;

                if (usedIn.Contains("kB"))
                {
                    multiplicator = 1024;
                }
            
                if (usedIn.Contains("MB"))
                {
                    multiplicator = 1024 * 1024;
                }
            
                if (usedIn.Contains("GB"))
                {
                    multiplicator = 1024 * 1024 * 1024;
                }

                stats.NetworkOut = usedInD * multiplicator;
            }
            catch (Exception e)
            {
                stats.NetworkOut = 0;
            }

            stats.Name = raw.Name;

            return stats;
        }
        catch (Exception e)
        {
            return null;
        }
    }
    
    private class DockerStatsRaw
    {
        [JsonProperty("CPUPerc")]
        public string CpuPerc { get; set; }

        [JsonProperty("Container")]
        public string Container { get; set; }

        [JsonProperty("ID")]
        public string Id { get; set; }

        [JsonProperty("MemPerc")]
        public string MemPerc { get; set; }

        [JsonProperty("MemUsage")]
        public string MemUsage { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("NetIO")]
        public string NetIo { get; set; }
    }
}