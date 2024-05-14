using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Archivers;

public class TarGzArchiver : IFileArchiver
{
    public async Task Archive(ServerFileSystem fileSystem, string destination, string[] files)
    {
        await ArchiveHelper.ArchiveToServerFs(destination, fileSystem, files);
    }

    public async Task UnArchive(ServerFileSystem fileSystem, string source, string destination)
    {
        await ArchiveHelper.ExtractFromServerFs(source, fileSystem, destination);
    }
}