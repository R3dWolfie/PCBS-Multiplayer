using System;
using BepInEx.Logging;
using HarmonyLib;
using PCBSMultiplayer.Session;

namespace PCBSMultiplayer.GamePatches;

internal static class MoneyPatchLog
{
    internal static readonly ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("PCBSMultiplayer.MoneyPatches");
}

[HarmonyPatch(typeof(CareerStatus), nameof(CareerStatus.SpendCash))]
public static class SpendCashPatch
{
    public static bool Prefix(int cash, bool force, ref bool __result)
    {
        try
        {
            if (SessionManager.ApplyingRemoteDelta) return true;
            var mgr = SessionManager.Current;
            if (mgr == null || !mgr.IsLive) return true;
            if (mgr.Role == SessionRole.Host) return true;
            mgr.Client.RequestSpend(cash);
            __result = false;
            return false;
        }
        catch (Exception ex) { MoneyPatchLog.Log.LogError($"SpendCash prefix: {ex}"); return true; }
    }

    public static void Postfix(int cash, bool force, bool __result)
    {
        try
        {
            if (SessionManager.ApplyingRemoteDelta) { MoneyPatchLog.Log.LogInfo("SpendCash postfix skipped: ApplyingRemoteDelta"); return; }
            var mgr = SessionManager.Current;
            MoneyPatchLog.Log.LogInfo("SpendCash postfix: cash=" + cash + " force=" + force + " result=" + __result
                + " mgr=" + (mgr == null ? "null" : ("Role=" + mgr.Role + " IsLive=" + mgr.IsLive)));
            if (mgr == null || !mgr.IsLive || mgr.Role != SessionRole.Host) return;
            if (!__result) return;
            int total = CareerStatus.Get().GetCash();
            MoneyPatchLog.Log.LogInfo("SpendCash postfix: broadcasting MoneyChanged total=" + total);
            mgr.Host.BroadcastMoneyChanged(total);
        }
        catch (Exception ex) { MoneyPatchLog.Log.LogError($"SpendCash postfix: {ex}"); }
    }
}

[HarmonyPatch(typeof(CareerStatus), nameof(CareerStatus.AddCash))]
public static class AddCashPatch
{
    public static void Postfix(int amount)
    {
        try
        {
            if (SessionManager.ApplyingRemoteDelta) { MoneyPatchLog.Log.LogInfo("AddCash postfix skipped: ApplyingRemoteDelta"); return; }
            var mgr = SessionManager.Current;
            MoneyPatchLog.Log.LogInfo("AddCash postfix: amount=" + amount
                + " mgr=" + (mgr == null ? "null" : ("Role=" + mgr.Role + " IsLive=" + mgr.IsLive)));
            if (mgr == null || !mgr.IsLive || mgr.Role != SessionRole.Host) return;
            int total = CareerStatus.Get().GetCash();
            MoneyPatchLog.Log.LogInfo("AddCash postfix: broadcasting MoneyChanged total=" + total);
            mgr.Host.BroadcastMoneyChanged(total);
        }
        catch (Exception ex) { MoneyPatchLog.Log.LogError($"AddCash postfix: {ex}"); }
    }
}
