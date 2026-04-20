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
        LobbyPanel.RebroadcastState();
        Log.LogInfo("Peer joined lobby: " + peerId);
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
        var transport = new SteamTransport(hostId);
        var mgr = new SessionManager(SessionRole.Client, transport);
        SessionManager.Current = mgr;
        mgr.Router.On<LobbyState>(LobbyPanel.OnLobbyStateReceived);
        mgr.Router.On<StartGame>(LobbyPanel.OnStartGameReceived);
        mgr.Client.SteamId = SteamUser.GetSteamID().m_SteamID;
        mgr.Client.DisplayName = SteamFriends.GetPersonaName();
        mgr.Client.SayHello();
        LobbyPanel.ShowForClient();
        Log.LogInfo("Client session started; host " + hostId + ".");
    }

    public static void Stop()
    {
        var mgr = SessionManager.Current;
        if (mgr != null && mgr.Transport != null) mgr.Transport.Disconnect();
        SessionManager.Current = null;
        if (_lobby.LobbyId != CSteamID.Nil) SteamMatchmaking.LeaveLobby(_lobby.LobbyId);
        LobbyPanel.Hide();
        Log.LogInfo("Session stopped.");
    }
}

internal sealed class NullTransport : ITransport
{
    public bool IsConnected { get { return false; } }
    public void Send(byte[] payload) { }
    public bool TryReceive(out byte[] payload) { payload = new byte[0]; return false; }
    public void Disconnect() { }
}
