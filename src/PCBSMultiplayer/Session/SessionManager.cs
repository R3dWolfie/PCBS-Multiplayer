using System;
using System.Collections.Generic;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.State;

namespace PCBSMultiplayer.Session;

public enum SessionRole { Host, Client }

public sealed class SessionManager
{
    public static SessionManager Current { get; set; }
    public static bool ApplyingRemoteDelta { get; set; }

    public SessionRole Role { get; }
    public WorldState World { get; } = new();
    public State.RemotePlayerRegistry RemoteRegistry { get; } = new();
    public MessageRouter Router { get; } = new();
    public ITransport Transport { get; }
    public int LocalSlot { get; internal set; } = -1;
    public bool IsLive { get; internal set; }

    private readonly HostSession _host;
    private readonly ClientSession _client;
    private readonly List<ClientTransportEntry> _clientTransports = new();
    private readonly Dictionary<ITransport, long> _lastSeenMs = new();
    private const long TimeoutMs = 3000;
    private long _lastSeenMsStamp;

    private readonly struct ClientTransportEntry
    {
        public readonly ITransport Transport;
        public readonly MessageRouter Router;
        public ClientTransportEntry(ITransport t, MessageRouter r) { Transport = t; Router = r; }
    }

    public SessionManager(SessionRole role, ITransport transport)
    {
        Role = role;
        Transport = transport;
        if (role == SessionRole.Host) _host = new HostSession(this);
        else _client = new ClientSession(this);
    }

    public void Tick() => Tick(_lastSeenMsStamp);

    // Scene loads (and alt-tab-then-return) block Unity's Update loop for seconds at a time,
    // while Time.unscaledTime keeps advancing at wall-clock. Without a fresh nowMs, Tick
    // would stamp last_seen with the pre-pause timestamp — the very next Heartbeat would
    // then see a multi-TimeoutMs gap on a client whose queued frames just arrived, start a
    // grace timer, and (once grace elapsed) wipe the transport from _transports so every
    // subsequent broadcast went to nobody.
    public void Tick(long nowMs)
    {
        _lastSeenMsStamp = nowMs;
        if (Role == SessionRole.Client)
        {
            while (Transport.TryReceive(out var frame))
                Router.Dispatch(frame);
        }
        else
        {
            foreach (var e in _clientTransports)
                while (e.Transport.TryReceive(out var frame))
                {
                    _lastSeenMs[e.Transport] = nowMs;
                    _host?.CancelGraceFor(e.Transport);
                    e.Router.Dispatch(frame);
                }
        }
    }

    public void Heartbeat(long nowMs)
    {
        _lastSeenMsStamp = nowMs;
        if (_host == null) return;

        _host.SetLastHeartbeat(nowMs);

        var toDisconnect = new List<ITransport>();
        foreach (var e in _clientTransports)
        {
            if (!_lastSeenMs.TryGetValue(e.Transport, out var last))
            {
                _lastSeenMs[e.Transport] = nowMs;
                continue;
            }
            if (nowMs - last > TimeoutMs) toDisconnect.Add(e.Transport);
        }
        foreach (var t in toDisconnect) _host.RemoveClient(t);
        _host.TickGrace(nowMs);
    }

    internal void AttachClientTransport(ITransport t, MessageRouter r)
    {
        _clientTransports.Add(new ClientTransportEntry(t, r));
    }

    public void PumpHostTransports()
    {
        foreach (var e in _clientTransports)
        {
            var st = e.Transport as SteamTransport;
            if (st != null) st.Pump();
        }
    }

    public HostSession Host => _host ?? throw new InvalidOperationException("not a host");
    public ClientSession Client => _client ?? throw new InvalidOperationException("not a client");
}
