using Steamworks;

namespace PCBSMultiplayer.Session;

public delegate void HostCreatedHandler(CSteamID lobbyId);
public delegate void LobbyJoinedHandler(CSteamID hostId, string remoteVersion);
public delegate void InviteHandler(CSteamID lobbyId);
public delegate void MemberJoinedHandler(CSteamID peerId);

public sealed class SteamLobby
{
    public const string VersionMetaKey = "mod_version";
    public const int MaxMembers = 4;

    public CSteamID LobbyId { get; private set; } = CSteamID.Nil;
    public bool IsHost { get; private set; }

    private Callback<LobbyCreated_t> _onCreated;
    private Callback<LobbyEnter_t> _onEnter;
    private Callback<GameLobbyJoinRequested_t> _onJoinRequested;
    private Callback<LobbyChatUpdate_t> _onChat;
    private HostCreatedHandler _onHostCreated;
    private LobbyJoinedHandler _onJoined;
    private InviteHandler _onInvite;
    private MemberJoinedHandler _onMemberJoined;

    public void CreateLobby(string modVersion, HostCreatedHandler onCreated)
    {
        _onHostCreated = onCreated;
        _onCreated ??= Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxMembers);
        _pendingModVersion = modVersion;
    }

    public void JoinLobby(CSteamID lobbyId, LobbyJoinedHandler onJoined)
    {
        IsHost = false;
        _onJoined = onJoined;
        _onEnter ??= Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    public void RegisterInviteHandler(InviteHandler onInvite)
    {
        _onInvite = onInvite;
        _onJoinRequested ??= Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
    }

    public void RegisterMemberJoinHandler(MemberJoinedHandler onMemberJoined)
    {
        _onMemberJoined = onMemberJoined;
        _onChat ??= Callback<LobbyChatUpdate_t>.Create(OnChat);
    }

    private void OnChat(LobbyChatUpdate_t ev)
    {
        var flags = (EChatMemberStateChange)ev.m_rgfChatMemberStateChange;
        if ((flags & EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0
            && _onMemberJoined != null)
            _onMemberJoined(new CSteamID(ev.m_ulSteamIDUserChanged));
    }

    public void OpenInviteOverlay()
    {
        if (LobbyId == CSteamID.Nil) return;
        SteamFriends.ActivateGameOverlayInviteDialog(LobbyId);
    }

    private string _pendingModVersion = "";

    private void OnLobbyCreated(LobbyCreated_t ev)
    {
        if (ev.m_eResult != EResult.k_EResultOK) return;
        LobbyId = new CSteamID(ev.m_ulSteamIDLobby);
        IsHost = true;
        SteamMatchmaking.SetLobbyData(LobbyId, VersionMetaKey, _pendingModVersion);
        if (_onHostCreated != null) _onHostCreated(LobbyId);
    }

    private void OnLobbyEnter(LobbyEnter_t ev)
    {
        // Steam fires LobbyEnter_t on the creator too (auto-join). If this is our own lobby we
        // just created, skip — otherwise we'd spin up a phantom client session pointing at self.
        if (IsHost && LobbyId.m_SteamID == ev.m_ulSteamIDLobby) return;
        LobbyId = new CSteamID(ev.m_ulSteamIDLobby);
        IsHost = false;
        var remote = SteamMatchmaking.GetLobbyData(LobbyId, VersionMetaKey);
        if (_onJoined != null) _onJoined(new CSteamID(SteamMatchmaking.GetLobbyOwner(LobbyId).m_SteamID), remote);
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t ev)
    {
        if (_onInvite != null) _onInvite(ev.m_steamIDLobby);
    }
}
