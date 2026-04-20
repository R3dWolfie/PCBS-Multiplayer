using System.Collections.Generic;
using System.Linq;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.State;

namespace PCBSMultiplayer.Session;

public sealed class HostSession
{
    public const string ModVersion = "0.1.0";
    public const int MaxClients = 3;

    private readonly SessionManager _mgr;
    private readonly Dictionary<int, ClientInfo> _clients = new();
    private readonly Dictionary<int, ITransport> _transports = new();
    private readonly Dictionary<ITransport, int> _slotByTransport = new();
    private int _nextSlot = 1;
    private readonly GraceTimer _grace = new();
    private readonly HashSet<int> _inGrace = new();
    private const long GraceMs = 30000;
    private long _lastHeartbeatMs;

    public IReadOnlyDictionary<int, ClientInfo> Clients => _clients;
    public GraceTimer GraceTimer => _grace;

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

    private void BroadcastJobBoardDelta()
    {
        var delta = new JobBoardDelta
        {
            Available = _mgr.World.JobBoard.Available.Select(j => new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot }).ToList(),
            Claimed = _mgr.World.JobBoard.Claimed.Values.Select(j => new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot }).ToList(),
            Completed = _mgr.World.JobBoard.Completed.Select(j => new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot }).ToList()
        };
        var frame = Serializer.Pack(delta);
        foreach (var t in _transports.Values) t.Send(frame);
    }
}

public sealed class ClientInfo
{
    public int Slot { get; set; }
    public ulong SteamId { get; set; }
    public string DisplayName { get; set; } = "";
}
