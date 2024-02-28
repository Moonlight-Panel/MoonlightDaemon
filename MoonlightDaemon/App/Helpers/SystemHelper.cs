using MoonCore.Attributes;
using MoonCore.Helpers;

namespace MoonlightDaemon.App.Helpers;

[Singleton]
public class SystemHelper
{
    public Task<string> GetOsName()
    {
        try
        {
            var releaseRaw = File
                .ReadAllLines("/etc/os-release")
                .FirstOrDefault(x => x.StartsWith("PRETTY_NAME="));

            if (string.IsNullOrEmpty(releaseRaw))
                return Task.FromResult("Linux (unknown release)");

            var release = releaseRaw
                .Replace("PRETTY_NAME=", "")
                .Replace("\"", "");
                
            if(string.IsNullOrEmpty(release))
                return Task.FromResult("Linux (unknown release)");

            return Task.FromResult(release);
        }
        catch (Exception e)
        {
            Logger.Warn("Error retrieving os information");
            Logger.Warn(e);

            return Task.FromResult("N/A");
        }
    }
}