using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using MoonCore.Helpers;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Archivers;

public class TarGzArchiver : IFileArchiver
{
    public async Task Archive(ServerFileSystem fileSystem, string destination, string[] files)
    {/*
        await using var tarFileStream = fileSystem.OpenWriteFileStream(destination);

        await using var gzoStream = new GZipOutputStream(tarFileStream);
        using var tarArchive = TarArchive.CreateOutputTarArchive(gzoStream, Encoding.UTF8);

        tarArchive.RootPath = "/";

        foreach (var file in files)
        {
            try
            {
                var fi = fileSystem.Stat(file);

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

                    if (!linkTarget.FullName.StartsWith(fi.FullName))
                        continue; // ignore
                }
                
                var entry = TarEntry.CreateEntryFromFile(fi.FullName);
                entry.Name = fi.Name;
                tarArchive.WriteEntry(entry, false);
            }
            catch (FileNotFoundException)
            {
                var directory = fileSystem.StatDirectory(file);
                await AddDirectoryToTarNew(tarArchive, fileSystem, file);
            }
        }
        
        tarArchive.Close();
        
        await tarFileStream.FlushAsync();
        tarFileStream.Close();*/
    }

    public Task UnArchive(ServerFileSystem fileSystem, string source, string destination)
    {
        throw new NotImplementedException();
    }
    /*
    private async Task AddDirectoryToTarNew(TarArchive archive, ServerFileSystem fileSystem, string root)
    {
        foreach (var file in fileSystem.ListFiles(root))
        {
            var fi = fileSystem.Stat(root + file.Name);

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

                if (!linkTarget.FullName.StartsWith(fi.FullName))
                    continue; // ignore
            }
                
            var entry = TarEntry.CreateEntryFromFile(fi.FullName);
            entry.Name = root + file.Name;
            archive.WriteEntry(entry, false);
        }

        foreach (var directory in fileSystem.ListDirectories(root))
        {
            await AddDirectoryToTarNew(archive, fileSystem, root + directory.Name + "/");
        }
    }*/
}