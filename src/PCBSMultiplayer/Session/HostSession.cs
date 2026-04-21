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
        Log.LogInfo("OnHello: from " + hello.DisplayName + " (steam=" + hello.SteamId + ", mod=" + hello.ModVersion + ")");
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

    // Delegate seam around CareerStatus. The default returns Usable=false so xUnit tests —
    // which don't ship Assembly-CSharp.dll — never JIT the CareerStatus reference. Inlining
    // CareerStatus.Get() into OnSpendMoney caused JIT-prep to fail before the try/catch ran,
    // and the client's SpendMoneyResult never came back. PCBSMultiplayerPlugin.Awake swaps
    // SpendAuthority to TrySpendViaCareerStatus once BepInEx has loaded Assembly-CSharp.
    public delegate SpendResult TrySpendAuthority(int amount);
    public static TrySpendAuthority SpendAuthority = NoopSpendAuthority;
    private static SpendResult NoopSpendAuthority(int amount) => default;

    public static SpendResult TrySpendViaCareerStatus(int amount)
    {
        try
        {
            var career = CareerStatus.Get();
            if (career == null) return default;
            int cash = career.GetCash();
            if (cash < amount) return new SpendResult { Usable = true, Accepted = false, NewTotal = cash };
            career.SpendCash(amount, true);
            return new SpendResult { Usable = true, Accepted = true, NewTotal = career.GetCash() };
        }
        catch { return default; }
    }

    private void OnSpendMoney(ITransport transport, SpendMoneyRequest req)
    {
        // PCBS's CareerStatus is authoritative — WorldState.Money is a mirror that's never
        // seeded from the loaded save (only patched on post-load AddCash/SpendCash), so the
        // mirror reads 0 at session start and would reject every client purchase. Delegate
        // through SpendAuthority: prod hits CareerStatus, tests get Usable=false and fall
        // through to the mirror with its pre-seeded host.World.Money.
        int amount = (int)req.Amount; // PCBS's CareerStatus.SpendCash takes int; wire type is long.
        var auth = SpendAuthority(amount);
        bool ok;
        if (auth.Usable)
        {
            ok = auth.Accepted;
            if (ok) _mgr.World.Money = auth.NewTotal;
            // No manual broadcast: SpendCashPatch.Postfix triggers BroadcastMoneyChanged.
        }
        else
        {
            ok = _mgr.World.Money >= amount;
            if (ok)
            {
                _mgr.World.Money -= amount;
                var delta = Serializer.Pack(new MoneyChanged { NewTotal = _mgr.World.Money });
                foreach (var t in _transports.Values) t.Send(delta);
            }
        }

        transport.Send(Serializer.Pack(new SpendMoneyResult
        {
            RequestId = req.RequestId,
            Accepted = ok,
            DenyReason = ok ? "" : "insufficient_funds"
        }));
    }

    public void BroadcastMoneyChanged(long newTotal)
    {
        _mgr.World.Money = newTotal;
        var frame = Serializer.Pack(new MoneyChanged { NewTotal = newTotal });
        Log.LogInfo("BroadcastMoneyChanged: total=" + newTotal + " bytes=" + frame.Length + " recipients=" + _transports.Count);
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
        Log.LogInfo("BroadcastJobBoardDelta: available=" + available.Count + " claimed=" + claimed.Count + " completed=" + completed.Count + " bytes=" + frame.Length + " recipients=" + _transports.Count);
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

public struct SpendResult
{
    public bool Usable;   // Authority responded (false = caller should fall back to mirror)
    public bool Accepted; // Spend succeeded
    public long NewTotal; // Authority's post-spend balance (only meaningful when Usable=true && Accepted=true)
}
