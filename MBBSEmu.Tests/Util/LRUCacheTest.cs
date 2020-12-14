using FluentAssertions;
using MBBSEmu.Util;
using System.Collections.Generic;
using System;
using Xunit;

namespace MBBSEmu.Tests.Util
{
  public class LRUCacheTest
  {
    [Fact]
    public void invalidKey()
    {
      LRUCache<int, string> cache = new(1);
      cache.Count.Should().Be(0);
      cache.ListCount.Should().Be(0);
      Action action = () => cache[0].Should().Be("test");
      action.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void addAndGet()
    {
      LRUCache<int, string> cache = new(1);
      cache.Add(5, "test");
      cache.Count.Should().Be(1);
      cache.ListCount.Should().Be(1);
      cache.MostRecentlyUsed.Should().Be(5);

      cache[5].Should().Be("test");
    }

    [Fact]
    public void addAndChangeAndGet()
    {
      LRUCache<int, string> cache = new(1);
      cache.Add(5, "test");
      cache.Count.Should().Be(1);
      cache.ListCount.Should().Be(1);
      cache.MostRecentlyUsed.Should().Be(5);

      cache[5] = "another";

      cache.Count.Should().Be(1);
      cache.ListCount.Should().Be(1);
      cache.MostRecentlyUsed.Should().Be(5);
      cache[5].Should().Be("another");
    }

    [Fact]
    public void addManyMostRecentlyUsed()
    {
      LRUCache<int, string> cache = new(3);
      cache.Add(5, "test");
      cache.Count.Should().Be(1);
      cache.ListCount.Should().Be(1);
      cache.MostRecentlyUsed.Should().Be(5);
      cache[5].Should().Be("test");

      cache[6] = "test2";
      cache.Count.Should().Be(2);
      cache.ListCount.Should().Be(2);
      cache.MostRecentlyUsed.Should().Be(6);
      cache[6].Should().Be("test2");

      cache[7] = "test3";
      cache.Count.Should().Be(3);
      cache.ListCount.Should().Be(3);
      cache.MostRecentlyUsed.Should().Be(7);
      cache[7].Should().Be("test3");

      cache[6].Should().Be("test2");
      cache[5].Should().Be("test");
    }

    [Fact]
    public void addManyReplacesOldest()
    {
      LRUCache<int, string> cache = new(3);
      cache.Add(5, "test");
      cache[6] = "test2";
      cache[7] = "test3";

      cache[8] = "test4";
      cache.Count.Should().Be(3);
      cache.ListCount.Should().Be(3);
      cache.MostRecentlyUsed.Should().Be(8);
      cache[8].Should().Be("test4");
      cache[7].Should().Be("test3");
      cache.MostRecentlyUsed.Should().Be(7);
      cache[6].Should().Be("test2");
      cache.MostRecentlyUsed.Should().Be(6);
      cache.ContainsKey(5).Should().BeFalse();

      // add some more, 6, 7 are the most recently touched
      cache[9] = "test5"; // pushes out 8
      cache.Count.Should().Be(3);
      cache.ListCount.Should().Be(3);
      cache.MostRecentlyUsed.Should().Be(9);

      cache[7].Should().Be("test3");
      cache.MostRecentlyUsed.Should().Be(7);
      cache[6].Should().Be("test2");
      cache.MostRecentlyUsed.Should().Be(6);
      cache.ContainsKey(8).Should().BeFalse();
    }
  }
}
