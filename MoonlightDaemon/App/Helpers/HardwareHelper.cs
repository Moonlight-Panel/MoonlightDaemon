using System.Globalization;
using MoonCore.Attributes;
using static MoonlightDaemon.App.Http.Resources.SystemStatus.HardwareInformationData;

namespace MoonlightDaemon.App.Helpers;

[Singleton]
public class HardwareHelper
{
    public async Task<CpuCoreData[]> GetCpuDetails()
    {
        var result = new List<CpuCoreData>();

        var lines = await File.ReadAllLinesAsync("/proc/cpuinfo");

        var core = new CpuCoreData();

        foreach (var line in lines)
        {
            if (line.StartsWith("power management"))
            {
                result.Add(core);
                core = new();
            }

            if (line.StartsWith("model name"))
                core.Name = line.Split(":")[1].Trim();

            if (line.StartsWith("processor"))
                core.Id = int.Parse(line.Split(":")[1].Trim());
        }

        var usages = await GetCpuUsages();

        foreach (var cpuCore in result)
        {
            cpuCore.Usage = usages[cpuCore.Id];
        }

        return result.ToArray();
    }

    public async Task<TimeSpan> GetUptime()
    {
        var uptimeText = await File.ReadAllTextAsync("/proc/uptime");
        var values = uptimeText.Split(" ");
        var seconds = double.Parse(values[0], CultureInfo.InvariantCulture);

        return TimeSpan.FromSeconds(seconds);
    }

    public async Task<double[]> GetCpuUsages()
    {
        var linesBefore = await File.ReadAllLinesAsync("/proc/stat");
        await Task.Delay(1000); // Wait for 1 second
        var linesAfter = await File.ReadAllLinesAsync("/proc/stat");

        var cpuDataBefore = linesBefore
            .Where(line => line.StartsWith("cpu"))
            .Select(line => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(long.Parse)
                .ToArray())
            .ToList();

        var cpuDataAfter = linesAfter
            .Where(line => line.StartsWith("cpu"))
            .Select(line => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(long.Parse)
                .ToArray())
            .ToList();

        var numCores = Environment.ProcessorCount;
        var cpuUsagePerCore = new double[numCores];

        for (int i = 0; i < numCores; i++)
        {
            var beforeIdle = cpuDataBefore[i][3];
            var beforeTotal = cpuDataBefore[i].Sum();
            var afterIdle = cpuDataAfter[i][3];
            var afterTotal = cpuDataAfter[i].Sum();

            double idleDelta = afterIdle - beforeIdle;
            double totalDelta = afterTotal - beforeTotal;
            
            var usage = 100.0 * (1.0 - idleDelta / totalDelta);
            cpuUsagePerCore[i] = usage;
        }

        return cpuUsagePerCore;
    }

    public async Task<MemoryData> GetMemoryDetails()
    {
        var result = new MemoryData();

        var memInfoText = await File.ReadAllLinesAsync("/proc/meminfo");

        foreach (var line in memInfoText)
        {
            if (line.StartsWith("MemTotal:"))
                result.Total = 1024 * long.Parse(line.Replace("MemTotal:", "").Replace("kB", "").Trim());

            if (line.StartsWith("MemFree:"))
                result.Free = 1024 * long.Parse(line.Replace("MemFree:", "").Replace("kB", "").Trim());

            if (line.StartsWith("MemAvailable:"))
                result.Available = 1024 * long.Parse(line.Replace("MemAvailable:", "").Replace("kB", "").Trim());

            if (line.StartsWith("Cached:"))
                result.Cached = 1024 * long.Parse(line.Replace("Cached:", "").Replace("kB", "").Trim());

            if (line.StartsWith("SwapTotal:"))
                result.Swap = 1024 * long.Parse(line.Replace("SwapTotal:", "").Replace("kB", "").Trim());

            if (line.StartsWith("SwapFree:"))
                result.SwapFree = 1024 * long.Parse(line.Replace("SwapFree:", "").Replace("kB", "").Trim());
        }

        return result;
    }

    public Task<DiskData> GetDiskDetails()
    {
        var di = DriveInfo.GetDrives().FirstOrDefault(x => x.RootDirectory.Name == "/");

        if (di == null)
        {
            return Task.FromResult(new DiskData()
            {
                Free = 0,
                Total = 0
            });
        }

        return Task.FromResult(new DiskData()
        {
            Free = di.TotalFreeSpace,
            Total = di.TotalSize
        });
    }
}