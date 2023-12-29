using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Parsers;

public class FileParser : IParser
{
    public Task<string> Parse(string fileContent, string key, string value)
    {
        var processedFile = "";

        var hasBeenFound = false;
        
        foreach (var line in fileContent.Split("\n"))
        {
            if(string.IsNullOrEmpty(line))
                continue;

            var lineMod = line;

            if (lineMod.StartsWith(key))
            {
                hasBeenFound = true;
                lineMod = $"{key}={value}";
            }

            processedFile += $"{lineMod}\n";
        }

        if (!hasBeenFound)
        {
            processedFile += $"{key}={value}\n";
        }
        
        return Task.FromResult(processedFile);
    }
}