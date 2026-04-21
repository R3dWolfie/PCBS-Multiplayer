using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.State;

namespace PCBSMultiplayer.Session;

public sealed class HostSession
{
    public const string ModVersion = "0.1.0";
    public const int MaxClients = 3;

    private static readonly ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("PCBSMultiplayer.HostSession");

    private readonly SessionManager _mgr;
    private readonly Dictionary<int, ClientInfo> _clients = new();
    private readonly Dictionary<int, ITransport> _transports = new();
    private readonly Dictionary<ITransport, int> _slotByTransport = new();
    private int _nextSlot = 1;
    private readonly GraceTimer _grace = new();
    private readonly HashSet<int> _inGrace = new();
    private const long GraceMs = 30000;
    private long _lastHeartbeatMs;

    public Dictionary<int, ClientInfo> Clients => _clients;
    public GraceTimer GraceTimer => _grace;
    public ICollection<ITransport> Transports => _transports.Values;

    public event Action<int> ClientAccepted;

    internal void AttachExistingClient(int slot, ITransport transport)
    {
        _clients[slot] = new ClientInfo { Slot = slot };
        _transports[slot] = transport;
        _slotByTransport[transport] = slot;
    }

    internal void TickGrace(long nowMs) => _grace.Tick(nowMs);
    internal void SetLastHeartbeat(long nowMs) => _lastHeartbeatMs = nowMs;

    public HostSession(SessionManager mgr)
    {
        _mgr = mgr;
        _mgr.LocalSlot = 0;
        _mgr.IsLive = true;
    }

    public void AttachClient(ITransport transport)
    {
        var router = new MessageRouter();
        router.On<Hello>(h => OnHello(transport, h));
        router.On<ClaimJobRequest>(r => OnClaimJob(transport, r));
        router.On<SpendMoneyRequest>(r => OnSpendMoney(transport, r));
        _mgr.AttachClientTransport(transport, router);
    }

    private void OnHello(ITransport transport, Hello hello)
    {
        if (hello.ModVersion != ModVersion) { transport.Send(Serializer.Pack(new Bye { Reason = "version_mismatch" })); return; }

        int slot = -1;
        foreach (var kv in _clients)
            if (kv.Value.SteamId == hello.SteamId && hello.SteamId != 0 && _inGrace.Contains(kv.Key))
            { slot = kv.Key; break; }

        if (slot != -1)
        {
            _grace.Cancel($"client-{slot}");
            _inGrace.Remove(slot);
            if (_transports.TryGetValue(slot, out var oldT)) _slotByTransport.Remove(oldT);
            _transports[slot] = transport;
            _slotByTransport[transport] = slot;
            _clients[slot].DisplayName = hello.DisplayName;
        }
        else
        {
            if (_clients.Count >= MaxClients) { transport.Send(Serializer.Pack(new Bye { Reason = "lobby_full" })); return; }
            slot = _nextSlot++;
            _clients[slot] = new ClientInfo { Slot = slot, SteamId = hello.SteamId, DisplayName = hello.DisplayName };
            _transports[slot] = transport;
            _slotByTransport[transport] = slot;
        }

        var snapshot = SnapshotBuilder.Serialize(_mgr.World);
        transport.Send(Serializer.Pack(new Welcome { AssignedSlot = slot, SnapshotBytes = snapshot }));
        var handler = ClientAccepted;
        if (handler != null)
        {
            try { handler(slot); }
            catch (Exception ex)
            {
                Log.LogError("ClientAccepted subscriber threw: " + ex.Message);
            }
        }
    }

    private void OnClaimJob(ITransport transport, ClaimJobRequest req)
    {
        if (!_slotByTransport.TryGetValue(transport, out var slot)) return;
        var ok = _mgr.World.JobBoard.TryClaim(req.JobId, slot);
        transport.Send(Serializer.Pack(new ClaimJobResult
        {
            RequestId = req.RequestId,
            Accepted = ok,
            DenyReason = ok ? "" : "already_claimed_or_missing"
        }));
        if (ok) BroadcastJobBoardDelta();
    }

    internal void RemoveClient(ITransport transport)
    {
        if (!_slotByTransport.TryGetValue(transport, out var slot)) return;
        if (!_inGrace.Add(slot)) return;
        _grace.Start($"client-{slot}", startMs: _lastHeartbeatMs, durationMs: GraceMs, onElapsed: () =>
        {
            var claimed = new List<string>();
            foreach (var kv in _mgr.World.JobBoard.Claimed)
                if (kv.Value.ClaimedBySlot == slot) claimed.Add(kv.Key);
            foreach (var id in claimed) _mgr.World.JobBoard.Release(id);
            _clients.Remove(slot);
            _transports.Remove(slot);
            _slotByTransport.Remove(transport);
            _inGrace.Remove(slot);
            BroadcastJobBoardDelta();
        });
    }

    private void OnSpendMoney(ITransport transport, SpendMoneyRequest req)
    {
        var ok = _mgr.World.Money >= req.Amount;
        if (ok) _mgr.World.Money -= req.Amount;
        transport.Send(Serializer.Pack(new SpendMoneyResult
        {
            RequestId = req.RequestId,
            Accepted = ok,
            DenyReason = ok ? "" : "insufficient_funds"
        }));
        if (ok)
        {
            var delta = Serializer.Pack(new MoneyChanged { NewTotal = _mgr.World.Money });
            foreach (var t in _transports.Values) t.Send(delta);
        }
    }

    public void BroadcastMoneyChanged(long newTotal)
    {
        _mgr.World.Money = newTotal;
        var frame = Serializer.Pack(new MoneyChanged { NewTotal = newTotal });
        foreach (var t in _transports.Values) t.Send(frame);
    }

    public void BroadcastJobBoardDelta()
    {
        var available = new List<SnapshotBuilder.JobDto>();
        foreach (var j in _mgr.World.JobBoard.Available)
            available.Add(new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot });
        var claimed = new List<SnapshotBuilder.JobDto>();
        foreach (var j in _mgr.World.JobBoard.Claimed.Values)
            claimed.Add(new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot });
        var completed = new List<SnapshotBuilder.JobDto>();
        foreach (var j in _mgr.World.JobBoard.Completed)
            completed.Add(new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot });

        var delta = new JobBoardDelta { Available = available, Claimed = claimed, Completed = completed };
        var frame = Serializer.Pack(delta);
        foreach (var t in _transports.Values) t.Send(frame);
    }

    public bool BeginSaveTransfer(string saveName, string sceneName, string savesDirAbsolute, out string err)
    {
        err = null;
        // PCBS's SaveFileInfo.Name already includes the .binary extension — don't double-append.
        string bareName = saveName.EndsWith(".binary", StringComparison.OrdinalIgnoreCase)
            ? saveName.Substring(0, saveName.Length - ".binary".Length)
            : saveName;
        string fileName = bareName + ".binary";
        string fullPath = Path.Combine(savesDirAbsolute, fileName);

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(fullPath);
        }
        catch (Exception ex)
        {
            err = "save read failed at " + fullPath + ": " + ex.Message;
            return false;
        }

        var packer = new SaveSyncPacker(bytes, saveName, sceneName);

        Log.LogInfo("BeginSaveTransfer: save=\"" + saveName + "\" bytes=" + bytes.Length + " chunks=" + packer.Chunks.Count);

        var beginFrame = Serializer.Pack(packer.Begin);
        foreach (var kv in _transports)
        {
            try { kv.Value.Send(beginFrame); }
            catch (Exception ex) { Log.LogWarning("Send begin to slot " + kv.Key + " failed: " + ex.Message); }
        }

        for (int i = 0; i < packer.Chunks.Count; i++)
        {
            var frame = Serializer.Pack(packer.Chunks[i]);
            foreach (var kv in _transports)
            {
                try { kv.Value.Send(frame); }
                catch (Exception ex) { Log.LogWarning("Send chunk " + i + " to slot " + kv.Key + " failed: " + ex.Message); }
            }
        }

        var endFrame = Serializer.Pack(packer.End);
        foreach (var kv in _transports)
        {
            try { kv.Value.Send(endFrame); }
            catch (Exception ex) { Log.LogWarning("Send end to slot " + kv.Key + " failed: " + ex.Message); }
        }

        Log.LogInfo("BeginSaveTransfer: complete");
        return true;
    }
}

public sealed class ClientInfo
{
    public int Slot { get; set; }
    public ulong SteamId { get; set; }
    public string DisplayName { get; set; } = "";
}
