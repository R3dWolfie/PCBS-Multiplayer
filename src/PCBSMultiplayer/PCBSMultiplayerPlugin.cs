using System;
using BepInEx;
using BepInEx.Configuration;

namespace PCBSMultiplayer;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class PCBSMultiplayerPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.pcbs.multiplayer";
    public const string PluginName = "PCBS Multiplayer";
    public const string PluginVersion = "0.1.0";

    private ConfigEntry<bool>? _enabled;

    private void Awake()
    {
        try
        {
            _enabled = Config.Bind("General", "EnableMultiplayer", true,
                "Master switch. When false, the plugin loads but applies no patches — vanilla PCBS behavior.");

            if (!_enabled.Value)
            {
                Logger.LogInfo("PCBS Multiplayer disabled via config — skipping all hooks.");
                return;
            }

            Logger.LogInfo($"PCBS Multiplayer {PluginVersion} loaded. (Plan 1 — no game patches yet.)");
        }
        catch (Exception ex)
        {
            Logger.LogError($"PCBS Multiplayer init failed: {ex}. Continuing without the mod active.");
        }
    }
}
