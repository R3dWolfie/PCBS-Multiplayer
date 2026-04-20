using FluentAssertions;
using PCBSMultiplayer.Session;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class GraceTimerTests
{
    [Fact]
    public void Timer_fires_after_duration_elapses()
    {
        var t = new GraceTimer();
        bool fired = false;
        t.Start("client-7", startMs: 0, durationMs: 30000, onElapsed: () => fired = true);
        t.Tick(15000);
        fired.Should().BeFalse();
        t.Tick(30001);
        fired.Should().BeTrue();
    }

    [Fact]
    public void Cancel_prevents_firing()
    {
        var t = new GraceTimer();
        bool fired = false;
        t.Start("client-7", startMs: 0, durationMs: 30000, onElapsed: () => fired = true);
        t.Cancel("client-7");
        t.Tick(60000);
        fired.Should().BeFalse();
    }

    [Fact]
    public void Restart_resets_duration()
    {
        var t = new GraceTimer();
        bool fired = false;
        t.Start("x", startMs: 0, durationMs: 30000, onElapsed: () => fired = true);
        t.Start("x", startMs: 20000, durationMs: 30000, onElapsed: () => fired = true);
        t.Tick(40000);
        fired.Should().BeFalse();
        t.Tick(60000);
        fired.Should().BeTrue();
    }
}
