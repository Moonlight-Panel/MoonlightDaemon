using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Parsers;

public class PropertiesParser : IParser
{
    public Task<string> Parse(string fileContent, Dictionary<string, string> configuration, Dictionary<string, string> environmentVariables)
    {
        var result = "";
        List<string> foundOptions = new();

        foreach (var line in fileContent.Split("\n")) // Split into lines
        {
            var lineModification = line; // Save current line in a new variable in order to modify it

            if (!string.IsNullOrEmpty(lineModification)) // Check if the line is empty
            {
                foreach (var option in configuration) // Iterate through every configuration option available
                {
                    if (lineModification.StartsWith(option.Key)) // If the current line starts with the key of the current option ...
                    {
                        var value = option.Value; // ... we want to save the value, ...
                        foundOptions.Add(option.Key); // ... save that we found the option, ...

                        // ...replace every variable in this value string with the variable value ...
                        foreach (var variable in environmentVariables)
                            value = value.Replace("{{" + variable.Key + "}}", variable.Value);

                        // ... and set the new value with the key as the lineModification, in order to get saved
                        lineModification = $"{option.Key}={value}";
                    }
                }
            }

            result += lineModification + "\n";
        }

        // Now we want to add all options which has not been found previous.
        // This is useful when the configuration file has been created by moonlight
        // and you want to specify options before the first start of an server.
        // e.g. server-port and minecraft servers
        foreach (var option in configuration // This finds every option
                     .Where(x => // which has not been saved
                         foundOptions.All(y => y != x.Key))) // in the foundOptions list
        {
            var value = option.Value;
            
            foreach (var variable in environmentVariables)
                value = value.Replace("{{" + variable.Key + "}}", variable.Value);

            result += $"{option.Key}={value}\n";
        }

        return Task.FromResult(result);
    }
}