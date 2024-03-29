using System.Text;
using Docker.DotNet;
using MoonCore.Helpers;

namespace MoonlightDaemon.App.Helpers;

public class ServerConsole
{
    public SmartEventHandler<string> OnNewLogMessage { get; set; } = new();

    private readonly List<string> Logs = new();
    private MultiplexedStream? Stream;
    private CancellationTokenSource Cancellation = new();

    public Task Attach(MultiplexedStream stream)
    {
        // Replace/set stream
        Stream = stream;
        Cancellation = new();

        Task.Run(async () =>
        {
            while (true)
            {
                // Initialize buffer and try reading
                var buffer = new byte[1024];
                var readResult = await stream.ReadOutputAsync(buffer, 0, buffer.Length, Cancellation.Token);

                if (readResult.EOF) // If we reached the end of file, we are done
                    return;

                // Copy data to a resized buffer and continue reading
                var finalSizedBuffer = new byte[readResult.Count];
                Array.Copy(buffer, finalSizedBuffer, readResult.Count);

                Task.Run(async () =>
                {
                    // Decode, parse and add all non empty lines
                    var text = Encoding.UTF8.GetString(finalSizedBuffer);
                    var lines = text.Split("\n");

                    foreach (var line in lines.Where(x => !string.IsNullOrEmpty(x)))
                        await WriteLine(line);
                });
            }
        });

        return Task.CompletedTask;
    }

    public async Task WriteLine(string content, bool suppressEvent = false)
    {
        lock (Logs)
        {
            // Clear oversized log cache
            if (Logs.Count > 1000)
            {
                try
                {
                    Logs.RemoveRange(0, 500);
                }
                catch (Exception e)
                {
                    Logger.Warn($"Unhandled error while cleaning oversized logs cache ({Logs.Count})");
                    Logger.Warn(e);
                }
            }

            Logs.Add(content);
        }

        if(!suppressEvent)
            await OnNewLogMessage.Invoke(content);
    }

    public async Task SendCommand(string command)
    {
        if (Stream == null) // This should never happen as it can lead to a broken state machine
        {
            Logger.Warn("Tried to write to a server console without a stream connected");
            return;
        }

        var buffer = Encoding.UTF8.GetBytes(command + "\n");
        await Stream.WriteAsync(buffer, 0, buffer.Length, Cancellation.Token);
    }

    public Task<string[]> GetAllLogMessages()
    {
        lock (Logs)
            return Task.FromResult(Logs.ToArray());
    }

    public void Close()
    {
        if(!Cancellation.IsCancellationRequested)
            Cancellation.Cancel();
    }
}