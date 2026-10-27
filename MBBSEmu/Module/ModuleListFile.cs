using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Adds modules to a modules.json file, the same format -C reads
    /// </summary>
    public static class ModuleListFile
    {
        /// <summary>
        ///     The module list the admin console loads and saves when MBBSEmu runs without arguments
        /// </summary>
        public const string DefaultFileName = "modules.json";

        /// <summary>
        ///     Adds a module to the end of the list at path, creating the file if there isn't one
        ///
        ///     The file is edited as a JSON document rather than re-serialized, so everything already in it (BasePath,
        ///     relative paths, patches, disabled modules, menu keys) stays as written. It's written to a temporary file
        ///     first and then moved over the original, so a failed write can't leave half a file behind.
        /// </summary>
        /// <param name="modulePath">Absolute, so a BasePath in the file isn't combined with it</param>
        /// <exception cref="InvalidDataException">The existing file isn't a module list that can be added to</exception>
        public static void Append(string path, string identifier, string modulePath, string menuOptionKey)
        {
            var root = File.Exists(path) ? Read(path) : new JsonObject();

            if (!root.TryGetPropertyValue("Modules", out var modules) || modules == null)
            {
                modules = new JsonArray();
                root["Modules"] = modules;
            }

            if (modules is not JsonArray list)
                throw new InvalidDataException($"{path} has a Modules value that isn't a list of modules");

            list.Add(new JsonObject
            {
                ["Identifier"] = identifier,
                ["Path"] = modulePath,
                ["MenuOptionKey"] = menuOptionKey
            });

            var temporaryFile = path + ".tmp";
            try
            {
                File.WriteAllText(temporaryFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporaryFile, path, overwrite: true);
            }
            catch
            {
                TryDelete(temporaryFile);
                throw;
            }
        }

        private static JsonObject Read(string path)
        {
            JsonNode root;
            try
            {
                root = JsonNode.Parse(File.ReadAllText(path));
            }
            catch (JsonException e)
            {
                throw new InvalidDataException($"{path} isn't valid JSON: {e.Message}", e);
            }

            return root as JsonObject ?? throw new InvalidDataException($"{path} isn't a module list");
        }

        private static void TryDelete(string file)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                //Leaving it behind is harmless; the original is untouched
            }
        }
    }
}
