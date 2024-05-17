using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace MoonlightDaemon.App.Helpers;

public static class ArchiveHelper
{
    public static async Task ArchiveToServerFs(string destination, ServerFileSystem fileSystem, string[] items)
    {
        await using var fs = fileSystem.OpenWriteFileStream(destination);

        await ArchiveToTar(fs, fileSystem, items);
    }

    public static async Task ArchiveToTarFile(string fullPath, ServerFileSystem fileSystem, string[] items)
    {
        await using var fs = File.Create(fullPath);

        await ArchiveToTar(fs, fileSystem, items);
    }
    
    public static async Task ArchiveToTar(FileStream fs, ServerFileSystem fileSystem, string[] items)
    {
        await using var gzipStream = new GZipOutputStream(fs);
        await using var tarStream = new TarOutputStream(gzipStream, Encoding.UTF8);
        
        foreach (var item in items)
        {
            var itemDetails = fileSystem.Stat(item);

            if (itemDetails == null)
                continue;

            if (itemDetails.IsDirectory)
                await ArchiveDirectory(fileSystem, tarStream, item);
            else if (itemDetails.IsFile)
                await ArchiveFile(fileSystem, tarStream, string.Empty, item);
        }

        await tarStream.FlushAsync();
        await gzipStream.FlushAsync();
        await fs.FlushAsync();
        
        tarStream.Close();
        gzipStream.Close();
        fs.Close();
    }

    private static async Task ArchiveDirectory(ServerFileSystem fileSystem, TarOutputStream tarOutputStream, string directory)
    {
        var items = fileSystem.List(directory);

        foreach (var entry in items)
        {
            if (entry.IsDirectory)
                await ArchiveDirectory(fileSystem, tarOutputStream, directory + "/" + entry.Name);
            else if (entry.IsFile)
                await ArchiveFile(fileSystem, tarOutputStream, directory, entry.Name);
        }
    }

    private static async Task ArchiveFile(ServerFileSystem fileSystem, TarOutputStream tarOutputStream, string parentDirectory, string file)
    {
        var path = (string.IsNullOrEmpty(parentDirectory) ? "" : parentDirectory + "/") + file;
        var dataStream = fileSystem.OpenFileReadStream(path);
        
        // Meta 
        var entry = TarEntry.CreateTarEntry(path);
        entry.Size = dataStream.Length;
        await tarOutputStream.PutNextEntryAsync(entry, CancellationToken.None);
        
        // Data
        await dataStream.CopyToAsync(tarOutputStream);
        dataStream.Close();

        // Close the entry
        tarOutputStream.CloseEntry();
    }

    public static async Task ExtractFromTarFile(string fullPath, ServerFileSystem fileSystem, string destination)
    {
        await using var fs = File.OpenRead(fullPath);

        await ExtractFromTar(fs, fileSystem, destination);
    }

    public static async Task ExtractFromServerFs(string path, ServerFileSystem fileSystem, string destination)
    {
        await using var fs = fileSystem.OpenFileReadStream(path);

        await ExtractFromTar(fs, fileSystem, destination);
    }
    
    public static async Task ExtractFromTar(FileStream fs, ServerFileSystem fileSystem, string destination)
    {
        await using var gzipStream = new GZipInputStream(fs);
        await using var tarStream = new TarInputStream(gzipStream, Encoding.UTF8);
        
        while (true)
        {
            var entry = await tarStream.GetNextEntryAsync(CancellationToken.None);
            
            if(entry == null)
                break;
            
            await using var outputStream = fileSystem.OpenWriteFileStream(destination + "/" + entry.Name);
            await tarStream.CopyEntryContentsAsync(outputStream, CancellationToken.None);
            
            await outputStream.FlushAsync();
            outputStream.Close();
        }
        
        tarStream.Close();
        gzipStream.Close();
        fs.Close();
    }
}