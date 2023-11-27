using System.Text;
using Docker.DotNet;
using MoonlightDaemon.App.Extensions;

namespace MoonlightDaemon.App.Models.Abstractions;

public class ContainerStream // Utility to read and write from container stream
{
    public EventHandler<string>? OnOutput { get; set; }
    public EventHandler<string>? OnSystemOutput { get; set; }
    
    public bool IsActive => !CancellationTokenSource?.IsCancellationRequested ?? false;

    private CancellationTokenSource? CancellationTokenSource;
    private MultiplexedStream? MultiplexedStream;

    public async Task Attach(MultiplexedStream multiplexedStream) // Attach to stream
    {
        if (MultiplexedStream != null) // Close previous if still attached
            await Close();

        CancellationTokenSource = new();
        MultiplexedStream = multiplexedStream;

        // Start read loop in own task
        Task.Run(RunReadLoop);
    }

    private async Task RunReadLoop() // Perform read operations
    {
        while (!CancellationTokenSource!.IsCancellationRequested)
        {
            // Declare buffer and read
            var buffer = new byte[1024];
            var result = await MultiplexedStream!.ReadOutputAsync(buffer, 0, buffer.Length, CancellationTokenSource.Token);
            
            // End of stream reached => stop reading
            if (result.EOF)
                return;
            
            // Finalize data and resize buffer
            var finalBuffer = new byte[result.Count];
            Array.Copy(buffer, finalBuffer, result.Count);

            // Parse text and split into lines
            var text = Encoding.UTF8.GetString(finalBuffer);
            var lines = text.Split("\n");

            // Emit on output events
            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                    await WriteOutput(line);
            }
        }
    }

    public async Task WriteInput(string content) // Write content to container stdin
    {
        // Prevent sending without being attached
        if (MultiplexedStream == null)
            throw new ArgumentException("Stream not attached");

        // Encode text to buffer
        var buffer = Encoding.UTF8.GetBytes(content + "\n");
        
        // Write buffer
        await MultiplexedStream.WriteAsync(buffer, 0, buffer.Length, CancellationTokenSource!.Token);
    }
    
    public async Task WriteOutput(string content)
    {
        await OnOutput.InvokeAsync(content);
    }

    public async Task WriteSystemOutput(string content)
    {
        await OnSystemOutput.InvokeAsync(content);
    }

    public Task AttachToEnvironment(EnvironmentStream stream)
    {
        OnOutput += async (_, content) => await stream.WriteOutput(content);
        OnSystemOutput += async (_, content) => await stream.WriteSystemOutput(content);
        stream.OnInput += async (_, content) => await WriteInput(content);
        
        return Task.CompletedTask;
    }

    public Task Close() // Close stream and reset data
    {
        if(MultiplexedStream == null) // Prevent closing of already closed stream
            return Task.CompletedTask;
        
        // Cleanup
        MultiplexedStream?.CloseWrite();
        CancellationTokenSource?.Cancel();
        MultiplexedStream = null;

        return Task.CompletedTask;
    }
}