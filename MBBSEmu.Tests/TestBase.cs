using System.IO;
using System;

namespace MBBSEmu.Tests
{
    public abstract class TestBase
    {
        protected static readonly Random RANDOM = new Random();

        static TestBase()
        {

        }

        protected string GetModulePath()
        {
            return Path.Join(Path.GetTempPath(), $"mbbsemu{RANDOM.Next()}");
        }
    }
}
