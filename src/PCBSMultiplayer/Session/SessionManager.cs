using System;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.State;

namespace PCBSMultiplayer.Session;

public enum SessionRole { Host, Client }

public sealed class SessionManager
{
    public SessionRole Role { get; }
    public WorldState World { get; } = new();
    public MessageRouter Router { get; } = new();
    public ITransport Transport { get; }
    public int LocalSlot { get; internal set; } = -1;
    public bool IsLive { get; internal set; }

    private readonly HostSession? _host;
    private readonly ClientSession? _client;

    public SessionManager(SessionRole role, ITransport transport)
    {
        Role = role;
        Transport = transport;
        if (role == SessionRole.Host) _host = new HostSession(this);
        else _client = new ClientSession(this);
    }

    public void Tick()
    {
        while (Transport.TryReceive(out var frame))
            Router.Dispatch(frame);
    }

    public HostSession Host => _host ?? throw new InvalidOperationException("not a host");
    public ClientSession Client => _client ?? throw new InvalidOperationException("not a client");
}
