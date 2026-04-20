using FluentAssertions;
using PCBSMultiplayer.State;
using Xunit;

namespace PCBSMultiplayer.Tests.State;

public class SnapshotBuilderTests
{
    [Fact]
    public void Round_trip_preserves_money_xp_day()
    {
        var src = new WorldState { Money = 9999, XP = 250, DayIndex = 12 };
        var bytes = SnapshotBuilder.Serialize(src);
        var dst = SnapshotBuilder.Deserialize(bytes);
        dst.Money.Should().Be(9999);
        dst.XP.Should().Be(250);
        dst.DayIndex.Should().Be(12);
    }

    [Fact]
    public void Round_trip_preserves_job_board_contents()
    {
        var src = new WorldState();
        src.JobBoard.AddAvailable(new Job { Id = "j1" });
        src.JobBoard.AddAvailable(new Job { Id = "j2" });
        src.JobBoard.TryClaim("j2", 3);
        var bytes = SnapshotBuilder.Serialize(src);
        var dst = SnapshotBuilder.Deserialize(bytes);
        dst.JobBoard.Available.Should().ContainSingle(j => j.Id == "j1");
        dst.JobBoard.Claimed.Should().ContainKey("j2");
        dst.JobBoard.Claimed["j2"].ClaimedBySlot.Should().Be(3);
    }
}
