using System;
using System.IO;
using System.Reflection;

namespace MBBSEmu.Resources
{
    public class ResourceManager : IResourceManager
    {
        private readonly Assembly _assembly;

        public ResourceManager(Assembly assembly)
        {
            _assembly = assembly;
        }

        /// <summary>
        ///     Gets an embedded string resource by key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetString(string key)
        {
            using var stream = _assembly.GetManifestResourceStream(key);
            using var reader =
                new StreamReader(stream ?? throw new InvalidOperationException(
                                     $"Unable to open Stream for Embedded Resource {key}"));
            return reader.ReadToEnd();
        }
    }
}
