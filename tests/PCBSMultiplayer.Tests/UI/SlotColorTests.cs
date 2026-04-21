using PCBSMultiplayer.UI;
using Xunit;

namespace PCBSMultiplayer.Tests.UI;

public sealed class SlotColorTests
{
    [Theory]
    [InlineData(1, 0.9f, 0.3f, 0.3f)]  // red
    [InlineData(2, 0.3f, 0.5f, 0.9f)]  // blue
    [InlineData(3, 0.3f, 0.8f, 0.3f)]  // green
    [InlineData(4, 0.9f, 0.8f, 0.3f)]  // yellow
    public void Known_slots_return_expected_rgb(int slot, float r, float g, float b)
    {
        var c = SlotColor.For(slot);
        Assert.Equal(r, c.r);
        Assert.Equal(g, c.g);
        Assert.Equal(b, c.b);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Out_of_range_slots_return_white(int slot)
    {
        var c = SlotColor.For(slot);
        Assert.Equal(1f, c.r);
        Assert.Equal(1f, c.g);
        Assert.Equal(1f, c.b);
    }
}
