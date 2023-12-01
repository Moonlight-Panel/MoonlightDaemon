using Docker.DotNet;
using Docker.DotNet.Models;

namespace MoonlightDaemon.App.Extensions;

public static class ContainerOperationsExtensions
{
    public static async Task<ContainerInspectResponse?> InspectContainerSafeAsync(this IContainerOperations operations, string id)
    {
        try
        {
            return await operations.InspectContainerAsync(id);
        }
        catch (DockerContainerNotFoundException)
        {
            return null;
        }
    }
}