using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using MoonCore.Helpers;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Provider;

public class FileBackupProvider : IBackupProvider
{
    public async Task<Backup> Create(Server server, int backupId)
    {
        //var volumePath = server.Configuration.GetRuntimeVolumePath();
        var backupPath = $"/var/lib/moonlight/backups/{backupId}.tar.gz";

        var serverFs = server.FileSystem;
        var items = serverFs.List("/").Select(x => x.Name).ToArray();

        await ArchiveHelper.ArchiveToTarFile(backupPath, server.FileSystem, items);

        return new Backup()
        {
            Size = new FileInfo(backupPath).Length
        };
    }

    public Task Delete(Server server, int backupId)
    {
        var backupPath = $"/var/lib/moonlight/backups/{backupId}.tar.gz";
        
        if(File.Exists(backupPath))
            File.Delete(backupPath);
        
        return Task.CompletedTask;
    }

    public Task<BackupDownload> GetDownload(Server server, int backupId)
    {
        var backupPath = $"/var/lib/moonlight/backups/{backupId}.tar.gz";

        return Task.FromResult(new BackupDownload()
        {
            Stream = File.OpenRead(backupPath),
            FileName = Path.GetFileName(backupPath),
            ContentType = "application/tar+gzip"
        });
    }

    public async Task Restore(Server server, int backupId)
    {
        var backupPath = $"/var/lib/moonlight/backups/{backupId}.tar.gz";
        
        var serverFs = server.FileSystem;
        // TODO: Rm items here

        await ArchiveHelper.ExtractFromTarFile(backupPath, serverFs, ".");

    }
/*
    private async Task ArchiveServer(ChrootFileSystem fileSystem, string pathToTar)
    {
        await using var outStream = File.Create(pathToTar);
        await using var gzoStream = new GZipOutputStream(outStream);
        using var tarArchive = TarArchive.CreateOutputTarArchive(gzoStream, Encoding.UTF8);

        tarArchive.RootPath = "/";

        await AddDirectoryToTarNew(tarArchive, fileSystem, "/");
    }
    
    private async Task AddDirectoryToTarNew(TarArchive archive, ChrootFileSystem fileSystem, string root)
    {
        foreach (var file in fileSystem.ListFiles(root))
        {
            var fi = fileSystem.Stat(root + file.Name);

            if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint) && string.IsNullOrEmpty(fi.LinkTarget))
                continue; // => ignore

            if (!string.IsNullOrEmpty(fi.LinkTarget))
            {
                if (fi.LinkTarget.Contains(".."))
                    continue; // => ignore

                if (fi.LinkTarget.StartsWith("/") && !fi.LinkTarget.StartsWith("/home/container"))
                    continue; // => ignore

                var linkTarget = fi.ResolveLinkTarget(true);

                if (linkTarget == null)
                    continue; // => ignore

                if (!linkTarget.FullName.StartsWith(fi.FullName))
                    continue; // ignore
            }
                
            var entry = TarEntry.CreateEntryFromFile(fi.FullName);
            entry.Name = root + file.Name;
            archive.WriteEntry(entry, false);
        }

        foreach (var directory in fileSystem.ListDirectories(root))
        {
            await AddDirectoryToTarNew(archive, fileSystem, root + directory.Name + "/");
        }
    }

    private async Task UnarchiveTar(string pathToTar, string dst)
    {
        await using var outStream = File.OpenRead(pathToTar);
        await using var gzoStream = new GZipInputStream(outStream);
        using var tarArchive = TarArchive.CreateInputTarArchive(gzoStream, Encoding.UTF8);
        
        tarArchive.ExtractContents(dst, false);
    }*/
}