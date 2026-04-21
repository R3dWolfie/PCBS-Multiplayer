using PCBSMultiplayer.State;
using Xunit;

namespace PCBSMultiplayer.Tests.State;

public sealed class RemotePlayerRegistryTests
{
    [Fact]
    public void Register_creates_entry_with_identity_and_empty_sample()
    {
        var r = new RemotePlayerRegistry();
        r.Register(slot: 2, steamId: 76561198000000001UL, name: "arl");
        Assert.True(r.TryGet(2, out var p));
        Assert.Equal(2, p.Slot);
        Assert.Equal(76561198000000001UL, p.SteamId);
        Assert.Equal("arl", p.DisplayName);
        Assert.Equal(0f, p.PosX);
        Assert.Equal(0u, p.LastSeq);
    }

    [Fact]
    public void ApplySample_with_increasing_seq_updates_state()
    {
        var r = new RemotePlayerRegistry();
        r.Register(1, 0UL, "");
        r.ApplySample(1, posX: 10f, posY: 20f, posZ: 30f, yaw: 45f, seq: 1u, nowMs: 100L);
        Assert.True(r.TryGet(1, out var p));
        Assert.Equal(10f, p.PosX);
        Assert.Equal(20f, p.PosY);
        Assert.Equal(30f, p.PosZ);
        Assert.Equal(45f, p.Yaw);
        Assert.Equal(1u, p.LastSeq);
        Assert.Equal(100L, p.LastSampleMs);
        Assert.Equal(100L, p.LastSeenMs);
    }

    [Fact]
    public void ApplySample_with_older_seq_is_noop()
    {
        var r = new RemotePlayerRegistry();
        r.Register(1, 0UL, "");
        r.ApplySample(1, 10f, 0f, 0f, 0f, 5u, 100L);
        r.ApplySample(1, 999f, 0f, 0f, 0f, 3u, 200L); // older seq
        Assert.True(r.TryGet(1, out var p));
        Assert.Equal(10f, p.PosX);        // unchanged
        Assert.Equal(5u, p.LastSeq);      // unchanged
        Assert.Equal(100L, p.LastSampleMs); // unchanged
    }

    [Fact]
    public void Remove_deletes_only_the_named_slot()
    {
        var r = new RemotePlayerRegistry();
        r.Register(1, 0UL, "a");
        r.Register(2, 0UL, "b");
        r.Remove(1);
        Assert.False(r.TryGet(1, out _));
        Assert.True(r.TryGet(2, out _));
    }

    [Fact]
    public void PruneStale_removes_entries_past_threshold_only()
    {
        var r = new RemotePlayerRegistry();
        r.Register(1, 0UL, "");
        r.ApplySample(1, 0f, 0f, 0f, 0f, 1u, 100L);
        r.Register(2, 0UL, "");
        r.ApplySample(2, 0f, 0f, 0f, 0f, 1u, 400L);
        r.PruneStale(nowMs: 500L, staleMs: 500L);   // slot 1: 400ms old — under
        Assert.True(r.TryGet(1, out _));
        Assert.True(r.TryGet(2, out _));
        r.PruneStale(nowMs: 700L, staleMs: 500L);   // slot 1: 600ms old — OVER; slot 2: 300ms — under
        Assert.False(r.TryGet(1, out _));
        Assert.True(r.TryGet(2, out _));
    }
}
