using System.Collections.Generic;

namespace PCBSMultiplayer.Net.Messages;

public struct LobbyPlayer
{
    public ulong SteamId;
    public string DisplayName;
    public bool IsHost;
    // False when the peer has joined the Steam lobby but their Hello handshake hasn't
    // completed yet. Host refuses to Start Game while any non-host player is not ready,
    // to avoid BeginSaveTransfer iterating an empty transport list.
    public bool IsReady;
}

public sealed class LobbyState : IMessage
{
    public List<LobbyPlayer> Players { get; set; } = new List<LobbyPlayer>();
    public string SelectedSaveName { get; set; } = "";
    public string SelectedSceneName { get; set; } = "";
    public TypeTag Tag => TypeTag.LobbyState;
}

public sealed class StartGame : IMessage
{
    public string SaveName { get; set; } = "";
    public string SceneName { get; set; } = "";
    public TypeTag Tag => TypeTag.StartGame;
}
