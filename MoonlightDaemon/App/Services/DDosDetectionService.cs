using System.Diagnostics;
using MoonlightDaemon.App.ApiClients.Moonlight;
using MoonlightDaemon.App.ApiClients.Moonlight.Requests;
using MoonlightDaemon.App.Helpers;
using Serilog;

namespace MoonlightDaemon.App.Services;

public class DDosDetectionService
{
    private readonly BashHelper BashHelper;
    private readonly MoonlightApiHelper ApiHelper;
    
    private Process? ScriptProcess;

    public bool IsRunning => !ScriptProcess?.HasExited ?? false; // If null -> false

    public DDosDetectionService(BashHelper bashHelper, MoonlightApiHelper apiHelper)
    {
        BashHelper = bashHelper;
        ApiHelper = apiHelper;

        Task.Run(RunDetection);
    }

    private async Task RunDetection()
    {
        try
        {
            Log.Information("Starting ddos detection");

            // We use .bash here because in WSL .sh will act as a directory or smth idk :(
            if (!File.Exists("ddosDetection.bash"))
            {
                Log.Information("Downloading script");
                using var httpClient = new HttpClient();
                
                var script = await httpClient.GetStringAsync("https://gist.githubusercontent.com/Marcel-Baumgartner/0310679f6f6e03a4bad26d784231fa13/raw/ddosDetection.sh");
                await File.WriteAllTextAsync("ddosDetection.bash", script);
            }

            Log.Information("Executing script...");
            
            ScriptProcess = await BashHelper.ExecuteCommandRaw("bash ddosDetection.bash");

            while (!ScriptProcess.StandardOutput.EndOfStream)
            {
                var line = await ScriptProcess.StandardOutput.ReadLineAsync();
                
                if(string.IsNullOrEmpty(line))
                    continue;
                
                if(!line.StartsWith("DATA"))
                    continue;

                var parts = line.Trim().Split(":");

                if (parts[1] == "START")
                {
                    var ip = parts[2];
                    var packets = parts[3];

                    await ApiHelper.Post("api/remote/ddos/start", new DdosStart()
                    {
                        Ip = ip,
                        Packets = long.Parse(packets)
                    });
                }
                else if (parts[1] == "END")
                {
                    var ip = parts[2];
                    var traffic = parts[3];

                    await ApiHelper.Post("api/remote/ddos/stop", new DdosStop()
                    {
                        Ip = ip,
                        Traffic = long.Parse(traffic)
                    });
                }
            }

            await ScriptProcess.WaitForExitAsync();
            
            Log.Information("DDos detection script stopped. Restart the daemon to start again");
        }
        catch (Exception e)
        {
            Log.Fatal("Error running ddos detection");
            Log.Fatal(e.ToStringDemystified());
        }
    }
}