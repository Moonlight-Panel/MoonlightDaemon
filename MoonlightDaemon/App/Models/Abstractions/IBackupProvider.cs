namespace MoonlightDaemon.App.Models.Abstractions;

public interface IBackupProvider
{
    public Task<Backup> Create(Server server, int backupId);
    public Task Delete(Server server, int backupId);
    public Task<BackupDownload> GetDownload(Server server, int backupId);
    public Task Restore(Server server, int backupId);
}