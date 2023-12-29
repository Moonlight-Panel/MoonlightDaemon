namespace MoonlightDaemon.App.Models.Abstractions;

public interface IParser
{
    public Task<string> Parse(string fileContent, string key, string value);
}