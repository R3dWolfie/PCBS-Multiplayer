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

    public IReadOnlyDictionary<int, ClientInfo> Clients => _clients;

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
        if (_clients.Count >= MaxClients) { transport.Send(Serializer.Pack(new Bye { Reason = "lobby_full" })); return; }
        if (hello.ModVersion != ModVersion) { transport.Send(Serializer.Pack(new Bye { Reason = "version_mismatch" })); return; }

        var slot = _nextSlot++;
        _clients[slot] = new ClientInfo { Slot = slot, SteamId = hello.SteamId, DisplayName = hello.DisplayName };
        _transports[slot] = transport;
        _slotByTransport[transport] = slot;

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
        _clients.Remove(slot);
        _transports.Remove(slot);
        _slotByTransport.Remove(transport);
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
