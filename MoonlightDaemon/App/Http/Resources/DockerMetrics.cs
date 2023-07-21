using MoonlightDaemon.App.Models;

namespace MoonlightDaemon.App.Http.Resources;

public class DockerMetrics
{
    public Container[] Containers { get; set; } = Array.Empty<Container>();
}