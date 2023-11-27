using MoonlightDaemon.App.Extensions;

namespace MoonlightDaemon.App.Models.Abstractions;

public class EnvironmentStream
{
    public EventHandler<string> OnOutput { get; set; }
    public EventHandler<string> OnSystemOutput { get; set; }
    public EventHandler<string> OnInput { get; set; }
    
    public async Task WriteOutput(string content)
    {
        await OnOutput.InvokeAsync(content);
    }

    public async Task WriteSystemOutput(string content)
    {
        await OnSystemOutput.InvokeAsync(content);
    }

    public async Task WriteInput(string content)
    {
        await OnInput.InvokeAsync(content);
    }

    public Task AttachToConsole(ServerConsole console)
    {
        OnOutput += async (_, content) => await console.WriteOutput(content);
        OnSystemOutput += async (_, content) => await console.WriteSystemOutput(content);
        console.OnInput += async (_, content) => await WriteInput(content);
        
        return Task.CompletedTask;
    }
}