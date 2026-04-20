using System;
using System.Collections.Generic;
using BepInEx.Logging;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.Session;
using Steamworks;
using UnityEngine;

namespace PCBSMultiplayer.UI;

public sealed class LobbyPanel : MonoBehaviour
{
    public static LobbyPanel Instance { get; private set; }
    public static ManualLogSource Log;

    private bool _isHost;
    private bool _visible;

    private List<LobbyPlayer> _players = new List<LobbyPlayer>();
    private string _selectedSaveName = "";
    private string _selectedSceneName = "";
    private string _errorMessage = "";

    private struct SaveEntry { public string Name; public string Display; public string Scene; public string GameMode; public int Cash; public int Kudos; }
    private List<SaveEntry> _saves = new List<SaveEntry>();
    private Vector2 _scroll;
    private int _selectedIndex = -1;
    private string _newCareerName = "Multiplayer-Career";

    private static readonly Color NavyDeep    = new Color(0.07f, 0.09f, 0.18f, 0.96f);
    private static readonly Color NavyPanel   = new Color(0.11f, 0.14f, 0.28f, 1f);
    private static readonly Color NavyRow     = new Color(0.15f, 0.19f, 0.36f, 1f);
    private static readonly Color NavyRowAlt  = new Color(0.13f, 0.16f, 0.30f, 1f);
    private static readonly Color Divider     = new Color(0.25f, 0.30f, 0.55f, 1f);
    private static readonly Color Accent      = new Color(0.32f, 0.72f, 0.95f, 1f);
    private static readonly Color AccentHover = new Color(0.45f, 0.82f, 1.0f, 1f);
    private static readonly Color Select      = new Color(0.22f, 0.85f, 0.48f, 1f);
    private static readonly Color TextLight   = new Color(0.94f, 0.95f, 0.97f, 1f);
    private static readonly Color TextDim     = new Color(0.68f, 0.72f, 0.82f, 1f);
    private static readonly Color TextError   = new Color(1.0f, 0.48f, 0.48f, 1f);
    private static readonly Color BtnIdle     = new Color(0.20f, 0.25f, 0.48f, 1f);
    private static readonly Color BtnHover    = new Color(0.28f, 0.36f, 0.65f, 1f);
    private static readonly Color BtnDisabled = new Color(0.15f, 0.17f, 0.22f, 1f);

    private static GUIStyle _titleStyle, _h2Style, _labelStyle, _dimStyle, _rowStyle, _rowSelStyle, _btnStyle, _btnPrimaryStyle, _btnDangerStyle;
    private static Texture2D _navyTex, _navyRowTex, _navyAltTex, _dividerTex, _btnIdleTex, _btnHoverTex, _btnDisTex, _selTex, _selHoverTex, _primaryTex, _primaryHoverTex, _dangerTex, _dangerHoverTex;
    private static bool _stylesBuilt;

    public static void ShowForHost()
    {
        if (Log != null) Log.LogInfo("LobbyPanel.ShowForHost() called");
        var p = EnsureInstance();
        p._isHost = true;
        p._visible = true;
        p._errorMessage = "";
        if (p._players == null) p._players = new List<LobbyPlayer>();
        if (p._saves == null) p._saves = new List<SaveEntry>();
        try { p.RefreshPlayers(); }
        catch (Exception ex) { p._errorMessage = "RefreshPlayers crashed: " + ex.Message; if (Log != null) Log.LogError("RefreshPlayers outer: " + ex); }
        try { p.RefreshSaves(); }
        catch (Exception ex) { p._errorMessage = "RefreshSaves crashed: " + ex.Message; if (Log != null) Log.LogError("RefreshSaves outer: " + ex); }
        if (Log != null) Log.LogInfo("ShowForHost: players=" + p._players.Count + " saves=" + p._saves.Count);
    }

    public static void ShowForClient()
    {
        var p = EnsureInstance();
        p._isHost = false;
        p._visible = true;
        p._errorMessage = "";
        p._players.Clear();
        try
        {
            p._players.Add(new LobbyPlayer
            {
                SteamId = SteamUser.GetSteamID().m_SteamID,
                DisplayName = SteamFriends.GetPersonaName(),
                IsHost = false
            });
        }
        catch (Exception ex) { if (Log != null) Log.LogError("ShowForClient: " + ex); }
    }

    public static void Hide() { if (Instance != null) Instance._visible = false; }

    public static void OnLobbyStateReceived(LobbyState s)
    {
        if (Instance == null || Instance._isHost) return;
        Instance._players = s.Players;
        Instance._selectedSaveName = s.SelectedSaveName;
        Instance._selectedSceneName = s.SelectedSceneName;
    }

    public static void OnStartGameReceived(StartGame s)
    {
        if (Instance == null || Instance._isHost) return;
        if (Log != null) Log.LogInfo("Client received StartGame: " + s.SaveName + " / " + s.SceneName);
        Instance.TryLoadLocally(s.SaveName, s.SceneName);
    }

    public static void RebroadcastState()
    {
        if (Instance == null || !Instance._isHost) return;
        Instance.RefreshPlayers();
        Instance.BroadcastLobbyState();
    }

    private static LobbyPanel EnsureInstance()
    {
        if (Instance == null)
        {
            var go = new GameObject("PCBSMultiplayer.LobbyPanel");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<LobbyPanel>();
        }
        return Instance;
    }

    private void RefreshPlayers()
    {
        if (_players == null) _players = new List<LobbyPlayer>();
        _players.Clear();
        ulong selfId = 0;
        string selfName = "Host (you)";
        try { selfId = SteamUser.GetSteamID().m_SteamID; }
        catch (Exception ex) { if (Log != null) Log.LogError("GetSteamID: " + ex); }
        try { selfName = SteamFriends.GetPersonaName() ?? "Host (you)"; }
        catch (Exception ex) { if (Log != null) Log.LogError("GetPersonaName: " + ex); }
        _players.Add(new LobbyPlayer { SteamId = selfId, DisplayName = selfName, IsHost = true });
        if (Log != null) Log.LogInfo("RefreshPlayers self added: " + selfName + " (" + selfId + ")");

        var mgr = SessionManager.Current;
        if (mgr == null || mgr.Host == null) return;
        foreach (var kv in mgr.Host.Clients)
        {
            _players.Add(new LobbyPlayer
            {
                SteamId = kv.Value.SteamId,
                DisplayName = string.IsNullOrEmpty(kv.Value.DisplayName) ? ("Guest " + kv.Value.Slot) : kv.Value.DisplayName,
                IsHost = false
            });
        }
    }

    private void RefreshSaves()
    {
        if (_saves == null) _saves = new List<SaveEntry>();
        _saves.Clear();
        int total = 0, kept = 0;
        try
        {
            foreach (var info in SaveLoadSystem.GetSaveGames())
            {
                total++;
                var h = info.m_header;
                if (h == null) { if (Log != null) Log.LogWarning("Save " + info.m_info.Name + " has null header"); continue; }
                if (h.m_gameMode == GameMode.NOT_SET || h.m_gameMode == GameMode.HOW_TO_BUILD_A_PC) continue;
                _saves.Add(new SaveEntry
                {
                    Name = info.m_info.Name,
                    Display = string.IsNullOrEmpty(h.m_saveName) ? info.m_info.Name : h.m_saveName,
                    Scene = h.m_scene,
                    GameMode = h.m_gameMode.ToString().Replace("DLC_", ""),
                    Cash = h.m_cash,
                    Kudos = h.m_kudos
                });
                kept++;
            }
            if (Log != null) Log.LogInfo("RefreshSaves: enumerated " + total + ", kept " + kept);
        }
        catch (Exception ex)
        {
            _errorMessage = "Save enumeration: " + ex.Message;
            if (Log != null) Log.LogError("RefreshSaves: " + ex);
        }
    }

    private void BroadcastLobbyState()
    {
        var mgr = SessionManager.Current;
        if (mgr == null || mgr.Role != SessionRole.Host) return;
        var state = new LobbyState
        {
            Players = new List<LobbyPlayer>(_players),
            SelectedSaveName = _selectedSaveName,
            SelectedSceneName = _selectedSceneName
        };
        var frame = Serializer.Pack(state);
        foreach (var t in mgr.Host.Transports) t.Send(frame);
    }

    private void OnHostStartNewCareerClicked()
    {
        var label = string.IsNullOrEmpty(_newCareerName) ? "Multiplayer-Career" : _newCareerName.Trim();
        if (Log != null) Log.LogInfo("OnHostStartNewCareerClicked: label=\"" + label + "\"");
        try
        {
            var mgr = SessionManager.Current;
            if (mgr != null && mgr.Host != null)
            {
                var frame = Serializer.Pack(new StartGame { SaveName = "__NEW_CAREER__:" + label, SceneName = "" });
                foreach (var t in mgr.Host.Transports) t.Send(frame);
            }
        }
        catch (Exception ex) { if (Log != null) Log.LogError("Broadcast new-career StartGame: " + ex); }

        try
        {
            var mm = UnityEngine.Object.FindObjectOfType<MainMenu>();
            if (mm == null)
            {
                _errorMessage = "MainMenu not found — cannot start new career.";
                if (Log != null) Log.LogError("FindObjectOfType<MainMenu>() returned null");
                return;
            }
            _visible = false;
            if (Log != null) Log.LogInfo("Calling MainMenu.PlayCareer(CAREER, false)");
            mm.PlayCareer(GameMode.CAREER, false);
        }
        catch (Exception ex)
        {
            _errorMessage = "New career failed: " + ex.Message;
            if (Log != null) Log.LogError("PlayCareer: " + ex);
        }
    }

    private void OnHostStartClicked()
    {
        if (Log != null) Log.LogInfo("OnHostStartClicked: save=\"" + _selectedSaveName + "\" scene=\"" + _selectedSceneName + "\"");
        if (string.IsNullOrEmpty(_selectedSaveName)) { _errorMessage = "Pick a save first."; return; }

        var mgr = SessionManager.Current;
        if (mgr == null || mgr.Role != SessionRole.Host) { _errorMessage = "Not hosting."; return; }

        // broadcast save bytes to all clients first — they reassemble while host loads
        string savesDir = ResolveSavesDir();
        if (savesDir == null) { _errorMessage = "Could not resolve SaveLoadSystem.s_saveDir"; return; }

        string err;
        bool ok = mgr.Host.BeginSaveTransfer(_selectedSaveName, _selectedSceneName, savesDir, out err);
        if (!ok)
        {
            _errorMessage = "Save transfer failed: " + err;
            if (Log != null) Log.LogError("BeginSaveTransfer: " + err);
            return;
        }

        // now broadcast the scene-start signal — clients load *after* SaveTransferEnd arrives
        foreach (var t in mgr.Host.Transports)
            t.Send(Serializer.Pack(new StartGame { SaveName = _selectedSaveName, SceneName = _selectedSceneName }));

        // host loads its own save locally (unchanged — client receives bytes in parallel)
        TryLoadLocally(_selectedSaveName, _selectedSceneName);
    }

    private string ResolveSavesDir()
    {
        try
        {
            return SaveLoadSystem.s_saveDir;
        }
        catch (Exception ex)
        {
            if (Log != null) Log.LogError("ResolveSavesDir: " + ex);
            return null;
        }
    }

    private void TryLoadLocally(string saveName, string scene)
    {
        if (Log != null) Log.LogInfo("TryLoadLocally entry: save=\"" + saveName + "\" scene=\"" + scene + "\"");
        try
        {
            LevelLoadPersistency llp = null;
            try
            {
                if (PCBS.Singleton<LevelLoadPersistency>.InstanceExists)
                {
                    llp = PCBS.Singleton<LevelLoadPersistency>.Instance;
                    if (Log != null) Log.LogInfo("TryLoadLocally: got llp via Singleton<>.Instance");
                }
            }
            catch (Exception ex) { if (Log != null) Log.LogError("Singleton<LevelLoadPersistency>.Instance failed: " + ex); }

            if (llp == null)
            {
                var all = UnityEngine.Object.FindObjectsOfType<LevelLoadPersistency>();
                if (Log != null) Log.LogInfo("TryLoadLocally: FindObjectsOfType returned " + (all == null ? -1 : all.Length));
                if (all != null && all.Length > 0) llp = all[0];
            }

            if (llp == null)
            {
                _errorMessage = "LevelLoadPersistency not available — is main menu fully loaded?";
                if (Log != null) Log.LogError("TryLoadLocally: LevelLoadPersistency not found by any lookup");
                return;
            }

            if (Log != null) Log.LogInfo("TryLoadLocally: calling llp.LoadGameFromDir(\"" + saveName + "\", \"" + scene + "\")");
            llp.LoadGameFromDir(saveName, scene);
            if (Log != null) Log.LogInfo("TryLoadLocally: LoadGameFromDir returned (scene load should be in progress)");
            _visible = false;
        }
        catch (Exception ex)
        {
            _errorMessage = "Load failed: " + ex.Message;
            if (Log != null) Log.LogError("Load failed: " + ex);
        }
    }

    private static Texture2D SolidTex(Color c)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var px = new Color[4]; for (int i = 0; i < 4; i++) px[i] = c;
        t.SetPixels(px); t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    private static void BuildStyles()
    {
        if (_stylesBuilt) return;
        _navyTex       = SolidTex(NavyDeep);
        _navyRowTex    = SolidTex(NavyRow);
        _navyAltTex    = SolidTex(NavyRowAlt);
        _dividerTex    = SolidTex(Divider);
        _btnIdleTex    = SolidTex(BtnIdle);
        _btnHoverTex   = SolidTex(BtnHover);
        _btnDisTex     = SolidTex(BtnDisabled);
        _selTex        = SolidTex(new Color(0.16f, 0.38f, 0.28f, 1f));
        _selHoverTex   = SolidTex(new Color(0.22f, 0.52f, 0.36f, 1f));
        _primaryTex    = SolidTex(new Color(0.18f, 0.55f, 0.88f, 1f));
        _primaryHoverTex = SolidTex(new Color(0.28f, 0.68f, 1.0f, 1f));
        _dangerTex     = SolidTex(new Color(0.55f, 0.22f, 0.26f, 1f));
        _dangerHoverTex = SolidTex(new Color(0.75f, 0.30f, 0.34f, 1f));

        _titleStyle = new GUIStyle { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _titleStyle.normal.textColor = TextLight;
        _titleStyle.padding = new RectOffset(0, 0, 0, 0);

        _h2Style = new GUIStyle { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        _h2Style.normal.textColor = TextLight;
        _h2Style.padding = new RectOffset(4, 4, 0, 0);

        _labelStyle = new GUIStyle { fontSize = 18, alignment = TextAnchor.MiddleLeft };
        _labelStyle.normal.textColor = TextLight;
        _labelStyle.padding = new RectOffset(4, 4, 0, 0);

        _dimStyle = new GUIStyle(_labelStyle);
        _dimStyle.normal.textColor = TextDim;
        _dimStyle.fontSize = 15;

        _rowStyle = new GUIStyle { fontSize = 17, alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal };
        _rowStyle.padding = new RectOffset(14, 14, 6, 6);
        _rowStyle.normal.background = _navyRowTex;
        _rowStyle.normal.textColor = TextLight;
        _rowStyle.hover.background = SolidTex(new Color(0.19f, 0.24f, 0.45f, 1f));
        _rowStyle.hover.textColor = TextLight;
        _rowStyle.active.background = _rowStyle.hover.background;
        _rowStyle.active.textColor = TextLight;
        _rowStyle.wordWrap = false;
        _rowStyle.alignment = TextAnchor.MiddleLeft;

        _rowSelStyle = new GUIStyle(_rowStyle);
        _rowSelStyle.normal.background = _selTex;
        _rowSelStyle.hover.background = _selHoverTex;
        _rowSelStyle.active.background = _selHoverTex;
        _rowSelStyle.normal.textColor = new Color(0.94f, 1f, 0.96f, 1f);
        _rowSelStyle.hover.textColor = _rowSelStyle.normal.textColor;
        _rowSelStyle.fontStyle = FontStyle.Bold;

        _btnStyle = new GUIStyle { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _btnStyle.padding = new RectOffset(12, 12, 8, 8);
        _btnStyle.normal.background = _btnIdleTex;
        _btnStyle.normal.textColor = TextLight;
        _btnStyle.hover.background = _btnHoverTex;
        _btnStyle.hover.textColor = TextLight;
        _btnStyle.active.background = _btnHoverTex;
        _btnStyle.active.textColor = TextLight;

        _btnPrimaryStyle = new GUIStyle(_btnStyle);
        _btnPrimaryStyle.normal.background = _primaryTex;
        _btnPrimaryStyle.hover.background = _primaryHoverTex;
        _btnPrimaryStyle.active.background = _primaryHoverTex;

        _btnDangerStyle = new GUIStyle(_btnStyle);
        _btnDangerStyle.normal.background = _dangerTex;
        _btnDangerStyle.hover.background = _dangerHoverTex;
        _btnDangerStyle.active.background = _dangerHoverTex;

        _stylesBuilt = true;
    }

    private static void FillRect(Rect r, Texture2D tex)
    {
        var prev = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
        GUI.color = prev;
    }

    private void OnGUI()
    {
        if (!_visible) return;
        BuildStyles();

        float winW = 1100f, winH = 760f;
        float x = (Screen.width - winW) * 0.5f;
        float y = (Screen.height - winH) * 0.5f;

        // Dim full screen behind
        var fullPrev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = fullPrev;

        // Panel background
        FillRect(new Rect(x, y, winW, winH), _navyTex);

        // Title bar
        float titleH = 68f;
        FillRect(new Rect(x, y, winW, titleH), SolidTex(NavyPanel));
        GUI.Label(new Rect(x, y, winW, titleH), _isHost ? "MULTIPLAYER LOBBY" : "MULTIPLAYER LOBBY — GUEST", _titleStyle);
        FillRect(new Rect(x, y + titleH, winW, 2f), _dividerTex);

        // Content area
        float contentY = y + titleH + 16f;
        float footerH = 96f;
        float contentH = winH - titleH - 16f - footerH - 16f;

        float gap = 24f;
        float col1W = winW * 0.38f - gap;
        float col2W = winW - col1W - gap * 2f - 24f;
        float col1X = x + 24f;
        float col2X = col1X + col1W + gap;

        // Column headers
        GUI.Label(new Rect(col1X, contentY, col1W, 32f), "Players  (" + _players.Count + "/4)", _h2Style);
        GUI.Label(new Rect(col2X, contentY, col2W, 32f), _isHost ? ("Career Saves  (" + _saves.Count + ")") : "Selected Save", _h2Style);

        float listY = contentY + 40f;
        float listH = contentH - 40f;

        // Player list
        FillRect(new Rect(col1X, listY, col1W, listH), SolidTex(NavyRowAlt));
        float py = listY + 8f;
        for (int i = 0; i < _players.Count; i++)
        {
            var p = _players[i];
            var rowRect = new Rect(col1X + 6f, py, col1W - 12f, 42f);
            var rowBg = (i % 2 == 0) ? _navyRowTex : _navyAltTex;
            FillRect(rowRect, rowBg);
            if (p.IsHost) FillRect(new Rect(rowRect.x, rowRect.y, 4f, rowRect.height), SolidTex(Accent));
            var badge = p.IsHost ? "HOST  " : "GUEST ";
            var badgeStyle = new GUIStyle(_labelStyle);
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.normal.textColor = p.IsHost ? Accent : TextDim;
            badgeStyle.fontSize = 14;
            GUI.Label(new Rect(rowRect.x + 14f, rowRect.y, 80f, rowRect.height), badge, badgeStyle);
            GUI.Label(new Rect(rowRect.x + 96f, rowRect.y, rowRect.width - 96f, rowRect.height), p.DisplayName ?? "(unknown)", _labelStyle);
            py += 48f;
        }
        if (_players.Count < 4)
        {
            var emptyRect = new Rect(col1X + 6f, py, col1W - 12f, 42f);
            FillRect(emptyRect, SolidTex(new Color(0.10f, 0.12f, 0.22f, 1f)));
            GUI.Label(new Rect(emptyRect.x + 14f, emptyRect.y, emptyRect.width - 28f, emptyRect.height), "waiting for invite…", _dimStyle);
        }

        // Save list (host) or "waiting" (client)
        FillRect(new Rect(col2X, listY, col2W, listH), SolidTex(NavyRowAlt));

        if (_isHost)
        {
            float refreshW = 100f;
            if (GUI.Button(new Rect(col2X + col2W - refreshW - 6f, contentY - 4f, refreshW, 30f), "↻ Refresh", _btnStyle))
                RefreshSaves();

            // New career bar (above save list)
            float ncBarH = 56f;
            var ncBar = new Rect(col2X, listY, col2W, ncBarH);
            FillRect(ncBar, SolidTex(new Color(0.14f, 0.22f, 0.38f, 1f)));
            var ncLbl = new GUIStyle(_dimStyle); ncLbl.fontSize = 13;
            GUI.Label(new Rect(ncBar.x + 10f, ncBar.y + 2f, 200f, 18f), "Start fresh career as:", ncLbl);
            var tfStyle = new GUIStyle(GUI.skin.textField);
            tfStyle.fontSize = 16;
            tfStyle.alignment = TextAnchor.MiddleLeft;
            tfStyle.padding = new RectOffset(8, 8, 4, 4);
            var tfRect = new Rect(ncBar.x + 10f, ncBar.y + 22f, col2W - 220f, 30f);
            _newCareerName = GUI.TextField(tfRect, _newCareerName ?? "", 64, tfStyle);
            var ncBtnRect = new Rect(ncBar.x + col2W - 200f, ncBar.y + 8f, 190f, 40f);
            if (GUI.Button(ncBtnRect, "▶  Start New Career", _btnPrimaryStyle))
            {
                if (Log != null) Log.LogInfo("Start New Career clicked: name=\"" + _newCareerName + "\"");
                try { OnHostStartNewCareerClicked(); }
                catch (Exception ex) { if (Log != null) Log.LogError("OnHostStartNewCareerClicked threw: " + ex); _errorMessage = "New career dispatch failed: " + ex.Message; }
            }

            listY += ncBarH + 10f;
            listH -= ncBarH + 10f;

            int rowH = 60;
            var listRect = new Rect(col2X, listY, col2W, listH);
            var viewRect = new Rect(0, 0, col2W - 24f, Mathf.Max(_saves.Count * (rowH + 4), listH - 4f));
            _scroll = GUI.BeginScrollView(listRect, _scroll, viewRect);
            for (int i = 0; i < _saves.Count; i++)
            {
                var s = _saves[i];
                var rect = new Rect(4f, i * (rowH + 4) + 4f, col2W - 32f, rowH);
                var selected = (i == _selectedIndex);

                FillRect(rect, selected ? _selTex : (i % 2 == 0 ? _navyRowTex : _navyAltTex));
                if (selected) FillRect(new Rect(rect.x, rect.y, 4f, rect.height), SolidTex(Select));

                var titleRect = new Rect(rect.x + 14f, rect.y + 6f, rect.width - 28f, 26f);
                var subRect   = new Rect(rect.x + 14f, rect.y + 32f, rect.width - 28f, 22f);
                var titleSt = new GUIStyle(_labelStyle); titleSt.fontSize = 18; titleSt.fontStyle = FontStyle.Bold;
                GUI.Label(titleRect, s.Display, titleSt);

                var subSt = new GUIStyle(_dimStyle); subSt.fontSize = 14;
                if (selected) subSt.normal.textColor = new Color(0.75f, 1f, 0.88f, 1f);
                GUI.Label(subRect, s.GameMode + "   $" + s.Cash.ToString("N0") + "   " + s.Kudos + " kudos", subSt);

                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    _selectedIndex = i;
                    _selectedSaveName = s.Name;
                    _selectedSceneName = s.Scene;
                    if (Log != null) Log.LogInfo("Save row clicked: idx=" + i + " name=\"" + s.Name + "\" scene=\"" + s.Scene + "\"");
                    BroadcastLobbyState();
                    e.Use();
                }
            }
            GUI.EndScrollView();
        }
        else
        {
            var waitStyle = new GUIStyle(_labelStyle);
            waitStyle.alignment = TextAnchor.MiddleCenter;
            waitStyle.fontSize = 20;
            waitStyle.normal.textColor = TextDim;
            if (string.IsNullOrEmpty(_selectedSaveName))
            {
                GUI.Label(new Rect(col2X, listY, col2W, listH), "Host is choosing a save…", waitStyle);
            }
            else
            {
                var titleSt = new GUIStyle(_labelStyle); titleSt.fontSize = 22; titleSt.fontStyle = FontStyle.Bold; titleSt.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(col2X, listY + listH * 0.30f, col2W, 40f), "Selected save", _dimStyle);
                GUI.Label(new Rect(col2X, listY + listH * 0.30f + 36f, col2W, 48f), _selectedSaveName, titleSt);
                var note = new GUIStyle(_dimStyle); note.alignment = TextAnchor.MiddleCenter; note.fontSize = 14;
                GUI.Label(new Rect(col2X, listY + listH * 0.30f + 96f, col2W, 30f), "Make sure you have this save locally on your machine.", note);
            }
        }

        // Footer divider
        float footerY = y + winH - footerH;
        FillRect(new Rect(x, footerY - 2f, winW, 2f), _dividerTex);

        // Error message strip
        if (!string.IsNullOrEmpty(_errorMessage))
        {
            var errSt = new GUIStyle(_labelStyle); errSt.normal.textColor = TextError; errSt.fontSize = 15;
            FillRect(new Rect(x + 24f, footerY - 40f, winW - 48f, 30f), SolidTex(new Color(0.3f, 0.08f, 0.1f, 0.8f)));
            GUI.Label(new Rect(x + 36f, footerY - 40f, winW - 72f, 30f), "⚠  " + _errorMessage, errSt);
        }

        // Footer buttons
        float btnH = 56f;
        float btnY = footerY + (footerH - btnH) * 0.5f;
        float btnW1 = 200f, btnW2 = 240f, btnW3 = 180f;
        float fgap = 14f;
        float bx = x + 24f;

        if (Event.current.type == EventType.MouseDown && Log != null)
            Log.LogInfo("LobbyPanel MouseDown at " + Event.current.mousePosition + " btn=" + Event.current.button);

        var inviteRect = new Rect(bx, btnY, btnW1, btnH);
        if (GUI.Button(inviteRect, "Invite Friends", _btnStyle))
        {
            if (Log != null) Log.LogInfo("Invite Friends clicked");
            SessionLifecycle.Lobby.OpenInviteOverlay();
        }
        bx += btnW1 + fgap;

        var startRect = new Rect(bx, btnY, btnW2, btnH);
        if (_isHost)
        {
            var canStart = !string.IsNullOrEmpty(_selectedSaveName);
            if (canStart)
            {
                if (GUI.Button(startRect, "▶  Start Game", _btnPrimaryStyle))
                {
                    if (Log != null) Log.LogInfo("Start Game clicked; selectedSave=\"" + _selectedSaveName + "\"");
                    try { OnHostStartClicked(); }
                    catch (Exception ex) { if (Log != null) Log.LogError("OnHostStartClicked threw: " + ex); _errorMessage = "Start dispatch failed: " + ex.Message; }
                }
            }
            else
            {
                FillRect(startRect, _btnDisTex);
                var lblSt = new GUIStyle(_labelStyle); lblSt.alignment = TextAnchor.MiddleCenter; lblSt.normal.textColor = TextDim; lblSt.fontSize = 18; lblSt.fontStyle = FontStyle.Bold;
                GUI.Label(startRect, "Pick a save to start", lblSt);
            }
        }
        else
        {
            FillRect(startRect, _btnDisTex);
            var lblSt = new GUIStyle(_labelStyle); lblSt.alignment = TextAnchor.MiddleCenter; lblSt.normal.textColor = TextDim; lblSt.fontSize = 18; lblSt.fontStyle = FontStyle.Bold;
            GUI.Label(startRect, "Waiting for host…", lblSt);
        }

        float rx = x + winW - btnW3 - 24f;
        var leaveRect = new Rect(rx, btnY, btnW3, btnH);
        if (GUI.Button(leaveRect, "Leave Lobby", _btnDangerStyle))
        {
            if (Log != null) Log.LogInfo("Leave Lobby clicked");
            SessionLifecycle.Stop();
            _visible = false;
        }
    }
}
