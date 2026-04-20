using FluentAssertions;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.State;
using Xunit;

namespace PCBSMultiplayer.Tests.State;

public class DeltaApplierTests
{
    [Fact]
    public void MoneyChanged_overwrites_total()
    {
        var w = new WorldState { Money = 100 };
        DeltaApplier.Apply(w, new MoneyChanged { NewTotal = 750 });
        w.Money.Should().Be(750);
    }

    [Fact]
    public void XPChanged_overwrites_total()
    {
        var w = new WorldState { XP = 10 };
        DeltaApplier.Apply(w, new XPChanged { NewTotal = 42 });
        w.XP.Should().Be(42);
    }

    [Fact]
    public void TimeChanged_overwrites_day_index()
    {
        var w = new WorldState { DayIndex = 1 };
        DeltaApplier.Apply(w, new TimeChanged { NewDayIndex = 5 });
        w.DayIndex.Should().Be(5);
    }
}
