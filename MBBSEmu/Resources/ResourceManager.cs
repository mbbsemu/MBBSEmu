using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace MBBSEmu.Resources
{
    /// <summary>
    ///     Handles retrieval of embedded resources
    ///
    ///     This class is used for things like the command line help, text file assets, and the embedded
    ///     SQL queries for Dapper
    /// </summary>
    public class ResourceManager : IResourceManager
    {
        private readonly Assembly _assembly;
        private readonly Dictionary<string, byte[]> _resourceCache;
        private static readonly byte[] utf8bom = {0xEF, 0xBB, 0xBF};

        public ResourceManager()
        {
            _resourceCache = new Dictionary<string, byte[]>();
            _assembly = Assembly.GetExecutingAssembly();
        }

        public ReadOnlySpan<byte> GetResource(string key)
        {
            if (!_resourceCache.TryGetValue(key, out var result))
            {
                using var resourceStream = _assembly.GetManifestResourceStream(key);
                using var binaryReader = new BinaryReader(resourceStream ?? throw new InvalidOperationException(
                                                              $"Unable to open Stream for Embedded Resource {key}"));
                result = binaryReader.ReadBytes((int) resourceStream.Length);
                _resourceCache.Add(key, result);
            }

            return result;
        }

        /// <summary>
        ///     Gets an embedded string resource by key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetString(string key)
        {
            var result = GetResource(key);

            //Files Saved by Visual Studio are UTF-8 by default, so convert them to ASCII and strip UTF-8 byte order mark
            if (result.Length > 3 &&  result.Slice(0, 3).SequenceEqual(utf8bom))
                return Encoding.ASCII.GetString(result.Slice(3));

            return Encoding.ASCII.GetString(result);
        }
    }
}
