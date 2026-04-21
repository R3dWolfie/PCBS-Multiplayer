using System;
using System.Collections.Generic;
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
    // PluginVersion must be System.Version-parseable (digits+dots only) — BepInEx 5.x rejects
    // SemVer pre-release suffixes like "-rc1" with "Skipping type ... version is invalid".
    public const string PluginVersion = "0.3.0.0";
    public const string DisplayVersion = "0.3.0-alpha-preview2";

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

            Logger.LogInfo($"PCBS Multiplayer {DisplayVersion} loaded. Steam user: {SteamFriends.GetPersonaName()}");
            SweepStaleMpSaves();
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

    private void SweepStaleMpSaves()
    {
        try
        {
            string savesDir;
            try { savesDir = SaveLoadSystem.s_saveDir; } catch { return; }
            if (string.IsNullOrEmpty(savesDir) || !System.IO.Directory.Exists(savesDir)) return;

            DateTime cutoff = DateTime.UtcNow.AddDays(-7);

            int removedFiles = 0;
            var files = System.IO.Directory.GetFiles(savesDir, "mp-*.binary");
            foreach (var f in files)
            {
                try
                {
                    if (System.IO.File.GetLastWriteTimeUtc(f) < cutoff)
                    {
                        System.IO.File.Delete(f);
                        removedFiles++;
                    }
                }
                catch (Exception ex) { Logger.LogWarning("Stale mp-save skipped: " + f + " — " + ex.Message); }
            }

            int removedDirs = 0;
            var dirs = System.IO.Directory.GetDirectories(savesDir, "backup-mp-*");
            foreach (var d in dirs)
            {
                try
                {
                    if (System.IO.Directory.GetLastWriteTimeUtc(d) < cutoff)
                    {
                        System.IO.Directory.Delete(d, recursive: true);
                        removedDirs++;
                    }
                }
                catch (Exception ex) { Logger.LogWarning("Stale backup dir skipped: " + d + " — " + ex.Message); }
            }

            if (removedFiles > 0 || removedDirs > 0)
            {
                var parts = new List<string>(2);
                if (removedFiles > 0) parts.Add(removedFiles + " mp-*.binary file(s)");
                if (removedDirs > 0) parts.Add(removedDirs + " backup-mp-*/ dir(s)");
                Logger.LogInfo("Swept stale " + string.Join(" + ", parts.ToArray()) + " from " + savesDir);
            }
        }
        catch (Exception ex) { Logger.LogWarning("SweepStaleMpSaves failed: " + ex.Message); }
    }
}
