using System;
using System.IO;
using Xunit;

namespace MBBSEmu.Tests.Util
{
    /// <summary>
    ///     A Fact that runs only when MBBSEMU_TEST_MODULE_PATH points to a directory
    ///     containing the named module's files (e.g. $MBBSEMU_TEST_MODULE_PATH/RTSLORD).
    ///     Real module binaries are not redistributable, so they are never embedded as
    ///     test assets; these tests skip cleanly when the modules are absent (e.g. CI).
    /// </summary>
    public sealed class ModuleFactAttribute : FactAttribute
    {
        private const string EnvironmentVariable = "MBBSEMU_TEST_MODULE_PATH";

        public ModuleFactAttribute(string moduleName)
        {
            var root = Environment.GetEnvironmentVariable(EnvironmentVariable);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(Path.Combine(root, moduleName)))
                Skip = $"Requires {EnvironmentVariable} pointing to a directory containing {moduleName}/";
        }

        /// <summary>
        ///     Resolves the module's directory under MBBSEMU_TEST_MODULE_PATH. Only
        ///     valid inside a test that was gated by this attribute.
        /// </summary>
        public static string GetModulePath(string moduleName) =>
            Path.Combine(Environment.GetEnvironmentVariable(EnvironmentVariable), moduleName);
    }
}
