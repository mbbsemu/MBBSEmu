using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Tests
{
    public abstract class TestBase
    {
        static TestBase()
        {
            DependencyInjection.ServiceResolver.Create();
        }
    }
}
