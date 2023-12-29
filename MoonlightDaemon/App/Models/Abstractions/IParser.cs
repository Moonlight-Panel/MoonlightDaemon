namespace MoonlightDaemon.App.Models.Abstractions;

public interface IParser
{
    public Task<string> Parse(string fileContent, Dictionary<string, string> configuration, Dictionary<string, string> environmentVariables);
}