using MoonlightDaemon.App.Configuration;
using Newtonsoft.Json;

namespace MoonlightDaemon.App.Services;

public class ConfigService
{
    private readonly string Path = "/etc/moonlight/config.json";
    private ConfigV1 Config;
    private TempConfig TempConfig = new();

    public ConfigService()
    {
        Reload();
    }

    public void Reload()
    {
        Directory.CreateDirectory("/etc/moonlight");
        
        if(!File.Exists(Path))
            File.WriteAllText(Path, "{}");

        var text = File.ReadAllText(Path);
        Config = JsonConvert.DeserializeObject<ConfigV1>(text) ?? new();
        Save();
    }

    public void Save()
    {
        var text = JsonConvert.SerializeObject(Config, Formatting.Indented);
        File.WriteAllText(Path, text);
    }

    public ConfigV1 Get() => Config;
    public TempConfig GetTemp() => TempConfig;
}