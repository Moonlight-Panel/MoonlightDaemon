using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Archivers;

public class TarGzArchiver : IFileArchiver
{
    public async Task Archive(ServerFileSystem fileSystem, string destination, string[] files)
    {
        await AddDirectoryToTarNew()
    }

    public Task UnArchive(ServerFileSystem fileSystem, string source, string destination)
    {
        throw new NotImplementedException();
    }
    
    private async Task AddDirectoryToTarNew(TarArchive archive, ServerFileSystem fileSystem, string root)
    {
        var items = await fileSystem.List(root);
        
        foreach (var item in items)
        {
            if (item.IsFile)
            {
                var fullPath = fileSystem.GetRealPath(root + item.Name);
                var fi = new FileInfo(fullPath);

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

                    if (!linkTarget.FullName.StartsWith(fileSystem.GetRealPath("/")))
                        continue; // ignore
                }
                
                var entry = TarEntry.CreateEntryFromFile(fullPath);
                entry.Name = root + item.Name;
                archive.WriteEntry(entry, false);
            }
            else
                await AddDirectoryToTarNew(archive, fileSystem, root + item.Name + "/");
        }
    }

    private async Task UnArchiveTar(string pathToTar, string dst)
    {
        await using var outStream = File.OpenRead(pathToTar);
        await using var gzoStream = new GZipInputStream(outStream);
        using var tarArchive = TarArchive.CreateInputTarArchive(gzoStream, Encoding.UTF8);
        
        tarArchive.ExtractContents(dst, false);
    }
}