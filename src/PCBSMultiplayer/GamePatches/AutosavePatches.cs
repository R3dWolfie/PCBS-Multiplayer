using BepInEx.Logging;
using HarmonyLib;
using PCBSMultiplayer.Session;

namespace PCBSMultiplayer.GamePatches;

[HarmonyPatch(typeof(SaveLoadSystem), nameof(SaveLoadSystem.AutoSave))]
public static class AutosavePatches
{
    private static ManualLogSource _log;
    private static ManualLogSource Log
    {
        get { return _log != null ? _log : (_log = BepInEx.Logging.Logger.CreateLogSource("PCBSMultiplayer.Autosave")); }
    }

    public static bool ShouldSuppress()
    {
        var cur = SessionManager.Current;
        if (cur == null) return false;
        return cur.Role == SessionRole.Client;
    }

    [HarmonyPrefix]
    public static bool Prefix()
    {
        if (!ShouldSuppress()) return true; // run original
        Log.LogInfo("Autosave suppressed — client in MP session.");
        return false; // skip original
    }
}
