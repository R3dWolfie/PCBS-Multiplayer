using PCBSMultiplayer.Net;
using Xunit;

namespace PCBSMultiplayer.Tests.Net;

public sealed class InMemoryTransportUnreliableTests
{
    [Fact]
    public void SendUnreliable_delivers_like_Send_in_memory()
    {
        var (a, b) = InMemoryTransport.CreatePair();
        a.SendUnreliable(new byte[] { 1, 2, 3 }, channel: 1);
        Assert.True(b.TryReceive(out var got));
        Assert.Equal(new byte[] { 1, 2, 3 }, got);
    }
}
