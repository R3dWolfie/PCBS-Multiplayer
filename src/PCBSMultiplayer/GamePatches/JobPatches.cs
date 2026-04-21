using System;
using BepInEx.Logging;
using HarmonyLib;
using PCBSMultiplayer.Session;

namespace PCBSMultiplayer.GamePatches;

internal static class JobPatchLog
{
    internal static readonly ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("PCBSMultiplayer.JobPatches");
}

[HarmonyPatch(typeof(CareerStatus), nameof(CareerStatus.AddJob))]
public static class AddJobPatch
{
    public static void Postfix(Job job)
    {
        try
        {
            if (SessionManager.ApplyingRemoteDelta) { JobPatchLog.Log.LogInfo("AddJob postfix skipped: ApplyingRemoteDelta"); return; }
            var mgr = SessionManager.Current;
            string jobId = job == null ? "<null>" : job.GetId().ToString();
            JobPatchLog.Log.LogInfo("AddJob postfix: jobId=" + jobId
                + " mgr=" + (mgr == null ? "null" : ("Role=" + mgr.Role + " IsLive=" + mgr.IsLive)));
            if (mgr == null || !mgr.IsLive || mgr.Role != SessionRole.Host) return;
            mgr.World.JobBoard.AddAvailable(new PCBSMultiplayer.State.Job
            {
                Id = jobId,
                ClaimedBySlot = -1
            });
            JobPatchLog.Log.LogInfo("AddJob postfix: broadcasting JobBoardDelta");
            mgr.Host.BroadcastJobBoardDelta();
        }
        catch (Exception ex) { JobPatchLog.Log.LogError($"AddJob postfix: {ex}"); }
    }
}

[HarmonyPatch(typeof(CareerStatus), "AddJobForDLC2")]
public static class AddJobForDLC2Patch
{
    public static void Postfix(Job job) => AddJobPatch.Postfix(job);
}

[HarmonyPatch(typeof(Job), nameof(Job.OnAccept))]
public static class OnAcceptPatch
{
    public static bool Prefix(Job __instance)
    {
        try
        {
            if (SessionManager.ApplyingRemoteDelta) return true;
            var mgr = SessionManager.Current;
            if (mgr == null || !mgr.IsLive) return true;
            if (mgr.Role == SessionRole.Host) return true;
            mgr.Client.RequestClaimJob(__instance.GetId().ToString());
            return false;
        }
        catch (Exception ex) { JobPatchLog.Log.LogError($"OnAccept prefix: {ex}"); return true; }
    }

    public static void Postfix(Job __instance)
    {
        try
        {
            if (SessionManager.ApplyingRemoteDelta) { JobPatchLog.Log.LogInfo("OnAccept postfix skipped: ApplyingRemoteDelta"); return; }
            var mgr = SessionManager.Current;
            string jobId = __instance == null ? "<null>" : __instance.GetId().ToString();
            JobPatchLog.Log.LogInfo("OnAccept postfix: jobId=" + jobId
                + " mgr=" + (mgr == null ? "null" : ("Role=" + mgr.Role + " IsLive=" + mgr.IsLive)));
            if (mgr == null || !mgr.IsLive || mgr.Role != SessionRole.Host) return;
            mgr.World.JobBoard.TryClaim(jobId, mgr.LocalSlot);
            JobPatchLog.Log.LogInfo("OnAccept postfix: broadcasting JobBoardDelta");
            mgr.Host.BroadcastJobBoardDelta();
        }
        catch (Exception ex) { JobPatchLog.Log.LogError($"OnAccept postfix: {ex}"); }
    }
}

[HarmonyPatch(typeof(Job), nameof(Job.OnCollected))]
public static class OnCollectedPatch
{
    public static void Postfix(Job __instance)
    {
        try
        {
            if (SessionManager.ApplyingRemoteDelta) { JobPatchLog.Log.LogInfo("OnCollected postfix skipped: ApplyingRemoteDelta"); return; }
            var mgr = SessionManager.Current;
            string jobId = __instance == null ? "<null>" : __instance.GetId().ToString();
            JobPatchLog.Log.LogInfo("OnCollected postfix: jobId=" + jobId
                + " mgr=" + (mgr == null ? "null" : ("Role=" + mgr.Role + " IsLive=" + mgr.IsLive)));
            if (mgr == null || !mgr.IsLive || mgr.Role != SessionRole.Host) return;
            mgr.World.JobBoard.Complete(jobId);
            JobPatchLog.Log.LogInfo("OnCollected postfix: broadcasting JobBoardDelta");
            mgr.Host.BroadcastJobBoardDelta();
        }
        catch (Exception ex) { JobPatchLog.Log.LogError($"OnCollected postfix: {ex}"); }
    }
}

[HarmonyPatch(typeof(Job), nameof(Job.OnQuit))]
public static class OnQuitPatch
{
    public static void Postfix(Job __instance)
    {
        try
        {
            if (SessionManager.ApplyingRemoteDelta) { JobPatchLog.Log.LogInfo("OnQuit postfix skipped: ApplyingRemoteDelta"); return; }
            var mgr = SessionManager.Current;
            string jobId = __instance == null ? "<null>" : __instance.GetId().ToString();
            JobPatchLog.Log.LogInfo("OnQuit postfix: jobId=" + jobId
                + " mgr=" + (mgr == null ? "null" : ("Role=" + mgr.Role + " IsLive=" + mgr.IsLive)));
            if (mgr == null || !mgr.IsLive || mgr.Role != SessionRole.Host) return;
            mgr.World.JobBoard.Release(jobId);
            JobPatchLog.Log.LogInfo("OnQuit postfix: broadcasting JobBoardDelta");
            mgr.Host.BroadcastJobBoardDelta();
        }
        catch (Exception ex) { JobPatchLog.Log.LogError($"OnQuit postfix: {ex}"); }
    }
}

[HarmonyPatch(typeof(Job), nameof(Job.OnDiscard))]
public static class OnDiscardPatch
{
    public static void Postfix(Job __instance) => OnQuitPatch.Postfix(__instance);
}
