using NLog;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System;

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
        private static readonly Char[] PATH_SEPARATORS = new char[] {'\\', '/'};

        private static readonly EnumerationOptions CASE_INSENSITIVE_ENUMERATION_OPTIONS = new EnumerationOptions() {
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
        };

        private readonly ILogger _logger;

        public FileUtility(ILogger logger)
        {
            _logger = logger;
        }

        public static FileUtility createForTest()
        {
            return new FileUtility(null);
        }

        public string FindFile(string modulePath, string fileName)
        {
            //Strip any absolute pathing
            if (fileName.ToUpper().StartsWith(@"\BBSV6") || fileName.ToUpper().StartsWith(@"\WGSERV"))
            {
                var relativePathStart = fileName.IndexOf('\\', fileName.IndexOf('\\') + 1);
                fileName = fileName.Substring(relativePathStart + 1);
            }

            //Strip Relative Pathing, it'll always be relative to the module location
            if (fileName.StartsWith(".\\") || fileName.StartsWith("./"))
                fileName = fileName.Substring(2);

            Queue<string> pathComponents = new Queue<string>();
            foreach (string pathComponent in fileName.Split(PATH_SEPARATORS))
            {
                pathComponents.Enqueue(pathComponent);
            }

            string fullPath = SearchPath(modulePath, pathComponents);
            if (fullPath == null)
            {
                _logger?.Info($"Unable to find {fileName} under {modulePath}");
                return null;
            }
            return Path.GetRelativePath(modulePath, fullPath);
        }

        private string SearchPath(string currentPath, Queue<string> pathComponents)
        {
            string component = pathComponents.Dequeue();
            if (pathComponents.Count == 0)
            {
                return FindByEnumeration(currentPath, component, Directory.EnumerateFiles);
            }

            // recurse into the next directory
            string found = FindByEnumeration(currentPath, component, Directory.EnumerateDirectories);
            return found == null ? null : SearchPath(found, pathComponents);
        }

        private delegate IEnumerable<string> EnumerateFilesystemObjects(string path, string search, EnumerationOptions enumerationOptions);

        private string FindByEnumeration(string root, string filename, EnumerateFilesystemObjects enumerateDelegate) {
            foreach (string file in enumerateDelegate(root, filename, CASE_INSENSITIVE_ENUMERATION_OPTIONS))
            {
                return file;
            }
            return null;
        }

        /// <summary>
        ///     Because all files/paths used by Modules are assumed DOS, if we're running on a modern
        ///     Linux platform we need to replace the pathing character with the correct one if the files
        ///     are in a sub directory
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public string CorrectPathSeparator(string fileName)
        {
            return fileName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        }
    }
}
