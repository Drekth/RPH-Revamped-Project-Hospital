using System;
using RPH.Core.HR;
using Xunit;

namespace RPH.Core.Tests;

public class PorterRegistryTests
{
    [Fact]
    public void RoundTripKeepsTheIds()
    {
        var registry = new PorterRegistry();
        registry.Add(42);
        registry.Add(7);
        registry.Add(7);

        PorterRegistry read = PorterRegistry.ReadFromString(registry.WriteToString());

        Assert.Equal(2, read.Count);
        Assert.True(read.Contains(7));
        Assert.True(read.Contains(42));
        Assert.False(read.Contains(8));
    }

    [Fact]
    public void KeepOnlyDropsTheIdsThatNoLongerExist()
    {
        var registry = new PorterRegistry();
        registry.Add(1);
        registry.Add(2);

        registry.KeepOnly(id => id == 2);

        Assert.False(registry.Contains(1));
        Assert.True(registry.Contains(2));
    }

    [Fact]
    public void ReadIgnoresUnknownAndBadLines()
    {
        PorterRegistry read = PorterRegistry.ReadFromString("RPH-PORTERS\t1\nP\t5\nX\t9\nP\tabc\n\nP\t6\n");

        Assert.Equal(2, read.Count);
        Assert.True(read.Contains(5));
        Assert.True(read.Contains(6));
    }

    [Theory]
    [InlineData("")]
    [InlineData("RPH-LEDGER\t1\n")]
    [InlineData("RPH-PORTERS\t99\nP\t1\n")]
    public void ReadRejectsOtherFilesAndNewerFormats(string text)
    {
        Assert.Throws<FormatException>(() => PorterRegistry.ReadFromString(text));
    }
}
