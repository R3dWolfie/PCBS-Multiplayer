using System;
using System.Collections.Concurrent;

namespace PCBSMultiplayer.Net;

public sealed class InMemoryTransport : ITransport
{
    private readonly ConcurrentQueue<byte[]> _inbox = new();
    private InMemoryTransport? _peer;
    private bool _connected = true;

    public bool IsConnected => _connected;

    public void Send(byte[] payload)
    {
        if (!_connected || _peer == null) throw new InvalidOperationException("transport disconnected");
        _peer._inbox.Enqueue(payload);
    }

    public bool TryReceive(out byte[] payload)
    {
        if (_inbox.TryDequeue(out var p)) { payload = p; return true; }
        payload = Array.Empty<byte>();
        return false;
    }

    public void Disconnect()
    {
        _connected = false;
        if (_peer != null) _peer._connected = false;
    }

    public static (InMemoryTransport a, InMemoryTransport b) CreatePair()
    {
        var a = new InMemoryTransport();
        var b = new InMemoryTransport();
        a._peer = b;
        b._peer = a;
        return (a, b);
    }
}
