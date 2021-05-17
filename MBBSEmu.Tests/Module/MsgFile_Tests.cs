using FluentAssertions;
using MBBSEmu.IO;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace MBBSEmu.Tests.Module
{
  public class MsgFile_Tests : TestBase
  {
    private MemoryStream load(string resourceFile)
    {
        return new MemoryStream(ResourceManager.GetTestResourceManager().GetResource($"MBBSEmu.Tests.Assets.{resourceFile}").ToArray());
    }

    [Fact]
    public void test()
    {
        var abc = new MemoryStream();
        using var i = new StreamStream(load("MBBSEMU.MSG"));
        using var o = new StreamStream(abc);

        MsgFile.UpdateValues(i, o, new Dictionary<string, string>());

        abc.Flush();
        abc.Seek(0, SeekOrigin.Begin);
        var result = abc.ToString();

        var beginning = load("MBBSEMU.MSG").ToString();

        beginning.Should().Be("hahaha");
        beginning.Should().Be(result);
    }
  }
}
