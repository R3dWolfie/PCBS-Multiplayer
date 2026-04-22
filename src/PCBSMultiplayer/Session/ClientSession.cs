using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.State;

namespace PCBSMultiplayer.Session;

public sealed class ClientSession
{
    public const string ModVersion = "0.4.0-alpha";

    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("PCBSMultiplayer.ClientSession");

    private readonly SessionManager _mgr;
    public string DisplayName { get; set; } = "";
    public ulong SteamId { get; set; }
    public string DisconnectReason { get; private set; }

    public ClaimJobResult LastClaimResult { get; private set; }
    public SpendMoneyResult LastSpendMoneyResult { get; private set; }

    // Delegate seam for applying a JobBoardDelta to PCBS's CareerStatus. Default is a no-op so
    // xUnit tests — which don't ship Assembly-CSharp.dll — don't JIT the CareerStatus reference.
    // PCBSMultiplayerPlugin.Awake swaps this to ApplyJobDeltaToCareerStatus in prod.
    public delegate void ApplyJobDeltaFn(JobBoardDelta delta);
    public static ApplyJobDeltaFn ApplyJobDelta = NoopApplyJobDelta;
    private static void NoopApplyJobDelta(JobBoardDelta delta) { }

    public static void ApplyJobDeltaToCareerStatus(JobBoardDelta delta)
    {
        var career = CareerStatus.Get();
        if (career == null) return;
        foreach (var dto in delta.Claimed)
        {
            if (!int.TryParse(dto.Id, out var id)) continue;
            var job = career.GetJob(id);
            if (job == null) continue;
            if (job.m_status != global::Job.Status.NEW && job.m_status != global::Job.Status.READ) continue;
            Log.LogInfo("ApplyJobDelta: flipping local job " + id + " NEW/READ -> ACCEPTED");
            SessionManager.ApplyingRemoteDelta = true;
            try { job.OnAccept(_autoAccepting: true); }
            finally { SessionManager.ApplyingRemoteDelta = false; }
        }
    }

    private readonly SaveSyncReassembler _saveSync = new SaveSyncReassembler();
    private string _savesDirAbsolute = "";
    private ulong _lobbyId;

    public event Action<string> SaveReady; // string = save filename including ".binary" (matches host's LoadGameFromDir convention)

    public void ConfigureSaveSync(string savesDirAbsolute, ulong lobbyId)
    {
        _savesDirAbsolute = savesDirAbsolute ?? "";
        _lobbyId = lobbyId;
    }

    public ClientSession(SessionManager mgr)
    {
        _mgr = mgr;
        _mgr.Router.On<Welcome>(OnWelcome);
        _mgr.Router.On<Bye>(OnBye);
        _mgr.Router.On<ClaimJobResult>(r => LastClaimResult = r);
        _mgr.Router.On<JobBoardDelta>(d =>
        {
            Log.LogInfo("OnJobBoardDelta: available=" + d.Available.Count + " claimed=" + d.Claimed.Count + " completed=" + d.Completed.Count);
            DeltaApplier.Apply(_mgr.World, d);
            // Mirror the claim state into PCBS's own CareerStatus so the client's job-board UI
            // actually moves a job from "NEW" to "ACCEPTED" when the host approves. Delegated
            // through ApplyJobDelta so xUnit tests don't JIT the PCBS reference.
            try { ApplyJobDelta(d); }
            catch (Exception ex) { Log.LogError("ApplyJobDelta: " + ex.Message); }
        });
        _mgr.Router.On<SpendMoneyResult>(r => LastSpendMoneyResult = r);
        _mgr.Router.On<MoneyChanged>(OnMoneyChanged);
        _mgr.Router.On<SaveTransferBegin>(OnSaveTransferBegin);
        _mgr.Router.On<SaveChunk>(OnSaveChunk);
        _mgr.Router.On<SaveTransferEnd>(OnSaveTransferEnd);
        _mgr.Router.On<TransformUpdate>(OnTransformUpdate);
    }

    public void SayHello()
    {
        var hello = new Hello
        {
            ModVersion = ModVersion,
            GameVersion = "1.15.2",
            SteamId = SteamId,
            DisplayName = DisplayName
        };
        _mgr.Transport.Send(Serializer.Pack(hello));
    }

    private long _lastHeartbeatSentMs;
    private const long HeartbeatIntervalMs = 1000;

    // Without periodic heartbeats, the host's SessionManager times out the client after
    // TimeoutMs and grace-expires the transport, cutting off host-to-client broadcasts
    // (MoneyChanged, JobBoardDelta, etc.) while leaving client-to-host intact. Spends
    // would still debit the host's money but the client's UI would stay frozen.
    public void MaybeSendHeartbeat(long nowMs)
    {
        if (nowMs - _lastHeartbeatSentMs < HeartbeatIntervalMs) return;
        _lastHeartbeatSentMs = nowMs;
        _mgr.Transport.Send(Serializer.Pack(new Heartbeat { SentAtMs = nowMs }));
    }

    private void OnWelcome(Welcome w)
    {
        // Flip IsLive FIRST. Patches gate on !IsLive; if snapshot apply below throws, every
        // client-side spend/claim would route locally instead of through the host.
        // Plan 3 transfers the whole save anyway, so Welcome snapshot is mostly a redundant
        // baseline — failing to apply it is not a reason to sit disconnected from sync.
        _mgr.LocalSlot = w.AssignedSlot;
        // Register host (slot 0) so the client can render the host's capsule + nameplate
        // once TransformUpdate frames arrive. Lobby-carried host identity is a Plan 6 item.
        _mgr.RemoteRegistry.Register(slot: 0, steamId: 0UL, name: "Host");
        _mgr.IsLive = true;
        Log.LogInfo("OnWelcome: IsLive=true, slot=" + w.AssignedSlot + ", snapshot bytes=" + (w.SnapshotBytes == null ? 0 : w.SnapshotBytes.Length));

        try
        {
            var snapshot = SnapshotBuilder.Deserialize(w.SnapshotBytes);
            _mgr.World.Money = snapshot.Money;
            _mgr.World.XP = snapshot.XP;
            _mgr.World.DayIndex = snapshot.DayIndex;

            var available = new List<SnapshotBuilder.JobDto>();
            foreach (var j in snapshot.JobBoard.Available)
                available.Add(new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot });
            var claimed = new List<SnapshotBuilder.JobDto>();
            foreach (var j in snapshot.JobBoard.Claimed.Values)
                claimed.Add(new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot });
            var completed = new List<SnapshotBuilder.JobDto>();
            foreach (var j in snapshot.JobBoard.Completed)
                completed.Add(new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot });

            _mgr.World.JobBoard.ReplaceAll(available, claimed, completed);
        }
        catch (Exception ex)
        {
            Log.LogWarning("OnWelcome: snapshot apply failed (non-fatal, session already live): " + ex.Message);
        }
    }

    private void OnBye(Bye b)
    {
        DisconnectReason = b.Reason;
        _mgr.IsLive = false;
        _mgr.Transport.Disconnect();
    }

    private void OnMoneyChanged(MoneyChanged d)
    {
        DeltaApplier.Apply(_mgr.World, d);
        var career = CareerStatus.Get();
        if (career == null) { Log.LogWarning("OnMoneyChanged: target=" + d.NewTotal + " but CareerStatus.Get() returned null — cannot apply"); return; }
        int current = career.GetCash();
        int target = (int)d.NewTotal;
        int diff = target - current;
        Log.LogInfo("OnMoneyChanged: target=" + target + " current=" + current + " diff=" + diff);
        if (diff == 0) return;
        SessionManager.ApplyingRemoteDelta = true;
        try
        {
            if (diff > 0) career.AddCash(diff);
            else career.SpendCash(-diff, true);
            Log.LogInfo("OnMoneyChanged: applied, CareerStatus.GetCash() now=" + career.GetCash());
        }
        finally { SessionManager.ApplyingRemoteDelta = false; }
    }

    private int _nextRequestId = 1;

    public void RequestSpend(int amount)
    {
        _mgr.Transport.Send(Serializer.Pack(new SpendMoneyRequest
        {
            RequestId = (_nextRequestId++).ToString(),
            Amount = amount
        }));
    }

    public void RequestClaimJob(string jobId)
    {
        _mgr.Transport.Send(Serializer.Pack(new ClaimJobRequest
        {
            RequestId = (_nextRequestId++).ToString(),
            JobId = jobId
        }));
    }

    private void OnTransformUpdate(TransformUpdate msg)
    {
        // Lazy register with placeholder identity if the peer is unknown. Host doesn't
        // broadcast a PlayerJoined message yet (deferred to Plan 6), so TransformUpdate
        // is the "first sight" signal for client-to-client peers. SteamId stays 0 until
        // a dedicated identity message lands — which means the avatar falls back to
        // the gray placeholder until then.
        if (!_mgr.RemoteRegistry.TryGet(msg.Slot, out _))
        {
            _mgr.RemoteRegistry.Register(msg.Slot, steamId: 0UL, name: "Player " + msg.Slot);
        }
        long now = (long)(UnityEngine.Time.unscaledTime * 1000f);
        _mgr.RemoteRegistry.ApplySample(msg.Slot, msg.PosX, msg.PosY, msg.PosZ, msg.Yaw, msg.Seq, now);
    }

    private void OnSaveTransferBegin(SaveTransferBegin msg)
    {
        _saveSync.Reset();
        _saveSync.OnBegin(msg);
    }

    private void OnSaveChunk(SaveChunk msg)
    {
        _saveSync.OnChunk(msg);
    }

    private void OnSaveTransferEnd(SaveTransferEnd msg)
    {
        byte[] bytes;
        string err;
        bool ok = _saveSync.OnEnd(msg, out bytes, out err);
        if (!ok)
        {
            DisconnectReason = "save_transfer_failed: " + err;
            _mgr.IsLive = false;
            try
            {
                _mgr.Transport.Send(Serializer.Pack(new Bye { Reason = "save_transfer_failed" }));
            }
            catch (Exception ex)
            {
                Log.LogWarning("Send Bye failed (transport likely disconnected): " + ex.Message);
            }
            return;
        }

        if (string.IsNullOrEmpty(_savesDirAbsolute))
        {
            DisconnectReason = "save_write_failed: saves dir not configured";
            _mgr.IsLive = false;
            return;
        }

        // Include ".binary" so LoadGameFromDir(saveName) matches the file on disk.
        // Host calls LoadGameFromDir("auto.binary", ...) verbatim; the client used to pass
        // "mp-<lobbyId>" without the extension, so PCBS couldn't find the file and popped
        // a "Save Game Error" modal referencing the exact extension-less path.
        string saveName = "mp-" + _lobbyId + ".binary";
        string fullPath = Path.Combine(_savesDirAbsolute, saveName);
        try
        {
            File.WriteAllBytes(fullPath, bytes);
        }
        catch (Exception ex)
        {
            DisconnectReason = "save_write_failed: " + ex.Message;
            _mgr.IsLive = false;
            try
            {
                _mgr.Transport.Send(Serializer.Pack(new Bye { Reason = "save_write_failed" }));
            }
            catch (Exception sendEx)
            {
                Log.LogWarning("Send Bye failed (transport likely disconnected): " + sendEx.Message);
            }
            return;
        }

        var handler = SaveReady;
        if (handler != null)
        {
            try { handler(saveName); }
            catch (Exception ex) { Log.LogError("SaveReady subscriber threw: " + ex.Message); }
        }
    }
}
