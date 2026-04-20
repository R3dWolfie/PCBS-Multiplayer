using System;
using System.Collections.Generic;

namespace PCBSMultiplayer.Session;

public sealed class GraceTimer
{
    private readonly Dictionary<string, Entry> _entries = new();

    private sealed class Entry
    {
        public long StartMs;
        public long DurationMs;
        public Action Callback = () => { };
        public bool Fired;
    }

    public void Start(string key, long startMs, long durationMs, Action onElapsed)
    {
        _entries[key] = new Entry { StartMs = startMs, DurationMs = durationMs, Callback = onElapsed, Fired = false };
    }

    public void Cancel(string key) => _entries.Remove(key);

    public void Tick(long nowMs)
    {
        List<string>? toRemove = null;
        foreach (var kvp in _entries)
        {
            var e = kvp.Value;
            if (e.Fired) continue;
            if (nowMs - e.StartMs >= e.DurationMs)
            {
                e.Callback();
                e.Fired = true;
                (toRemove ??= new()).Add(kvp.Key);
            }
        }
        if (toRemove != null) foreach (var k in toRemove) _entries.Remove(k);
    }
}
