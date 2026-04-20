using FluentAssertions;
using PCBSMultiplayer.State;
using Xunit;

namespace PCBSMultiplayer.Tests.State;

public class JobBoardStateTests
{
    [Fact]
    public void New_board_is_empty()
    {
        var b = new JobBoardState();
        b.Available.Should().BeEmpty();
        b.Claimed.Should().BeEmpty();
        b.Completed.Should().BeEmpty();
    }

    [Fact]
    public void AddAvailable_then_claim_moves_from_available_to_claimed()
    {
        var b = new JobBoardState();
        b.AddAvailable(new Job { Id = "job-1" });
        b.TryClaim("job-1", slot: 2).Should().BeTrue();
        b.Available.Should().BeEmpty();
        b.Claimed.Should().ContainKey("job-1");
        b.Claimed["job-1"].ClaimedBySlot.Should().Be(2);
    }

    [Fact]
    public void TryClaim_returns_false_when_already_claimed()
    {
        var b = new JobBoardState();
        b.AddAvailable(new Job { Id = "job-1" });
        b.TryClaim("job-1", slot: 2).Should().BeTrue();
        b.TryClaim("job-1", slot: 3).Should().BeFalse();
        b.Claimed["job-1"].ClaimedBySlot.Should().Be(2);
    }

    [Fact]
    public void TryClaim_returns_false_for_missing_job()
    {
        var b = new JobBoardState();
        b.TryClaim("nope", slot: 1).Should().BeFalse();
    }

    [Fact]
    public void Release_moves_claimed_back_to_available()
    {
        var b = new JobBoardState();
        b.AddAvailable(new Job { Id = "job-1" });
        b.TryClaim("job-1", slot: 2);
        b.Release("job-1");
        b.Claimed.Should().BeEmpty();
        b.Available.Should().ContainSingle(j => j.Id == "job-1");
    }

    [Fact]
    public void Complete_moves_claimed_to_completed()
    {
        var b = new JobBoardState();
        b.AddAvailable(new Job { Id = "job-1" });
        b.TryClaim("job-1", slot: 2);
        b.Complete("job-1");
        b.Claimed.Should().BeEmpty();
        b.Completed.Should().ContainSingle(j => j.Id == "job-1");
    }
}
