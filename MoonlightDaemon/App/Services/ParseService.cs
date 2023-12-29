using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Extensions.ServerExtensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Abstractions;
using MoonlightDaemon.App.Models.Configuration;
using Newtonsoft.Json;

namespace MoonlightDaemon.App.Services;

public class ParseService
{
    public readonly Dictionary<string, IParser> Parsers = new();

    public async Task Process(Server server)
    {
        await server.Log("Parsing configuration files");
        
        ServerParseConfig[] configs;

        try
        {
            configs = JsonConvert.DeserializeObject<ServerParseConfig[]>(server.Configuration.ParseConfigurations) ??
                      Array.Empty<ServerParseConfig>();
        }
        catch (JsonSerializationException e)
        {
            Logger.Warn("An error occured while parsing the server parse configurations");
            Logger.Warn(e);

            await server.Log("Unable to process configuration parsing");
            return;
        }

        // We group the configs by the file they are trying to access to load and save the file once and
        // not saving and loading for every config. This will reduce the io impact
        var groupedConfigs = configs.GroupBy(x => x.File);

        foreach (var groupedConfig in groupedConfigs)
            await ProcessConfigsForFile(server, groupedConfig.Key, groupedConfig.ToArray());
    }

    private async Task ProcessConfigsForFile(Server server, string file, ServerParseConfig[] configs)
    {
        // Sanitize file path to prevent access to system files
        var cleanedPath = file;
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
                Logger.Warn($"Unable to create missing config file: {file}");
                Logger.Warn(e);

                await server.Log("An error occured while processing configuration parsing");
                return;
            }
        }

        var content = await File.ReadAllTextAsync(path);

        foreach (var config in configs)
            content = await ProcessConfig(server, content, config);

        await File.WriteAllTextAsync(path, content);
    }

    private async Task<string> ProcessConfig(Server server, string content, ServerParseConfig config)
    {
        // We use this way of getting variables to ensure we have the dynamic environment variables like SERVER_PORT included
        var environmentVariables = server.Configuration.GetEnvironmentVariables();
        
        if (!environmentVariables.ContainsKey(config.Variable))
        {
            Logger.Warn(
                $"Unable to process configuration parsing for variable {config.Variable}. Skipping config");
            await server.Log(
                $"An error occured while parsing configuration files: {config.Variable} variable is missing");

            return content;
        }

        IParser? parser;

        // Search for parser 
        lock (Parsers)
            parser = Parsers.ContainsKey(config.Type) ? Parsers[config.Type] : null;

        // Parse not found => next config
        if (parser == null)
            return content;

        try
        {
            return await parser.Parse(content, config.Key, environmentVariables[config.Variable]);
        }
        catch (Exception e)
        {
            Logger.Warn(
                $"An error occured while processing parser {parser.GetType().Name} for file {config.Key}");
            Logger.Warn(e);

            return content;
        }
    }

    public Task Register<T>(string name) where T : IParser
    {
        var instance = Activator.CreateInstance<T>() as IParser;

        lock (Parsers)
            Parsers.Add(name, instance);

        return Task.CompletedTask;
    }
}