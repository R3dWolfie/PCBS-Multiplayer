using FluentAssertions;
using PCBSMultiplayer.State;
using Xunit;

namespace PCBSMultiplayer.Tests.State;

public class WorldStateTests
{
    [Fact]
    public void New_state_has_zero_money_zero_xp_day_one()
    {
        var w = new WorldState();
        w.Money.Should().Be(0);
        w.XP.Should().Be(0);
        w.DayIndex.Should().Be(1);
    }

    [Fact]
    public void Mutations_persist()
    {
        var w = new WorldState { Money = 500, XP = 120, DayIndex = 7 };
        w.Money.Should().Be(500);
        w.XP.Should().Be(120);
        w.DayIndex.Should().Be(7);
    }
}
