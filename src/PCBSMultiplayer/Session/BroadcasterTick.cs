namespace PCBSMultiplayer.Session;

// Pure rate-limiter extracted from PlayerSnapshotBroadcaster.Update for testability.
// Caller feeds deltaMs every frame; Advance returns the number of sends this frame
// (usually 0, occasionally 1, occasionally 2 on a spike).
public sealed class BroadcasterTick
{
    private readonly float _periodMs;
    private uint _seq;

    public float AccumMs { get; private set; }
    public uint Seq => _seq;

    public BroadcasterTick(float periodMs)
    {
        _periodMs = periodMs;
    }

    public void Reset()
    {
        AccumMs = 0f;
        // NOTE: do NOT reset _seq — a reconnect should pick up where we left off
        // so stale samples from a dropped session get seq-dropped on the remote
        // (the peer's RemotePlayerRegistry still has the old LastSeq).
    }

    public int Advance(float deltaMs, out uint lastSeq)
    {
        AccumMs += deltaMs;
        int sends = 0;
        while (AccumMs >= _periodMs)
        {
            AccumMs -= _periodMs;
            _seq++;
            sends++;
        }
        lastSeq = _seq;
        return sends;
    }
}
