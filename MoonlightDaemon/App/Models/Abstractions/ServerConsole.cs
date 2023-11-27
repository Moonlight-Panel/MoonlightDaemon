using MoonlightDaemon.App.Extensions;

namespace MoonlightDaemon.App.Models.Abstractions;

public class ServerConsole
{
    public EventHandler<string>? OnAnyOutput { get; set; }
    public EventHandler<string>? OnInput { get; set; }

    private readonly List<string> OutputLines = new();

    public async Task WriteInput(string command) => await OnInput.InvokeAsync(command);

    public Task<string[]> GetAllOutput()
    {
        lock (OutputLines)
            return Task.FromResult(OutputLines.ToArray());
    }

    public async Task WriteOutput(string content)
    {
        lock (OutputLines)
            OutputLines.Add(content);

        await OnAnyOutput.InvokeAsync(content);
    }

    public async Task WriteSystemOutput(string content)
    {
        content = $"[Moonlight] {content}";

        await WriteOutput(content);
    }
}