using System;
using System.Collections.Generic;

namespace PCBSMultiplayer.Net;

public sealed class InMemoryTransport : ITransport
{
    private readonly Queue<byte[]> _inbox = new();
    private readonly object _gate = new();
    private InMemoryTransport? _peer;
    private bool _connected = true;

    public bool IsConnected => _connected;

    public void Send(byte[] payload)
    {
        if (!_connected || _peer == null) throw new InvalidOperationException("transport disconnected");
        lock (_peer._gate) _peer._inbox.Enqueue(payload);
    }

    public bool TryReceive(out byte[] payload)
    {
        lock (_gate)
        {
            if (_inbox.Count > 0) { payload = _inbox.Dequeue(); return true; }
        }
        payload = new byte[0];
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
