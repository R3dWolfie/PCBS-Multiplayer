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

    [Fact]
    public void Hello_round_trips()
    {
        var original = new Hello { ModVersion = "0.1.0", GameVersion = "1.15.2", SteamId = 76561198000000000UL, DisplayName = "alice" };
        var bytes = Serializer.Pack(original);
        var (tag, obj) = Serializer.Unpack(bytes);
        tag.Should().Be(TypeTag.Hello);
        var hello = obj.Should().BeOfType<Hello>().Subject;
        hello.ModVersion.Should().Be("0.1.0");
        hello.GameVersion.Should().Be("1.15.2");
        hello.SteamId.Should().Be(76561198000000000UL);
        hello.DisplayName.Should().Be("alice");
    }

    [Fact]
    public void Welcome_round_trips_with_empty_snapshot()
    {
        var original = new Welcome { AssignedSlot = 2, SnapshotBytes = new byte[] { 1, 2, 3 } };
        var bytes = Serializer.Pack(original);
        var (tag, obj) = Serializer.Unpack(bytes);
        tag.Should().Be(TypeTag.Welcome);
        var w = obj.Should().BeOfType<Welcome>().Subject;
        w.AssignedSlot.Should().Be(2);
        w.SnapshotBytes.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void Bye_round_trips_with_reason()
    {
        var original = new Bye { Reason = "version_mismatch" };
        var bytes = Serializer.Pack(original);
        var (tag, obj) = Serializer.Unpack(bytes);
        tag.Should().Be(TypeTag.Bye);
        obj.Should().BeOfType<Bye>().Which.Reason.Should().Be("version_mismatch");
    }
}
