using MoonlightDaemon.App.Helpers;
using Serilog;

namespace MoonlightDaemon.App.Services;

public class FirewallService
{
    private readonly BashHelper BashHelper;

    public FirewallService(BashHelper bashHelper)
    {
        BashHelper = bashHelper;
    }

    public async Task Rebuild(string[] ips)
    {
        Log.Information("Rebuilding blocklist");
        
        var chainName = "mlBlocklist";
        
        // Reset blocklist
        
        await BashHelper.ExecuteCommand($"iptables -D INPUT -j {chainName}", true);
        await BashHelper.ExecuteCommand($"iptables -F {chainName}", true);
        await BashHelper.ExecuteCommand($"iptables -X {chainName}", true);
        
        // Create chain
        await BashHelper.ExecuteCommand($"iptables -N {chainName}");

        foreach (var ip in ips)
        {
            await BashHelper.ExecuteCommand($"iptables -A {chainName} -s {ip} -j DROP");
        }
        
        // Attach chain into the INPUT chain
        await BashHelper.ExecuteCommand($"iptables -I INPUT 1 -j {chainName}");
        
        Log.Information("Rebuilded blocklist!");
    }
}