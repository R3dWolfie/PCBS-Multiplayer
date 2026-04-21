using System.Collections.Generic;

namespace PCBSMultiplayer.State;

public sealed class RemotePlayerRegistry
{
    private readonly Dictionary<int, RemotePlayer> _bySlot = new();

    public IEnumerable<RemotePlayer> All() => _bySlot.Values;

    public bool TryGet(int slot, out RemotePlayer player) => _bySlot.TryGetValue(slot, out player);

    public void Register(int slot, ulong steamId, string name)
    {
        if (_bySlot.TryGetValue(slot, out var existing))
        {
            existing.SteamId = steamId;
            existing.DisplayName = name ?? "";
            return;
        }
        _bySlot[slot] = new RemotePlayer
        {
            Slot = slot,
            SteamId = steamId,
            DisplayName = name ?? ""
        };
    }

    public void ApplySample(int slot, float posX, float posY, float posZ, float yaw, uint seq, long nowMs)
    {
        if (!_bySlot.TryGetValue(slot, out var p)) return;
        // Update LastSeenMs BEFORE the seq-drop: a replayed/duplicate packet still
        // proves the peer is alive, so PruneStale should not evict them.
        p.LastSeenMs = nowMs;
        if (seq <= p.LastSeq) return;  // out-of-order or replay — drop sample, keep liveness
        p.PrevPosX = p.PosX; p.PrevPosY = p.PosY; p.PrevPosZ = p.PosZ;
        p.PrevYaw = p.Yaw;
        p.PosX = posX; p.PosY = posY; p.PosZ = posZ;
        p.Yaw = yaw;
        p.LastSeq = seq;
        p.LastSampleMs = nowMs;
    }

    public void Remove(int slot) => _bySlot.Remove(slot);

    public void PruneStale(long nowMs, long staleMs)
    {
        List<int> toDelete = null;
        foreach (var kv in _bySlot)
        {
            if (nowMs - kv.Value.LastSeenMs > staleMs)
            {
                toDelete ??= new List<int>();
                toDelete.Add(kv.Key);
            }
        }
        if (toDelete == null) return;
        foreach (var slot in toDelete) _bySlot.Remove(slot);
    }

    public int Count => _bySlot.Count;
    public void Clear() => _bySlot.Clear();
}
