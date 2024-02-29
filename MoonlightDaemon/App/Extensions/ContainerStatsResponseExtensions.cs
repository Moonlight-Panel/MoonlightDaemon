using Docker.DotNet.Models;
using MoonCore.Helpers;
using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Extensions;

public static class ContainerStatsResponseExtensions
{
    public static ServerStats ToServerStats(this ContainerStatsResponse response)
    {
        var result = new ServerStats();

        // CPU
        var cpuDelta = (float)response.CPUStats.CPUUsage.TotalUsage - response.PreCPUStats.CPUUsage.TotalUsage;
        var cpuSystemDelta = (float)response.CPUStats.SystemUsage - response.PreCPUStats.SystemUsage;

        var cpuCoreCount = (int)response.CPUStats.OnlineCPUs;

        if (cpuCoreCount == 0)
            cpuCoreCount = response.CPUStats.CPUUsage.PercpuUsage.Count;

        var cpuPercent = 0f;
        
        if (cpuSystemDelta > 0.0f && cpuDelta > 0.0f)
        {
            cpuPercent = (cpuDelta / cpuSystemDelta) * 100;

            if (cpuCoreCount > 0)
                cpuPercent *= cpuCoreCount;
        }

        result.CpuUsage = Math.Round(cpuPercent * 1000) / 1000;
        
        // Memory
        result.MemoryTotal = ByteSizeValue.FromBytes((long)response.MemoryStats.Limit).MegaBytes;
        result.MemoryUsage = ByteSizeValue.FromBytes((long)response.MemoryStats.Usage).MegaBytes;
        
        // Io
        result.IoRead = (long)response.StorageStats.ReadCountNormalized;
        result.IoWrite = (long)response.StorageStats.WriteCountNormalized;
        
        // Net
        foreach (var network in response.Networks)
        {
            result.NetRead += ByteSizeValue.FromBytes((long)network.Value.RxBytes).KiloBytes;
            result.NetWrite += ByteSizeValue.FromBytes((long)network.Value.TxBytes).KiloBytes;
        }
        
        return result;
    }
}