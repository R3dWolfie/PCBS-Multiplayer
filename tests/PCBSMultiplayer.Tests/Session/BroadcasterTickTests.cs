using PCBSMultiplayer.Session;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public sealed class BroadcasterTickTests
{
    [Fact]
    public void Accum_below_period_produces_zero_sends()
    {
        var t = new BroadcasterTick(periodMs: 50f);
        int sends = t.Advance(deltaMs: 49f, out _);
        Assert.Equal(0, sends);
    }

    [Fact]
    public void Accum_at_period_produces_one_send()
    {
        var t = new BroadcasterTick(periodMs: 50f);
        int sends = t.Advance(50f, out var seq);
        Assert.Equal(1, sends);
        Assert.Equal(1u, seq);
    }

    [Fact]
    public void Accum_past_double_period_produces_two_sends_and_keeps_remainder()
    {
        var t = new BroadcasterTick(50f);
        int sends = t.Advance(120f, out var seq);
        Assert.Equal(2, sends);
        Assert.Equal(2u, seq);          // last-sent seq
        Assert.Equal(20f, t.AccumMs);   // 120 - 2*50 = 20 left over
    }

    [Fact]
    public void Seq_monotonic_across_calls()
    {
        var t = new BroadcasterTick(50f);
        t.Advance(50f, out var s1);
        t.Advance(50f, out var s2);
        t.Advance(50f, out var s3);
        Assert.Equal(1u, s1);
        Assert.Equal(2u, s2);
        Assert.Equal(3u, s3);
    }
}
