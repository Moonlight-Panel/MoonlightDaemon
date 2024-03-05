using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Provider;

public class FileBackupProvider : IBackupProvider
{
    public async Task<Backup> Create(Server server, int backupId)
    {
        var volumePath = server.Configuration.GetRuntimeVolumePath();
        var backupPath = $"/var/lib/moonlight/backups/{backupId}.tar.gz";

        await ArchiveDirectory(volumePath, backupPath);

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
        var volumePath = server.Configuration.GetRuntimeVolumePath();
        var backupPath = $"/var/lib/moonlight/backups/{backupId}.tar.gz";

        if (!File.Exists(backupPath))
            return;

        foreach (var directory in Directory.GetDirectories(volumePath))
            Directory.Delete(directory, true);

        foreach (var file in Directory.GetFiles(volumePath))
            File.Delete(file);

        await UnarchiveTar(backupPath, volumePath);
    }

    private async Task ArchiveDirectory(string src, string pathToTar)
    {
        await using var outStream = File.Create(pathToTar);
        await using var gzoStream = new GZipOutputStream(outStream);
        using var tarArchive = TarArchive.CreateOutputTarArchive(gzoStream, Encoding.UTF8);

        tarArchive.RootPath = src;

        if (tarArchive.RootPath.EndsWith("/"))
            tarArchive.RootPath = tarArchive.RootPath.Remove(tarArchive.RootPath.Length - 1);

        await AddDirectoryToTar(tarArchive, src);
    }

    private async Task AddDirectoryToTar(TarArchive archive, string src)
    {
        foreach (var file in Directory.GetFiles(src))
        {
            var entry = TarEntry.CreateEntryFromFile(file);
            archive.WriteEntry(entry, false);
        }

        foreach (var directory in Directory.GetDirectories(src))
        {
            await AddDirectoryToTar(archive, directory);
        }
    }

    private async Task UnarchiveTar(string pathToTar, string dst)
    {
        await using var outStream = File.OpenRead(pathToTar);
        await using var gzoStream = new GZipInputStream(outStream);
        using var tarArchive = TarArchive.CreateInputTarArchive(gzoStream, Encoding.UTF8);
        
        tarArchive.ExtractContents(dst, false);
    }
}