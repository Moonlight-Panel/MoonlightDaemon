using MoonCore.Attributes;
using MoonCore.Helpers;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Abstractions;
using MoonlightDaemon.App.Models.Configuration;
using Newtonsoft.Json;

namespace MoonlightDaemon.App.Services;

[Singleton]
public class ParseService
{
    public readonly Dictionary<string, IParser> Parsers = new();

    public async Task Process(Server server)
    {
        await server.Log("Parsing configuration files");
        
        ServerParseConfig[] configs;

        try
        {
            configs = JsonConvert.DeserializeObject<ServerParseConfig[]>(server.Configuration.Image.ParseConfigurations) ??
                      Array.Empty<ServerParseConfig>();
        }
        catch (JsonSerializationException e)
        {
            Logger.Warn("An error occured while parsing the server parse configurations");
            Logger.Warn(e);

            await server.Log("Unable to process configuration parsing");
            return;
        }

        foreach (var config in configs)
            await ProcessConfig(server, config);
    }

    private async Task ProcessConfig(Server server, ServerParseConfig config)
    {
        IParser? parser;
        
        // Search for parser 
        lock (Parsers)
            parser = Parsers.ContainsKey(config.Type) ? Parsers[config.Type] : null;

        // Parse not found, stop doing anything and continue with next config
        if (parser == null)
            return;
        
        // Sanitize file path to prevent access to system files
        var cleanedPath = config.File;
        cleanedPath = cleanedPath.TrimStart('/');
        cleanedPath = cleanedPath.Replace("..", "");

        // Build actual path
        var path = PathBuilder.File(server.Configuration.GetRuntimeVolumePath(), cleanedPath);

        // Ensure the file exists
        if (!File.Exists(path))
        {
            try
            {
                await File.WriteAllTextAsync(path, "");
            }
            catch (Exception e)
            {
                Logger.Warn($"Unable to create missing config file: {config.File}");
                Logger.Warn(e);

                await server.Log("An error occured while processing configuration parsing");
                return;
            }
        }

        var content = await File.ReadAllTextAsync(path);

        try
        {
            content = await parser.Parse(content, config.Configuration, server.Configuration.GetEnvironmentVariables());
        }
        catch (Exception e)
        {
            Logger.Warn($"An error occured while running '{config.Type} parser for file '{config.File}' for server {server.Configuration.Id}");
            Logger.Warn(e);

            await server.Log("An error occured while parsing configuration");
        }

        await File.WriteAllTextAsync(path, content);
    }
    
    public Task Register<T>(string name) where T : IParser
    {
        var instance = Activator.CreateInstance<T>() as IParser;

        lock (Parsers)
            Parsers.Add(name, instance);

        return Task.CompletedTask;
    }
}