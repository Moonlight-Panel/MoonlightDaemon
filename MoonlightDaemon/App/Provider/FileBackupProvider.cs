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

        foreach (var entry in serverFs.List("/"))
            serverFs.Remove(entry.Name);

        await ArchiveHelper.ExtractFromTarFile(backupPath, serverFs, ".");

    }
}