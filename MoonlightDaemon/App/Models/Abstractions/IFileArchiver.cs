using MoonCore.Helpers;

namespace MoonlightDaemon.App.Models.Abstractions;

public interface IFileArchiver
{
    public Task Archive(ChrootFileSystem fileSystem, string destination, string[] files);
    public Task UnArchive(ChrootFileSystem fileSystem, string source, string destination);
}