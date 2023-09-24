using System.IO;
using System;
using Xunit;

namespace MBBSEmu.Tests
{
    [CollectionDefinition("Non-Parallel", DisableParallelization = true)]
    public class NonParallelCollectionDefinitionClass
    {
    }

    public abstract class TestBase
    {
        protected static readonly Random RANDOM = new(Guid.NewGuid().GetHashCode());

        static TestBase()
        {

        }

        protected string GetModulePath()
        {
            return Path.Join(Path.GetTempPath(), $"mbbsemu{RANDOM.Next()}");
        }
    }
}
