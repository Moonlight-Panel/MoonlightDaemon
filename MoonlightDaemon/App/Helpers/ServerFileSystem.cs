using Mono.Unix.Native;
using MoonCore.Helpers;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Http.Resources;

namespace MoonlightDaemon.App.Helpers;

public class ServerFileSystem
{
    private readonly string RootPath;

    public ServerFileSystem(string rootPath)
    {
        RootPath = rootPath;
    }
    
    public Task<FileEntry[]> List(string path)
    {
        var fullPath = GetRealPath(path);

        var result = new List<FileEntry>();
        
        foreach (var file in Directory.GetFiles(fullPath))
        {
            var fi = new FileInfo(file);
            
            result.Add(new()
            {
                Name = fi.Name,
                Size = fi.Length,
                IsDirectory = false,
                IsFile = true,
                LastModifiedAt = fi.LastWriteTimeUtc
            });
        }

        foreach (var directory in Directory.GetDirectories(fullPath))
        {
            var di = new DirectoryInfo(directory);
            
            result.Add(new()
            {
                Name = di.Name,
                Size = 0,
                IsDirectory = true,
                LastModifiedAt = di.LastWriteTimeUtc,
                IsFile = false
            });
        }

        return Task.FromResult(result.ToArray());
    }

    public Task DeleteFile(string path)
    {
        var fullPath = GetRealPath(path);

        if(!File.Exists(fullPath))
            return Task.CompletedTask;
        
        var fi = new FileInfo(fullPath);

        if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint) || !string.IsNullOrEmpty(fi.LinkTarget))
            Syscall.unlink(fullPath);
        else
            File.Delete(fullPath);
        
        return Task.CompletedTask;
    }

    public Task DeleteDirectory(string path)
    {
        var fullPath = GetRealPath(path);

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException();
        
        Directory.Delete(fullPath, true);
        
        return Task.CompletedTask;
    }

    public Task Move(string from, string to)
    {
        var fromFull = GetRealPath(from);
        var toFull = GetRealPath(to);

        if (File.Exists(fromFull))
        {
            if (IsUnsafe(fromFull))
                throw new UnsafeFileAccessException();
            
            File.Move(fromFull, toFull);
        }
        else
            Directory.Move(fromFull, toFull);
        
        return Task.CompletedTask;
    }

    public Task CreateDirectory(string path)
    {
        var fullPath = GetRealPath(path);

        Directory.CreateDirectory(fullPath);
        
        return Task.CompletedTask;
    }

    public async Task CreateFile(string path)
    {
        var fullPath = GetRealPath(path);
        
        if(File.Exists(fullPath))
            return;
        
        EnsureParentDirectoryForFile(fullPath);
        
        await File.WriteAllTextAsync(fullPath, "");
    }

    public async Task<string> ReadFile(string path)
    {
        var fullPath = GetRealPath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException();

        if (IsUnsafe(fullPath))
            throw new UnsafeFileAccessException();

        return await File.ReadAllTextAsync(fullPath);
    }

    public async Task WriteFile(string path, string content)
    {
        var fullPath = GetRealPath(path);

        if (File.Exists(fullPath) && IsUnsafe(fullPath))
            throw new UnsafeFileAccessException();
        
        EnsureParentDirectoryForFile(fullPath);

        await File.WriteAllTextAsync(fullPath, content);
    }

    public Task<Stream> ReadFileStream(string path)
    {
        var fullPath = GetRealPath(path);
        
        if (!File.Exists(fullPath))
            throw new FileNotFoundException();

        if (IsUnsafe(fullPath))
            throw new UnsafeFileAccessException(fullPath);

        var fs = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        
        return Task.FromResult<Stream>(fs);
    }

    public async Task WriteFileStream(string path, Stream dataStream)
    {
        var fullPath = GetRealPath(path);
        
        if (File.Exists(fullPath) && IsUnsafe(fullPath))
            throw new UnsafeFileAccessException(fullPath);

        EnsureParentDirectoryForFile(fullPath);
        
        var fs = File.Create(fullPath);

        dataStream.Position = 0;
        await dataStream.CopyToAsync(fs);

        await fs.FlushAsync();
        fs.Close();
    }

    private void EnsureParentDirectoryForFile(string fullPath)
    {
        var path = Formatter.ReplaceEnd(fullPath, Path.GetFileName(fullPath), "");
        Directory.CreateDirectory(path);
    }

    private string FixPath(string path)
    {
        var result = path;

        if (!result.StartsWith("/"))
            result = "/" + result;

        result = result.Replace("..", "");
        result = result.Replace("//", "/");
        
        return result;
    }

    public string GetRealPath(string path)
    {
        var fixedPath = FixPath(path);
        return RootPath + fixedPath;
    }

    private bool IsUnsafe(string path)
    {
        var fi = new FileInfo(path);

        if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            Logger.Debug($"Symlink detected: {fi.FullName}");
            return true;
        }

        if (!string.IsNullOrEmpty(fi.LinkTarget))
        {
            Logger.Debug($"Symlink target detected: {fi.LinkTarget}");
            return true;
        }

        return false;
    }
}