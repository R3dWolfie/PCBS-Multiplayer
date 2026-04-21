using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using Xunit;

namespace PCBSMultiplayer.Tests.Net;

public sealed class TransformUpdateRoundTripTests
{
    [Fact]
    public void Packs_and_unpacks_all_fields()
    {
        var sent = new TransformUpdate
        {
            Slot = 3,
            PosX = 1.5f,
            PosY = -2.25f,
            PosZ = 100.125f,
            Yaw = 270f,
            Seq = 42u
        };
        var framed = Serializer.Pack(sent);
        var (tag, msg) = Serializer.Unpack(framed);
        Assert.Equal(TypeTag.TransformUpdate, tag);
        var got = Assert.IsType<TransformUpdate>(msg);
        Assert.Equal(sent.Slot, got.Slot);
        Assert.Equal(sent.PosX, got.PosX);
        Assert.Equal(sent.PosY, got.PosY);
        Assert.Equal(sent.PosZ, got.PosZ);
        Assert.Equal(sent.Yaw, got.Yaw);
        Assert.Equal(sent.Seq, got.Seq);
    }

    [Fact]
    public void Zero_fields_roundtrip()
    {
        var sent = new TransformUpdate();
        var framed = Serializer.Pack(sent);
        var (tag, msg) = Serializer.Unpack(framed);
        Assert.Equal(TypeTag.TransformUpdate, tag);
        var got = Assert.IsType<TransformUpdate>(msg);
        Assert.Equal(0, got.Slot);
        Assert.Equal(0f, got.PosX);
        Assert.Equal(0f, got.PosY);
        Assert.Equal(0f, got.PosZ);
        Assert.Equal(0f, got.Yaw);
        Assert.Equal(0u, got.Seq);
    }
}
