using FubarDev.FtpServer.BackgroundTransfer;
using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.FileSystem.DotNet;
using Mono.Unix.Native;

namespace MoonlightDaemon.App.Helpers.Ftp;

public class FtpFileSystem : IUnixFileSystem
{
    private static readonly int DefaultStreamBufferSize = 4096;
    private readonly int StreamBufferSize;

    public FtpFileSystem(string rootPath, bool allowNonEmptyDirectoryDelete)
    {
        FileSystemEntryComparer = StringComparer.OrdinalIgnoreCase;
        Root = new DotNetDirectoryEntry(Directory.CreateDirectory(rootPath), true, allowNonEmptyDirectoryDelete);
        SupportsNonEmptyDirectoryDelete = allowNonEmptyDirectoryDelete;
        StreamBufferSize = DefaultStreamBufferSize;
    }

    public bool SupportsNonEmptyDirectoryDelete { get; }
    public StringComparer FileSystemEntryComparer { get; }
    public IUnixDirectoryEntry Root { get; }
    public bool SupportsAppend => true;

    public Task<IReadOnlyList<IUnixFileSystemEntry>> GetEntriesAsync(IUnixDirectoryEntry directoryEntry,
        CancellationToken cancellationToken)
    {
        var result = new List<IUnixFileSystemEntry>();
        var searchDirInfo = ((DotNetDirectoryEntry)directoryEntry).DirectoryInfo;
        foreach (var info in searchDirInfo.EnumerateFileSystemInfos())
        {
            if (info is DirectoryInfo dirInfo)
            {
                result.Add(new DotNetDirectoryEntry(dirInfo, false, SupportsNonEmptyDirectoryDelete));
            }
            else
            {
                if (info is FileInfo fileInfo)
                {
                    result.Add(new DotNetFileEntry(fileInfo));
                }
            }
        }

        return Task.FromResult<IReadOnlyList<IUnixFileSystemEntry>>(result);
    }

    public Task<IUnixFileSystemEntry?> GetEntryByNameAsync(IUnixDirectoryEntry directoryEntry, string name,
        CancellationToken cancellationToken)
    {
        var searchDirInfo = ((DotNetDirectoryEntry)directoryEntry).Info;
        var fullPath = Path.Combine(searchDirInfo.FullName, name);
        IUnixFileSystemEntry? result;
        if (File.Exists(fullPath))
        {
            result = new DotNetFileEntry(new FileInfo(fullPath));
        }
        else if (Directory.Exists(fullPath))
        {
            result = new DotNetDirectoryEntry(new DirectoryInfo(fullPath), false, SupportsNonEmptyDirectoryDelete);
        }
        else
        {
            result = null;
        }

        return Task.FromResult(result);
    }

    public Task<IUnixFileSystemEntry> MoveAsync(IUnixDirectoryEntry parent, IUnixFileSystemEntry source,
        IUnixDirectoryEntry target, string fileName, CancellationToken cancellationToken)
    {
        var targetEntry = (DotNetDirectoryEntry)target;
        var targetName = Path.Combine(targetEntry.Info.FullName, fileName);

        if (source is DotNetFileEntry sourceFileEntry)
        {
            sourceFileEntry.FileInfo.MoveTo(targetName);
            return Task.FromResult<IUnixFileSystemEntry>(new DotNetFileEntry(new FileInfo(targetName)));
        }

        var sourceDirEntry = (DotNetDirectoryEntry)source;
        sourceDirEntry.DirectoryInfo.MoveTo(targetName);
        return Task.FromResult<IUnixFileSystemEntry>(new DotNetDirectoryEntry(new DirectoryInfo(targetName), false,
            SupportsNonEmptyDirectoryDelete));
    }

    public Task UnlinkAsync(IUnixFileSystemEntry entry, CancellationToken cancellationToken)
    {
        if (entry is DotNetDirectoryEntry dirEntry)
        {
            dirEntry.DirectoryInfo.Delete(SupportsNonEmptyDirectoryDelete);
        }
        else
        {
            var fileEntry = (DotNetFileEntry)entry;
            fileEntry.Info.Delete();
        }

        return Task.FromResult(0);
    }

    public async Task<IUnixDirectoryEntry> CreateDirectoryAsync(IUnixDirectoryEntry targetDirectory,
        string directoryName,
        CancellationToken cancellationToken)
    {
        var targetEntry = (DotNetDirectoryEntry)targetDirectory;
        var newDirInfo = targetEntry.DirectoryInfo.CreateSubdirectory(directoryName);

        await Chown(newDirInfo.FullName);

        return new DotNetDirectoryEntry(newDirInfo, false,
            SupportsNonEmptyDirectoryDelete);
    }

    public Task<Stream> OpenReadAsync(IUnixFileEntry fileEntry, long startPosition, CancellationToken cancellationToken)
    {
        var fileInfo = ((DotNetFileEntry)fileEntry).FileInfo;
        var input = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (startPosition != 0)
        {
            input.Seek(startPosition, SeekOrigin.Begin);
        }

        return Task.FromResult<Stream>(input);
    }

    public async Task<IBackgroundTransfer?> AppendAsync(IUnixFileEntry fileEntry, long? startPosition, Stream data,
        CancellationToken cancellationToken)
    {
        var fileInfo = ((DotNetFileEntry)fileEntry).FileInfo;
        await using var output = fileInfo.OpenWrite();
        startPosition ??= fileInfo.Length;

        output.Seek(startPosition.Value, SeekOrigin.Begin);
        await data.CopyToAsync(output, StreamBufferSize, cancellationToken).ConfigureAwait(false);

        return null;
    }

    public async Task<IBackgroundTransfer?> CreateAsync(IUnixDirectoryEntry targetDirectory, string fileName,
        Stream data, CancellationToken cancellationToken)
    {
        var targetEntry = (DotNetDirectoryEntry)targetDirectory;
        var fileInfo = new FileInfo(Path.Combine(targetEntry.Info.FullName, fileName));
        await using var output = fileInfo.Create();
        await data.CopyToAsync(output, StreamBufferSize, cancellationToken).ConfigureAwait(false);

        await Chown(Path.Combine(targetEntry.Info.FullName, fileName));

        return null;
    }

    public async Task<IBackgroundTransfer?> ReplaceAsync(IUnixFileEntry fileEntry, Stream data,
        CancellationToken cancellationToken)
    {
        var fileInfo = ((DotNetFileEntry)fileEntry).FileInfo;
        await using var output = fileInfo.OpenWrite();
        await data.CopyToAsync(output, StreamBufferSize, cancellationToken).ConfigureAwait(false);
        output.SetLength(output.Position);

        return null;
    }

    public Task<IUnixFileSystemEntry> SetMacTimeAsync(IUnixFileSystemEntry entry, DateTimeOffset? modify,
        DateTimeOffset? access, DateTimeOffset? create, CancellationToken cancellationToken)
    {
        var item = ((DotNetFileSystemEntry)entry).Info;

        if (access != null)
        {
            item.LastAccessTimeUtc = access.Value.UtcDateTime;
        }

        if (modify != null)
        {
            item.LastWriteTimeUtc = modify.Value.UtcDateTime;
        }

        if (create != null)
        {
            item.CreationTimeUtc = create.Value.UtcDateTime;
        }

        if (entry is DotNetDirectoryEntry dirEntry)
        {
            return Task.FromResult<IUnixFileSystemEntry>(new DotNetDirectoryEntry((DirectoryInfo)item, dirEntry.IsRoot,
                SupportsNonEmptyDirectoryDelete));
        }

        return Task.FromResult<IUnixFileSystemEntry>(new DotNetFileEntry((FileInfo)item));
    }

    private Task Chown(string path)
    {
        Syscall.chown(path, 998, 998);

        return Task.CompletedTask;
    }
}