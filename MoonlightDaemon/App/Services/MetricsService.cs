using MoonlightDaemon.App.Helpers;

namespace MoonlightDaemon.App.Services;

public class MetricsService
{
    private readonly BashHelper BashHelper;

    public MetricsService(BashHelper bashHelper)
    {
        BashHelper = bashHelper;
    }

    public async Task<string> GetCpuModel()
    {
        return await BashHelper
            .ExecuteCommand("lscpu | grep 'Model name' | awk -F: '{print $2}' | sed 's/^ *//'");
    }

    public async Task<double> GetCpuUsage()
    {
        return double.Parse(
            await BashHelper
                .ExecuteCommand("top -bn1 | grep \"Cpu(s)\" | awk '{print $2 + $4}'")
        );
    }

    public async Task<long> GetUsedMemory()
    {
        return await GetTotalMemory() - long.Parse(
            await BashHelper
                .ExecuteCommand("grep 'MemFree:' /proc/meminfo | awk '{print $2}'")
        );
    }

    public async Task<long> GetTotalMemory()
    {
        return long.Parse(
            await BashHelper
                .ExecuteCommand("grep 'MemTotal:' /proc/meminfo | awk '{print $2}'")
        );
    }

    public async Task<long> GetTotalDisk()
    {
        return long.Parse(
            await BashHelper
                .ExecuteCommand("df -B 1 --total | tail -1 | awk '{print $2}'")
        );
    }

    public async Task<long> GetUsedDisk()
    {
        return long.Parse(
            await BashHelper
                .ExecuteCommand("df -B 1 --total | tail -1 | awk '{print $3}'")
        );
    }

    public async Task<long> GetUptime()
    {
        return long.Parse(
            await BashHelper
                .ExecuteCommand("cut -d. -f1 /proc/uptime")
        ) * 1000;
    }

    public async Task<string> GetOsName()
    {
        return await BashHelper
            .ExecuteCommand("lsb_release -s -d");
    }
}
