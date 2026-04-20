using FluentAssertions;
using PCBSMultiplayer.UI;
using Xunit;

namespace PCBSMultiplayer.Tests.UI;

public class PanicHotkeyTrackerTests
{
    [Fact]
    public void Fires_after_hold_duration_elapses()
    {
        var t = new PanicHotkeyTracker(holdMs: 3000);
        t.Update(isDown: true, nowMs: 0);    t.Fired.Should().BeFalse();
        t.Update(isDown: true, nowMs: 1500); t.Fired.Should().BeFalse();
        t.Update(isDown: true, nowMs: 3000); t.Fired.Should().BeTrue();
    }

    [Fact]
    public void Release_resets_timer()
    {
        var t = new PanicHotkeyTracker(holdMs: 3000);
        t.Update(true, 0);
        t.Update(true, 2900);
        t.Update(false, 2950);
        t.Update(true, 3000);
        t.Update(true, 3100);
        t.Fired.Should().BeFalse();
    }

    [Fact]
    public void OnFired_is_invoked_once_while_held()
    {
        int count = 0;
        var t = new PanicHotkeyTracker(holdMs: 1000);
        t.OnFired = () => count++;
        t.Update(true, 0);
        t.Update(true, 1000);
        t.Update(true, 1500);
        t.Update(true, 2000);
        count.Should().Be(1);
    }

    [Fact]
    public void Progress01_reports_hold_ratio()
    {
        var t = new PanicHotkeyTracker(holdMs: 2000);
        t.Update(true, 0);
        t.Update(true, 1000);
        t.Progress01.Should().BeApproximately(0.5f, 0.01f);
    }
}
