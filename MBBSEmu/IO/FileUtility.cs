using NLog;
using System.IO;
using System.Runtime.InteropServices;

namespace MBBSEmu.IO
{
    /// <summary>
    ///     File Finder Utility
    ///
    ///     Because MBBSEmu is cross platform and MajorBBS/Worldgroup were originally not, the file names
    ///     and paths used in Modules might be valid on DOS but not on a case sensitive file systems on
    ///     Linux or OSX. This class helps locating a file that might otherwise be "not found" if the name
    ///     were treated literally.
    /// </summary>
    public class FileUtility : IFileUtility
    {
        private readonly ILogger _logger;

        public FileUtility(ILogger logger)
        {
            _logger = logger;
        }

        public string FindFile(string modulePath, string fileName)
        {
            //Strip any absolute pathing
            if (fileName.ToUpper().StartsWith(@"\BBSV6") || fileName.ToUpper().StartsWith(@"\WGSERV"))
            {
                var relativePathStart = fileName.IndexOf('\\', fileName.IndexOf('\\') + 1);
                fileName = fileName.Substring(relativePathStart + 1);
            }

            fileName = CorrectPathSeparator(fileName);

            //Duh
            if (File.Exists($"{modulePath}{fileName}"))
                return fileName;

            //Check all caps
            if (File.Exists($"{modulePath}{fileName.ToUpper()}"))
                return fileName.ToUpper();

            //Check all lower case
            if (File.Exists($"{modulePath}{fileName.ToLower()}"))
                return fileName.ToLower();

            //Set Directory Specifier depending on the platform
            var directorySpecifier = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\" : "/";

            //Strip Relative Pathing, it'll always be relative to the module location
            if (fileName.StartsWith($".{directorySpecifier}"))
                fileName = fileName.Substring(2);

            if (fileName.Contains(directorySpecifier))
            {
                var fileNameElements = fileName.Split(directorySpecifier);

                //We only support 1 directory deep.. for now
                if (fileNameElements.Length > 2 || fileNameElements.Length == 0)
                    return fileName;

                fileNameElements[0] = fileNameElements[0].ToUpper();
                fileNameElements[1] = fileNameElements[1].ToUpper();
                if (File.Exists($"{modulePath}{fileNameElements[0]}{directorySpecifier}{fileNameElements[1]}"))
                    return string.Join(directorySpecifier, fileNameElements);

                fileNameElements[0] = fileNameElements[0].ToLower();
                fileNameElements[1] = fileNameElements[1].ToUpper();
                if (File.Exists($"{modulePath}{fileNameElements[0]}{directorySpecifier}{fileNameElements[1]}"))
                    return string.Join(directorySpecifier, fileNameElements);

                fileNameElements[0] = fileNameElements[0].ToUpper();
                fileNameElements[1] = fileNameElements[1].ToLower();
                if (File.Exists($"{modulePath}{fileNameElements[0]}{directorySpecifier}{fileNameElements[1]}"))
                    return string.Join(directorySpecifier, fileNameElements);

                fileNameElements[0] = fileNameElements[0].ToLower();
                fileNameElements[1] = fileNameElements[1].ToLower();
                if (File.Exists($"{modulePath}{fileNameElements[0]}{directorySpecifier}{fileNameElements[1]}"))
                    return string.Join(directorySpecifier, fileNameElements);
            }

            _logger.Warn($"Unable to locate file attempting multiple cases: {fileName}");

            return fileName;
        }

        /// <summary>
        ///     Because all files/paths used by Modules are assumed DOS, if we're running on a modern
        ///     Linux platform we need to replace the pathing character with the correct one if the files
        ///     are in a sub directory
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public string CorrectPathSeparator(string fileName) => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? fileName
            : fileName.Replace(@"\", "/");
    }
}
