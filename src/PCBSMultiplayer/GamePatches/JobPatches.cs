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
            if (SessionManager.ApplyingRemoteDelta) return;
            var mgr = SessionManager.Current;
            if (mgr == null || !mgr.IsLive || mgr.Role != SessionRole.Host) return;
            mgr.World.JobBoard.AddAvailable(new PCBSMultiplayer.State.Job
            {
                Id = job.GetId().ToString(),
                ClaimedBySlot = -1
            });
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
            if (SessionManager.ApplyingRemoteDelta) return;
            var mgr = SessionManager.Current;
            if (mgr == null || !mgr.IsLive || mgr.Role != SessionRole.Host) return;
            mgr.World.JobBoard.TryClaim(__instance.GetId().ToString(), mgr.LocalSlot);
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
            if (SessionManager.ApplyingRemoteDelta) return;
            var mgr = SessionManager.Current;
            if (mgr == null || !mgr.IsLive || mgr.Role != SessionRole.Host) return;
            mgr.World.JobBoard.Complete(__instance.GetId().ToString());
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
            if (SessionManager.ApplyingRemoteDelta) return;
            var mgr = SessionManager.Current;
            if (mgr == null || !mgr.IsLive || mgr.Role != SessionRole.Host) return;
            mgr.World.JobBoard.Release(__instance.GetId().ToString());
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
