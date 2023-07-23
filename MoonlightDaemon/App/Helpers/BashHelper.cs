using System.Diagnostics;
using MoonlightDaemon.App.Exceptions;

namespace MoonlightDaemon.App.Helpers;

public class BashHelper
{
    public async Task<string> ExecuteCommand(string command, bool ignoreErrors = false)
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
            if(!ignoreErrors)
                throw new BashException(await process.StandardError.ReadToEndAsync());
        }

        return output;
    }
    
    public Task<Process> ExecuteCommandRaw(string command)
    {
        Process process = new Process();
        
        process.StartInfo.FileName = "/bin/bash";
        process.StartInfo.Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        process.Start();

        return Task.FromResult(process);
    }
}