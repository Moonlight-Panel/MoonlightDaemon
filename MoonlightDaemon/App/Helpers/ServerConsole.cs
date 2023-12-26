using System.Text;
using Docker.DotNet;
using MoonlightDaemon.App.Extensions;

namespace MoonlightDaemon.App.Helpers;

public class ServerConsole
{
    public SmartEventHandler<string> OnNewLogMessage { get; set; } = new();

    private readonly List<string> Logs = new();
    private MultiplexedStream? Stream;

    public Task Attach(MultiplexedStream stream)
    {
        Stream = stream;

        Task.Run(async () =>
        {
            while (true)
            {
                var buffer = new byte[1024];
                var readResult = await stream.ReadOutputAsync(buffer, 0, buffer.Length, CancellationToken.None);

                if (readResult.EOF)
                    return;

                var finalSizedBuffer = new byte[readResult.Count];
                Array.Copy(buffer, finalSizedBuffer, readResult.Count);

                Task.Run(async () =>
                {
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
                    var logsToClear = Logs.TakeLast(500).ToArray();

                    foreach (var line in logsToClear)
                        Logs.Remove(line);
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
        await Stream.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None);
    }

    public Task<string[]> GetAllLogMessages()
    {
        lock (Logs)
            return Task.FromResult(Logs.ToArray());
    }
}