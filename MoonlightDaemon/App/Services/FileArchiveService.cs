using MoonCore.Attributes;
using MoonCore.Helpers;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Services;

[Singleton]
public class FileArchiveService
{
    private readonly Dictionary<string, IFileArchiver> Archivers = new();

    public Task Register<T>(string id) where T : IFileArchiver
    {
        var archiver = Activator.CreateInstance<T>() as IFileArchiver;
        
        Archivers.Add(id, archiver);
        
        return Task.CompletedTask;
    }

    public async Task Archive(ChrootFileSystem fileSystem, string archiverId, string destination, string[] files)
    {
        var archiver = GetById(archiverId);

        await archiver.Archive(fileSystem, destination, files);
    }

    public async Task UnArchive(ChrootFileSystem fileSystem, string archiverId, string source, string destination)
    {
        var archiver = GetById(archiverId);

        await archiver.UnArchive(fileSystem, source, destination);
    }

    private IFileArchiver GetById(string id)
    {
        if (!Archivers.ContainsKey(id))
            throw new ArgumentException($"No archiver with the '{id}' id is registered");

        return Archivers[id];
    }
}