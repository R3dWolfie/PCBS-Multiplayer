using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using Xunit;

namespace PCBSMultiplayer.Tests.Net;

public class SerializerTests
{
    [Fact]
    public void Pack_then_unpack_returns_equivalent_heartbeat()
    {
        var original = new Heartbeat { SentAtMs = 1234567890L };
        var bytes = Serializer.Pack(original);
        var (tag, obj) = Serializer.Unpack(bytes);
        tag.Should().Be(TypeTag.Heartbeat);
        var hb = obj.Should().BeOfType<Heartbeat>().Subject;
        hb.SentAtMs.Should().Be(1234567890L);
    }

    [Fact]
    public void Packed_message_starts_with_type_tag_byte()
    {
        var bytes = Serializer.Pack(new Heartbeat { SentAtMs = 0L });
        bytes[0].Should().Be((byte)TypeTag.Heartbeat);
    }
}
