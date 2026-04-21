namespace PCBSMultiplayer.Net;

public interface ITransport
{
    void Send(byte[] payload);
    // Presence / transform updates. Channel 1 on Steam; reliability downgraded to
    // EP2PSendUnreliable. InMemory impl ignores both params and aliases to Send.
    void SendUnreliable(byte[] payload, int channel);
    bool TryReceive(out byte[] payload);
    bool IsConnected { get; }
    void Disconnect();
}
