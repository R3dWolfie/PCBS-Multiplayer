using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using Xunit;

namespace PCBSMultiplayer.Tests.Net;

public class MessageRouterTests
{
    [Fact]
    public void Registered_handler_receives_decoded_message()
    {
        var router = new MessageRouter();
        Heartbeat? received = null;
        router.On<Heartbeat>(m => received = m);
        var frame = Serializer.Pack(new Heartbeat { SentAtMs = 99L });
        router.Dispatch(frame);
        received.Should().NotBeNull();
        received!.SentAtMs.Should().Be(99L);
    }

    [Fact]
    public void Unknown_tag_does_not_throw_just_drops()
    {
        var router = new MessageRouter();
        var frame = new byte[] { 99, 0, 0 };
        router.Invoking(r => r.Dispatch(frame)).Should().NotThrow();
    }

    [Fact]
    public void Multiple_handlers_for_same_tag_are_replaced_not_stacked()
    {
        var router = new MessageRouter();
        int calls = 0;
        router.On<Heartbeat>(_ => calls += 1);
        router.On<Heartbeat>(_ => calls += 10);
        var frame = Serializer.Pack(new Heartbeat { SentAtMs = 0L });
        router.Dispatch(frame);
        calls.Should().Be(10);
    }
}
