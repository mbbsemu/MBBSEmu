using NLog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private static readonly char[] PATH_SEPARATORS = {'\\', '/'};

        public static readonly EnumerationOptions CASE_INSENSITIVE_ENUMERATION_OPTIONS = new EnumerationOptions() {
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

        public static FileUtility CreateForTest()
        {
            return new FileUtility(null);
        }

        public static string[] SplitIntoComponents(string path) {
            return path.Split(PATH_SEPARATORS);
        }

        public string FindFile(string modulePath, string fileName)
        {
            //Strip any absolute pathing
            if (fileName.ToUpper().StartsWith(@"\BBSV6") || fileName.ToUpper().StartsWith(@"\WGSERV") || fileName.ToUpper().StartsWith(@"C:\BBSV6"))
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

            // since SearchPath returns the full path, we relativize it based on modulePath
            return Path.GetRelativePath(modulePath, SearchPath(modulePath, pathComponents));
        }

        /// <summary>
        ///     Searches case-insensitivly for the filename in pathComponents
        /// </summary>
        /// <param name="currentPath">Root path to search from</param>
        /// <param name="pathComponents">Remaining path components to look for</param>
        /// <returns>The FULL PATH of the filename, which may be differently cased than
        ///     the individual path components. The value will always be
        ///     currentPath + pathComponents</returns>
        private string SearchPath(string currentPath, Queue<string> pathComponents)
        {
            var component = pathComponents.Dequeue();
            string found;
            if (pathComponents.Count == 0)
            {
                found = FindByEnumeration(currentPath, component, Directory.EnumerateFileSystemEntries);
                return String.IsNullOrEmpty(found) ? Path.Combine(currentPath, component) : found;
            }

            // recurse into the next directory
            found = FindByEnumeration(currentPath, component, Directory.EnumerateDirectories);
            return String.IsNullOrEmpty(found)
                ? CombineRemainingPaths(Path.Combine(currentPath, component), pathComponents)
                : SearchPath(found, pathComponents);
        }

        private static string CombineRemainingPaths(String rootPath, IEnumerable<string> restOfPaths)
        {
            var resultingPath = rootPath;
            foreach (var path in restOfPaths)
            {
                resultingPath = Path.Combine(resultingPath, path);
            }

            return resultingPath;
        }

        private delegate IEnumerable<string> EnumerateFilesystemObjects(string path, string search, EnumerationOptions enumerationOptions);

        /// <summary>
        ///     Searches case-insensitivly for filename by enumerating filesystem objects through
        ///     enumerateDelegate
        /// </summary>
        /// <param name="root">Root path to search from. Limited to search just this root, i.e. does
        ///     not descend into subdirectories.</param>
        /// <param name="filename">Filename to look for, case insensitive</param>
        /// <param name="enumerateDelegate">Function to call to get filesystem objects</param>
        /// <returns>The FULL PATH of the matching filename, which may be differently cased than
        ///     filename, or null if not found</returns>
        private string FindByEnumeration(string root, string filename, EnumerateFilesystemObjects enumerateDelegate)
            => enumerateDelegate(root, filename, CASE_INSENSITIVE_ENUMERATION_OPTIONS).DefaultIfEmpty(null).First();

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
