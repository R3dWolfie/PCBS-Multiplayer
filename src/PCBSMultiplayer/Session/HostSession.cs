using System.Collections.Generic;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.State;

namespace PCBSMultiplayer.Session;

public sealed class HostSession
{
    public const string ModVersion = "0.1.0";
    public const int MaxClients = 3;

    private readonly SessionManager _mgr;
    private readonly Dictionary<int, ClientInfo> _clients = new();
    private int _nextSlot = 1;

    public IReadOnlyDictionary<int, ClientInfo> Clients => _clients;

    public HostSession(SessionManager mgr)
    {
        _mgr = mgr;
        _mgr.LocalSlot = 0;
        _mgr.IsLive = true;
        _mgr.Router.On<Hello>(OnHello);
    }

    private void OnHello(Hello hello)
    {
        if (_clients.Count >= MaxClients) { Reject("lobby_full"); return; }
        if (hello.ModVersion != ModVersion) { Reject("version_mismatch"); return; }

        var slot = _nextSlot++;
        _clients[slot] = new ClientInfo { Slot = slot, SteamId = hello.SteamId, DisplayName = hello.DisplayName };
        var snapshot = SnapshotBuilder.Serialize(_mgr.World);
        var welcome = new Welcome { AssignedSlot = slot, SnapshotBytes = snapshot };
        _mgr.Transport.Send(Serializer.Pack(welcome));
    }

    private void Reject(string reason)
    {
        _mgr.Transport.Send(Serializer.Pack(new Bye { Reason = reason }));
    }
}

public sealed class ClientInfo
{
    public int Slot { get; set; }
    public ulong SteamId { get; set; }
    public string DisplayName { get; set; } = "";
}
