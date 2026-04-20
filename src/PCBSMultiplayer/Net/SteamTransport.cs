using System;
using System.Collections.Generic;
using Steamworks;

namespace PCBSMultiplayer.Net;

public sealed class SteamTransport : ITransport
{
    private CSteamID _remote;
    private bool _connected;
    private readonly Queue<byte[]> _inbox = new();
    private Callback<P2PSessionRequest_t>? _onRequest;
    private Callback<P2PSessionConnectFail_t>? _onConnectFail;
    private const int Channel = 0;

    public bool IsConnected => _connected;

    public SteamTransport(CSteamID remote)
    {
        _remote = remote;
        _onRequest ??= Callback<P2PSessionRequest_t>.Create(ev => {
            if (ev.m_steamIDRemote == _remote)
                SteamNetworking.AcceptP2PSessionWithUser(_remote);
        });
        _onConnectFail ??= Callback<P2PSessionConnectFail_t>.Create(ev => {
            if (ev.m_steamIDRemote == _remote) _connected = false;
        });
        _connected = true;
    }

    public void Send(byte[] payload)
    {
        if (!_connected) throw new InvalidOperationException("transport disconnected");
        SteamNetworking.SendP2PPacket(_remote, payload, (uint)payload.Length, EP2PSend.k_EP2PSendReliable, Channel);
    }

    public bool TryReceive(out byte[] payload)
    {
        if (_inbox.Count > 0) { payload = _inbox.Dequeue(); return true; }
        payload = new byte[0];
        return false;
    }

    public void Pump()
    {
        while (SteamNetworking.IsP2PPacketAvailable(out var size, Channel))
        {
            var buf = new byte[size];
            if (SteamNetworking.ReadP2PPacket(buf, size, out var read, out var sender, Channel))
            {
                if (sender == _remote) _inbox.Enqueue(buf);
            }
        }
    }

    public void Disconnect()
    {
        if (_connected) SteamNetworking.CloseP2PSessionWithUser(_remote);
        _connected = false;
    }
}
