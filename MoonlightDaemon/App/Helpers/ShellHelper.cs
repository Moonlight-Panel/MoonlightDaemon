using System.Diagnostics;
using MoonCore.Attributes;
using MoonlightDaemon.App.Exceptions;

namespace MoonlightDaemon.App.Helpers;

[Singleton]
public class ShellHelper
{
    public async Task<string> ExecuteCommand(string command, bool ignoreErrors = false)
    {
        Process process = new Process();
        
        process.StartInfo.FileName = "/bin/sh";
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
                throw new ShellException(await process.StandardError.ReadToEndAsync());
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