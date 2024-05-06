using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using MoonCore.Helpers;
using MoonlightDaemon.App.Helpers;
using MoonlightDaemon.App.Models.Abstractions;

namespace MoonlightDaemon.App.Archivers;

public class TarGzArchiver : IFileArchiver
{
    public async Task Archive(ChrootFileSystem fileSystem, string destination, string[] files)
    {
        /*
         *await using var outStream = File.Create(pathToTar);
           await using var gzoStream = new GZipOutputStream(outStream);
           using var tarArchive = TarArchive.CreateOutputTarArchive(gzoStream, Encoding.UTF8);

           tarArchive.RootPath = "/";
         * 
         */
    }

    public Task UnArchive(ChrootFileSystem fileSystem, string source, string destination)
    {
        throw new NotImplementedException();
    }
}