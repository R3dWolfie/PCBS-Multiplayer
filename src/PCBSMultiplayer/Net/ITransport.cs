namespace PCBSMultiplayer.Net;

public interface ITransport
{
    void Send(byte[] payload);
    bool TryReceive(out byte[] payload);
    bool IsConnected { get; }
    void Disconnect();
}
