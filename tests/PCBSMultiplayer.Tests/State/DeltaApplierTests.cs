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

    [Fact]
    public void JobBoardDelta_replaces_board_contents()
    {
        var w = new WorldState();
        w.JobBoard.AddAvailable(new Job { Id = "old" });
        DeltaApplier.Apply(w, new JobBoardDelta
        {
            Available = new() { new SnapshotBuilder.JobDto { Id = "new-1" } },
            Claimed = new() { new SnapshotBuilder.JobDto { Id = "new-2", ClaimedBySlot = 1 } },
            Completed = new()
        });
        w.JobBoard.Available.Should().ContainSingle(j => j.Id == "new-1");
        w.JobBoard.Claimed.Should().ContainKey("new-2");
        w.JobBoard.Claimed["new-2"].ClaimedBySlot.Should().Be(1);
    }
}
