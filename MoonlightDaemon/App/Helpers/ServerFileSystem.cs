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
        if (IsUnsafe(path))
            throw new UnsafeFileAccessException();

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
        var dirOfTarget = Formatter.ReplaceEnd(path, Path.GetFileName(path), "");

        if (IsUnsafe(dirOfTarget))
            throw new UnsafeFileAccessException();

        var fullPath = GetRealPath(path);

        if (IsSymlink(fullPath))
            Syscall.unlink(fullPath);
        else if(File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public Task DeleteDirectory(string path)
    {
        var dirOfTarget = Formatter.ReplaceEnd(path, Path.GetFileName(path), "");

        if (IsUnsafe(dirOfTarget))
            throw new UnsafeFileAccessException();

        var fullPath = GetRealPath(path);

        if (IsSymlink(fullPath))
            Syscall.unlink(fullPath);
        else if (Directory.Exists(fullPath))
            Directory.Delete(fullPath, true);

        return Task.CompletedTask;
    }

    public Task Move(string from, string to)
    {
        if (IsUnsafe(from))
            throw new UnsafeFileAccessException();

        if (IsUnsafe(to))
            throw new UnsafeFileAccessException();

        var fromFull = GetRealPath(from);
        var toFull = GetRealPath(to);

        if (File.Exists(fromFull))
        {
            File.Move(fromFull, toFull);
        }
        else
            Directory.Move(fromFull, toFull);

        return Task.CompletedTask;
    }

    public Task CreateDirectory(string path)
    {
        if (IsUnsafe(path))
            throw new UnsafeFileAccessException();

        var fullPath = GetRealPath(path);

        Directory.CreateDirectory(fullPath);

        return Task.CompletedTask;
    }

    public async Task CreateFile(string path)
    {
        if (IsUnsafe(path))
            throw new UnsafeFileAccessException();

        var fullPath = GetRealPath(path);

        if (File.Exists(fullPath))
            return;

        EnsureParentDirectoryForFile(fullPath);

        await File.WriteAllTextAsync(fullPath, "");
    }

    public async Task<string> ReadFile(string path)
    {
        if (IsUnsafe(path))
            throw new UnsafeFileAccessException();

        var fullPath = GetRealPath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException();

        await using var fs = await SafeReadFileStream(fullPath);
        using var streamReader = new StreamReader(fs);

        return await streamReader.ReadToEndAsync();
    }

    public async Task WriteFile(string path, string content)
    {
        if (IsUnsafe(path))
            throw new UnsafeFileAccessException();

        var fullPath = GetRealPath(path);

        if (File.Exists(fullPath) && IsUnsafe(fullPath))
            throw new UnsafeFileAccessException();

        EnsureParentDirectoryForFile(fullPath);

        await using var fs = await SafeWriteFileStream(path);
        await using var streamWriter = new StreamWriter(fs);

        await streamWriter.WriteAsync(content);
    }

    public async Task<Stream> ReadFileStream(string path)
    {
        if (IsUnsafe(path))
            throw new UnsafeFileAccessException();

        var fullPath = GetRealPath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException();

        return await SafeReadFileStream(fullPath);
    }

    public async Task WriteFileStream(string path, Stream dataStream)
    {
        if (IsUnsafe(path))
            throw new UnsafeFileAccessException();

        var fullPath = GetRealPath(path);

        EnsureParentDirectoryForFile(fullPath);

        var fs = await SafeWriteFileStream(fullPath);

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

    private bool IsUnsafe(string path) // Checks if any part of the path is a symlink
    {
        var previousPath = "/";

        foreach (var pathPart in path.Split("/"))
        {
            var fullPath = GetRealPath(previousPath + pathPart);

            if (IsSymlink(fullPath))
                return true;

            previousPath += pathPart + "/";
        }

        return false;
    }

    private bool IsSymlink(string path) // Check for a symlink at the path
    {
        if (File.Exists(path))
        {
            var fi = new FileInfo(path);

            if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return true;

            if (!string.IsNullOrEmpty(fi.LinkTarget))
                return true;
        }
        else if (Directory.Exists(path))
        {
            var di = new DirectoryInfo(path);

            if (di.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return true;

            if (!string.IsNullOrEmpty(di.LinkTarget))
                return true;
        }

        return false;
    }

    private Task<Stream> SafeReadFileStream(string fullPath)
    {
        var fi = new FileInfo(fullPath);

        // Double check if the accessed file is actually inside the server data directory
        if (!fi.FullName.StartsWith(RootPath))
            throw new UnsafeFileAccessException();

        var fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        return Task.FromResult<Stream>(fs);
    }

    private Task<Stream> SafeWriteFileStream(string fullPath)
    {
        var fi = new FileInfo(fullPath);

        // Double check if the accessed file is actually inside the server data directory
        if (!fi.FullName.StartsWith(RootPath))
            throw new UnsafeFileAccessException();

        var fs = fi.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

        return Task.FromResult<Stream>(fs);
    }
}