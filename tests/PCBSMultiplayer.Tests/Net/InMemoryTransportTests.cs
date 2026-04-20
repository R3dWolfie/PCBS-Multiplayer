using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using Xunit;

namespace PCBSMultiplayer.Tests.Net;

public class InMemoryTransportTests
{
    [Fact]
    public void Sending_from_a_puts_bytes_in_b_inbox()
    {
        var (a, b) = InMemoryTransport.CreatePair();
        var payload = Serializer.Pack(new Heartbeat { SentAtMs = 42L });
        a.Send(payload);
        b.TryReceive(out var received).Should().BeTrue();
        received.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void Sending_from_b_puts_bytes_in_a_inbox()
    {
        var (a, b) = InMemoryTransport.CreatePair();
        var payload = Serializer.Pack(new Heartbeat { SentAtMs = 7L });
        b.Send(payload);
        a.TryReceive(out var received).Should().BeTrue();
        received.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void Empty_inbox_returns_false_from_try_receive()
    {
        var (a, _) = InMemoryTransport.CreatePair();
        a.TryReceive(out _).Should().BeFalse();
    }

    [Fact]
    public void Disconnect_prevents_further_sends()
    {
        var (a, b) = InMemoryTransport.CreatePair();
        a.Disconnect();
        a.Invoking(t => t.Send(new byte[] { 1 })).Should().Throw<System.InvalidOperationException>();
    }
}
