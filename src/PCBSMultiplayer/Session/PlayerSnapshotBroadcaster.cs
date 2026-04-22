using BepInEx.Logging;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using UnityEngine;

namespace PCBSMultiplayer.Session;

public sealed class PlayerSnapshotBroadcaster : MonoBehaviour
{
    private static readonly ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("PCBSMultiplayer.Broadcaster");

    private readonly BroadcasterTick _tick = new BroadcasterTick(periodMs: 50f);
    private Transform _localCam;
    private bool _loggedFirstSend;

    private Transform ResolveLocalCam()
    {
        if (_localCam != null) return _localCam;
        // Recon verdict in P5-T1 picked Camera.main as the canonical path. If recon picked
        // a different approach, swap this line. DO NOT add a fallback — we want obvious
        // failures when PCBS changes structure, not silent broadcasting from the wrong rig.
        var c = Camera.main;
        if (c == null) return null;
        _localCam = c.transform;
        return _localCam;
    }

    private void Update()
    {
        var mgr = SessionManager.Current;
        if (mgr == null || !mgr.IsLive) { _tick.Reset(); return; }

        var cam = ResolveLocalCam();
        if (cam == null) return;

        int sends = _tick.Advance(Time.unscaledDeltaTime * 1000f, out var seq);
        if (sends == 0) return;

        // Build + send exactly one frame per Advance tick, regardless of how many whole
        // periods the timer accumulated. A frame-hitch that would produce 2+ sends can
        // collapse to the latest pose — there's no point broadcasting stale positions.
        var pos = cam.position;
        float yaw = cam.eulerAngles.y;
        var msg = new TransformUpdate
        {
            Slot = (byte)mgr.LocalSlot,
            PosX = pos.x, PosY = pos.y, PosZ = pos.z,
            Yaw = yaw,
            Seq = seq
        };
        byte[] framed = Serializer.Pack(msg);

        if (mgr.Role == SessionRole.Host)
        {
            // Host fans out directly to every client transport.
            foreach (var t in mgr.Host.Transports)
            {
                try { t.SendUnreliable(framed, channel: 1); }
                catch (System.Exception ex) { Log.LogWarning("SendUnreliable to client failed: " + ex.Message); }
            }
        }
        else
        {
            try { mgr.Transport.SendUnreliable(framed, channel: 1); }
            catch (System.Exception ex) { Log.LogWarning("SendUnreliable to host failed: " + ex.Message); }
        }

        if (!_loggedFirstSend)
        {
            Log.LogInfo("Broadcasting at 20 Hz (first send): slot=" + msg.Slot + " seq=" + seq);
            _loggedFirstSend = true;
        }
    }
}
