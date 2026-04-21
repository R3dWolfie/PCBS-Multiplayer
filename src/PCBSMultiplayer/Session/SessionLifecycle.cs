using System;
using BepInEx.Logging;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.UI;
using Steamworks;

namespace PCBSMultiplayer.Session;

public static class SessionLifecycle
{
    private static readonly ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("PCBSMultiplayer.Lifecycle");

    private static readonly SteamLobby _lobby = new SteamLobby();
    public static SteamLobby Lobby { get { return _lobby; } }
    public static CSteamID PendingInvite { get; set; }
    public static bool HasPendingInvite { get; private set; }

    public static void Init()
    {
        _lobby.RegisterInviteHandler(OnInvite);
    }

    private static void OnInvite(CSteamID lobbyId)
    {
        PendingInvite = lobbyId;
        HasPendingInvite = true;
        Log.LogInfo("Invite received: lobby " + lobbyId);
    }

    public static void StartHost()
    {
        var cur = SessionManager.Current;
        if (cur != null && cur.Role == SessionRole.Host)
        {
            LobbyPanel.ShowForHost();
            Log.LogInfo("Host lobby already active; re-opened lobby panel.");
            return;
        }
        if (cur != null) { Log.LogWarning("Already joined as client — leave first."); return; }
        _lobby.CreateLobby(HostSession.ModVersion, OnHostLobbyCreated);
    }

    private static void OnHostLobbyCreated(CSteamID lobbyId)
    {
        var mgr = new SessionManager(SessionRole.Host, new NullTransport());
        SessionManager.Current = mgr;
        mgr.IsLive = true;
        mgr.Host.ClientAccepted += OnClientAccepted;
        _lobby.RegisterMemberJoinHandler(OnPeerJoined);
        LobbyPanel.ShowForHost();
        Log.LogInfo("Host session started; lobby " + lobbyId + "; lobby panel opened.");
    }

    private static void OnPeerJoined(CSteamID peerId)
    {
        var mgr = SessionManager.Current;
        if (mgr == null || mgr.Role != SessionRole.Host) return;
        var transport = new SteamTransport(peerId);
        mgr.Host.AttachClient(transport);
        Log.LogInfo("Peer joined lobby: " + peerId);
    }

    private static void OnClientAccepted(int slot)
    {
        LobbyPanel.RebroadcastState();
        Log.LogInfo("Client accepted (slot " + slot + "); LobbyState rebroadcast.");
    }

    public static void JoinPendingInvite()
    {
        if (!HasPendingInvite) { Log.LogInfo("No pending invite — ask your host to invite you via Steam overlay."); return; }
        var target = PendingInvite;
        HasPendingInvite = false;
        _lobby.JoinLobby(target, OnLobbyJoined);
    }

    private static void OnLobbyJoined(CSteamID hostId, string remoteVersion)
    {
        if (!LobbyVersionCheck.IsCompatible(ClientSession.ModVersion, remoteVersion))
        {
            Log.LogError(LobbyVersionCheck.Describe(ClientSession.ModVersion, remoteVersion));
            SteamMatchmaking.LeaveLobby(_lobby.LobbyId);
            return;
        }
        SteamNetworking.AcceptP2PSessionWithUser(hostId);
        BackupClientSavesBeforeJoin(_lobby.LobbyId.m_SteamID);
        var transport = new SteamTransport(hostId);
        var mgr = new SessionManager(SessionRole.Client, transport);
        SessionManager.Current = mgr;
        mgr.Router.On<LobbyState>(LobbyPanel.OnLobbyStateReceived);
        mgr.Router.On<StartGame>(LobbyPanel.OnStartGameReceived);
        mgr.Client.SteamId = SteamUser.GetSteamID().m_SteamID;
        mgr.Client.DisplayName = SteamFriends.GetPersonaName();
        mgr.Client.SayHello();
        string savesDir = null;
        try { savesDir = SaveLoadSystem.s_saveDir; } catch (Exception ex) { Log.LogError("s_saveDir resolve: " + ex); }
        mgr.Client.ConfigureSaveSync(savesDir ?? "", _lobby.LobbyId.m_SteamID);
        mgr.Client.SaveReady += LobbyPanel.OnSaveReady;
        LobbyPanel.ShowForClient();
        Log.LogInfo("Client session started; host " + hostId + ".");
    }

    public static void Stop()
    {
        var mgr = SessionManager.Current;

        if (mgr != null && mgr.Role == SessionRole.Client)
        {
            try
            {
                string savesDir = SaveLoadSystem.s_saveDir;
                ulong lobbyId = _lobby.LobbyId.m_SteamID;
                string path = System.IO.Path.Combine(savesDir, "mp-" + lobbyId + ".binary");
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                    Log.LogInfo("Cleaned up client mp-save: " + path);
                }
            }
            catch (Exception ex) { Log.LogWarning("mp-save cleanup failed: " + ex.Message); }
        }

        if (mgr != null && mgr.Transport != null) mgr.Transport.Disconnect();
        if (mgr != null && mgr.Role == SessionRole.Host && mgr.Host != null)
        {
            mgr.Host.ClientAccepted -= OnClientAccepted;
        }
        if (mgr != null && mgr.Role == SessionRole.Client && mgr.Client != null)
        {
            mgr.Client.SaveReady -= LobbyPanel.OnSaveReady;
        }
        SessionManager.Current = null;
        if (_lobby.LobbyId != CSteamID.Nil) SteamMatchmaking.LeaveLobby(_lobby.LobbyId);
        LobbyPanel.Hide();
        Log.LogInfo("Session stopped.");
    }

    private static void BackupClientSavesBeforeJoin(ulong lobbyId)
    {
        try
        {
            string savesDir = SaveLoadSystem.s_saveDir;
            if (string.IsNullOrEmpty(savesDir) || !System.IO.Directory.Exists(savesDir)) return;

            string backupDir = System.IO.Path.Combine(savesDir, "backup-mp-" + lobbyId);
            System.IO.Directory.CreateDirectory(backupDir);

            var files = System.IO.Directory.GetFiles(savesDir, "*.binary");
            int copied = 0;
            foreach (var src in files)
            {
                string name = System.IO.Path.GetFileName(src);
                if (name.StartsWith("mp-")) continue;
                string dst = System.IO.Path.Combine(backupDir, name);
                System.IO.File.Copy(src, dst, overwrite: true);
                copied++;
            }
            Log.LogInfo("Pre-join backup: copied " + copied + " save file(s) to " + backupDir);
        }
        catch (Exception ex)
        {
            Log.LogWarning("Pre-join backup failed (continuing anyway): " + ex.Message);
        }
    }
}

internal sealed class NullTransport : ITransport
{
    public bool IsConnected { get { return false; } }
    public void Send(byte[] payload) { }
    public bool TryReceive(out byte[] payload) { payload = new byte[0]; return false; }
    public void Disconnect() { }
}
