using System.Diagnostics;
using MoonlightDaemon.App.Exceptions;

namespace MoonlightDaemon.App.Helpers;

public class BashHelper
{
    public async Task<string> ExecuteCommand(string command)
    {
        Process process = new Process();
        
        process.StartInfo.FileName = "/bin/bash";
        process.StartInfo.Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new BashException(await process.StandardError.ReadToEndAsync());
        }

        return output;
    }
}