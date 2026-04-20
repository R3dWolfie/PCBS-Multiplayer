using System;
using System.Collections.Generic;
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
    private readonly List<(ITransport t, MessageRouter r)> _clientTransports = new();

    public SessionManager(SessionRole role, ITransport transport)
    {
        Role = role;
        Transport = transport;
        if (role == SessionRole.Host) _host = new HostSession(this);
        else _client = new ClientSession(this);
    }

    public void Tick()
    {
        if (Role == SessionRole.Client)
        {
            while (Transport.TryReceive(out var frame))
                Router.Dispatch(frame);
        }
        else
        {
            foreach (var (t, r) in _clientTransports)
                while (t.TryReceive(out var frame))
                    r.Dispatch(frame);
        }
    }

    internal void AttachClientTransport(ITransport t, MessageRouter r)
    {
        _clientTransports.Add((t, r));
    }

    public HostSession Host => _host ?? throw new InvalidOperationException("not a host");
    public ClientSession Client => _client ?? throw new InvalidOperationException("not a client");
}
