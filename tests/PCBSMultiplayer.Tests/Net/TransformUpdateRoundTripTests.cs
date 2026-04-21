using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using Xunit;

namespace PCBSMultiplayer.Tests.Net;

public sealed class TransformUpdateRoundTripTests
{
    [Fact]
    public void TransformUpdate_round_trips_all_fields()
    {
        var sent = new TransformUpdate
        {
            Slot = 255,
            PosX = float.MaxValue,
            PosY = -1.1f,
            PosZ = float.NaN,
            Yaw = float.PositiveInfinity,
            Seq = uint.MaxValue
        };
        var framed = Serializer.Pack(sent);
        framed.Length.Should().Be(22); // 1 byte tag + 21 byte payload (1 + 4 + 4 + 4 + 4 + 4)
        var (tag, msg) = Serializer.Unpack(framed);
        tag.Should().Be(TypeTag.TransformUpdate);
        var got = msg.Should().BeOfType<TransformUpdate>().Subject;
        got.Slot.Should().Be(sent.Slot);
        got.PosX.Should().Be(sent.PosX);
        got.PosY.Should().Be(sent.PosY);
        float.IsNaN(got.PosZ).Should().BeTrue();
        got.Yaw.Should().Be(sent.Yaw);
        got.Seq.Should().Be(sent.Seq);
    }

    [Fact]
    public void TransformUpdate_round_trips_zero_fields()
    {
        var sent = new TransformUpdate();
        var framed = Serializer.Pack(sent);
        var (tag, msg) = Serializer.Unpack(framed);
        tag.Should().Be(TypeTag.TransformUpdate);
        var got = msg.Should().BeOfType<TransformUpdate>().Subject;
        got.Slot.Should().Be((byte)0);
        got.PosX.Should().Be(0f);
        got.PosY.Should().Be(0f);
        got.PosZ.Should().Be(0f);
        got.Yaw.Should().Be(0f);
        got.Seq.Should().Be(0u);
    }
}
