using MoonCore.Attributes;
using MoonCore.Exceptions;
using MoonCore.Services;
using MoonlightDaemon.App.Configuration;
using MoonlightDaemon.App.Models;
using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Services;

[Singleton]
public class BackupService
{
    public readonly Dictionary<string, IBackupProvider> Providers = new();
    private readonly ConfigService<ConfigV1> ConfigService;

    public BackupService(ConfigService<ConfigV1> configService)
    {
        ConfigService = configService;
    }

    public async Task<Backup> Create(Server server, int backupId)
    {
        var provider = GetProvider();
        return await provider.Create(server, backupId);
    }
    
    public async Task Delete(Server server, int backupId)
    {
        var provider = GetProvider();
        await provider.Delete(server, backupId);
    }
    
    public async Task<BackupDownload> GetDownload(Server server, int backupId)
    {
        var provider = GetProvider();
        return await provider.GetDownload(server, backupId);
    }
    
    public async Task Restore(Server server, int backupId)
    {
        var provider = GetProvider();
        await provider.Restore(server, backupId);
    }
    
    public Task Register<T>(string name) where T : IBackupProvider
    {
        var instance = Activator.CreateInstance<T>() as IBackupProvider;

        lock (Providers)
            Providers.Add(name, instance);

        return Task.CompletedTask;
    }

    private IBackupProvider GetProvider()
    {
        IBackupProvider? provider = default;
        var providerName = ConfigService.Get().Server.BackupProvider;

        lock (Providers)
        {
            if (Providers.Any(x => x.Key == providerName))
                provider = Providers[providerName];
        }

        if (provider == null)
            throw new DisplayException($"No backup provider with the name '{providerName}' registered");

        return provider;
    }
}