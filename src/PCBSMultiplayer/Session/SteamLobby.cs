using System;
using Steamworks;

namespace PCBSMultiplayer.Session;

public sealed class SteamLobby
{
    public const string VersionMetaKey = "mod_version";
    public const int MaxMembers = 4;

    public CSteamID LobbyId { get; private set; } = CSteamID.Nil;
    public bool IsHost { get; private set; }

    private Callback<LobbyCreated_t>? _onCreated;
    private Callback<LobbyEnter_t>? _onEnter;
    private Callback<GameLobbyJoinRequested_t>? _onJoinRequested;
    private Action<CSteamID>? _onHostCreated;
    private Action<CSteamID, string?>? _onJoined;

    public void CreateLobby(string modVersion, Action<CSteamID> onCreated)
    {
        _onHostCreated = onCreated;
        _onCreated ??= Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxMembers);
        _pendingModVersion = modVersion;
    }

    public void JoinLobby(CSteamID lobbyId, Action<CSteamID, string?> onJoined)
    {
        _onJoined = onJoined;
        _onEnter ??= Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    public void RegisterInviteHandler(Action<CSteamID> onInvite)
    {
        _onJoinRequested ??= Callback<GameLobbyJoinRequested_t>.Create(ev => onInvite(ev.m_steamIDLobby));
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
        _onHostCreated?.Invoke(LobbyId);
    }

    private void OnLobbyEnter(LobbyEnter_t ev)
    {
        LobbyId = new CSteamID(ev.m_ulSteamIDLobby);
        IsHost = false;
        var remote = SteamMatchmaking.GetLobbyData(LobbyId, VersionMetaKey);
        _onJoined?.Invoke(new CSteamID(SteamMatchmaking.GetLobbyOwner(LobbyId).m_SteamID), remote);
    }
}
