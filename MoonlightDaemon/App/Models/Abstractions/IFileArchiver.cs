using MoonlightDaemon.App.Helpers;

namespace MoonlightDaemon.App.Models.Abstractions;

public interface IFileArchiver
{
    public Task Archive(ServerFileSystem fileSystem, string destination, string[] files);
    public Task UnArchive(ServerFileSystem fileSystem, string source, string destination);
}