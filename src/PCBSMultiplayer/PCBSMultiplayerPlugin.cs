using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Session;
using Steamworks;
using UnityEngine;

namespace PCBSMultiplayer;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class PCBSMultiplayerPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.pcbs.multiplayer";
    public const string PluginName = "PCBS Multiplayer";
    public const string PluginVersion = "0.3.0.0";

    public static PCBSMultiplayerPlugin Instance { get; private set; }

    private ConfigEntry<bool> _enabled;
    private ConfigEntry<uint> _steamAppId;
    private Harmony _harmony;
    private bool _steamInitialized;

    private void Awake()
    {
        Instance = this;
        try
        {
            _enabled = Config.Bind("General", "EnableMultiplayer", true,
                "Master switch. When false, the plugin loads but applies no patches — vanilla PCBS behavior.");
            _steamAppId = Config.Bind("General", "SteamAppId", 621060u,
                "PCBS 1's Steam AppID. Rebind only if forking against a different game.");

            if (!_enabled.Value)
            {
                Logger.LogInfo("PCBS Multiplayer disabled via config — skipping all hooks.");
                return;
            }

            if (!SteamAPI.IsSteamRunning())
            {
                Logger.LogWarning("Steam is not running — multiplayer disabled for this session.");
                return;
            }
            if (!SteamAPI.Init())
            {
                Logger.LogError("SteamAPI.Init failed — multiplayer disabled for this session.");
                return;
            }
            _steamInitialized = true;

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PCBSMultiplayerPlugin).Assembly);

            Session.SessionLifecycle.Init();

            var panic = gameObject.AddComponent<UI.PanicHotkeyHandler>();
            panic.EnableEntry = _enabled;
            panic.Log = Logger;

            UI.LobbyPanel.Log = Logger;

            Logger.LogInfo($"PCBS Multiplayer {PluginVersion} loaded. Steam user: {SteamFriends.GetPersonaName()}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"PCBS Multiplayer init failed: {ex}. Continuing without the mod active.");
        }
    }

    private void Update()
    {
        if (!_steamInitialized) return;
        SteamAPI.RunCallbacks();

        var mgr = SessionManager.Current;
        if (mgr == null) return;

        var clientSt = mgr.Transport as SteamTransport;
        if (clientSt != null) clientSt.Pump();
        if (mgr.Role == SessionRole.Host)
        {
            foreach (var t in mgr.Host.Transports)
            {
                var hst = t as SteamTransport;
                if (hst != null) hst.Pump();
            }
        }

        mgr.Tick();
        mgr.Heartbeat((long)(Time.unscaledTime * 1000f));
    }

    private void OnDestroy()
    {
        if (_steamInitialized) SteamAPI.Shutdown();
        _steamInitialized = false;
    }
}
