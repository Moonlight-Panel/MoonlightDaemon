using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Parsers;

public class FileParser : IParser
{
    public Task<string> Parse(string fileContent, Dictionary<string, string> configuration, Dictionary<string, string> environmentVariables)
    {
        var result = "";

        foreach (var line in fileContent.Split("\n")) // Split into lines
        {
            var lineModification = line; // Save current line in a new variable in order to modify it

            if (!string.IsNullOrEmpty(lineModification)) // Check if the line is empty
            {
                foreach (var option in configuration) // Iterate through every configuration option available
                {
                    if (lineModification.StartsWith(option.Key)) // If the current line starts with the key of the current option ...
                    {
                        var modification = option.Value; // ... we want to save the value ...

                        // ...replace every variable in this value string with the variable value ...
                        foreach (var variable in environmentVariables)
                            modification = modification.Replace("{{" + variable.Key + "}}", variable.Value);

                        // ... and save the modification as the lineModification, in order to get saved
                        lineModification = modification;
                    }
                }
            }

            result += lineModification + "\n";
        }

        return Task.FromResult(result);
    }
}