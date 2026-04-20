using System;

namespace PCBSMultiplayer.Net;

public sealed class SteamTransport : ITransport
{
    public bool IsConnected => throw new NotImplementedException("implemented in Plan 2");
    public void Send(byte[] payload) => throw new NotImplementedException("implemented in Plan 2");
    public bool TryReceive(out byte[] payload) => throw new NotImplementedException("implemented in Plan 2");
    public void Disconnect() => throw new NotImplementedException("implemented in Plan 2");
}
