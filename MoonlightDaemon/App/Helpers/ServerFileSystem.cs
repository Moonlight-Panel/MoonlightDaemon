using Mono.Unix.Native;
using MoonCore.Helpers;
using MoonCore.Helpers.Unix;
using MoonCore.Helpers.Unix.Extensions;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Models.Configuration;

namespace MoonlightDaemon.App.Helpers;

public class ServerFileSystem : IDisposable
{
    private readonly UnixFileSystem FileSystem;
    private bool UsesVirtualDisk = false;

    public ServerFileSystem(ServerConfiguration configuration)
    {
        FileSystem = new(configuration.GetRuntimeVolumePath());
        UsesVirtualDisk = configuration.Limits.UseVirtualDisk;
    }

    public UnixFsEntry[] List(string path)
    {
        var error = FileSystem.ReadDir(path, out var result);
        
        error.ThrowIfError();

        if (UsesVirtualDisk && (string.IsNullOrEmpty(path) || path == "/"))
        {
            result = result
                .Where(x => !(x.Name == "lost+found" && x.IsDirectory))
                .ToArray();
        }
        
        return result;
    }

    public void Remove(string path)
    {
        var error = FileSystem.RemoveAll(path);
        
        error.ThrowIfError();
    }

    public void Move(string src, string dst)
    {
        var error = FileSystem.Rename(src, dst);
        
        error.ThrowIfError();
    }

    public void CreateDirectory(string path)
    {
        var error = FileSystem.MkdirAll(path, FilePermissions.ACCESSPERMS);
        
        error.ThrowIfError();
    }

    public void CreateFile(string path)
    {
        using var fs = OpenWriteFileStream(path);
        fs.Close();
    }

    public string ReadFile(string path)
    {
        using var fs = OpenFileReadStream(path);
        using var sw = new StreamReader(fs);

        var result = sw.ReadToEnd();
        
        sw.Close();
        fs.Close();

        return result;
    }

    public void WriteFile(string path, string data)
    {
        using var fs = OpenWriteFileStream(path);
        using var sw = new StreamWriter(fs);
        
        sw.Write(data);
        
        sw.Flush();
        fs.Flush();
        
        sw.Close();
        fs.Close();
    }

    public FileStream OpenFileReadStream(string path)
    {
        var error = FileSystem.Open(path, out var fileHandle);
        error.ThrowIfError();

        return new FileStream(fileHandle, FileAccess.Read);
    }

    public void WriteStreamToFile(string path, Stream stream)
    {
        using var fs = OpenWriteFileStream(path);
        
        stream.CopyTo(fs);
        
        fs.Flush();
        fs.Close();
    }

    public FileStream OpenWriteFileStream(string path)
    {
        var error = FileSystem.Touch(path, OpenFlags.O_RDWR | OpenFlags.O_TRUNC, FilePermissions.ACCESSPERMS, out var fs);
        error.ThrowIfError();

        return fs;
    }

    public UnixFsEntry? Stat(string path)
    {
        var error = FileSystem.Stat(path, out var stat);

        if (error != null && error.Errno == Errno.ENOENT)
            return null;
        
        error.ThrowIfError();

        return new()
        {
            Name = Path.GetFileName(path),
            IsDirectory = FileSystem.IsFileType(stat.st_mode, FilePermissions.S_IFDIR),
            IsFile = FileSystem.IsFileType(stat.st_mode, FilePermissions.S_IFREG),
            Size = stat.st_size,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(stat.st_ctime).UtcDateTime,
            LastChanged = DateTimeOffset.FromUnixTimeSeconds(stat.st_mtime).UtcDateTime
        };
    }

    public void Dispose()
    {
        FileSystem.Dispose();
    }
}