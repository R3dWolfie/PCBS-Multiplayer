using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using Xunit;

namespace PCBSMultiplayer.Tests.Net;

public class SaveSyncMessagesTests
{
    [Fact]
    public void SaveTransferBegin_round_trips()
    {
        var original = new SaveTransferBegin
        {
            SaveName = "Multiplayer-Career",
            SceneName = "Workshop_V2",
            TotalBytes = 262144,
            ChunkCount = 32,
            ChunkSize = 8192,
            Crc32 = 0xDEADBEEFu,
        };
        var framed = Serializer.Pack(original);
        var (tag, decoded) = Serializer.Unpack(framed);
        tag.Should().Be(TypeTag.SaveTransferBegin);
        var r = decoded.Should().BeOfType<SaveTransferBegin>().Subject;
        r.SaveName.Should().Be("Multiplayer-Career");
        r.SceneName.Should().Be("Workshop_V2");
        r.TotalBytes.Should().Be(262144);
        r.ChunkCount.Should().Be(32);
        r.ChunkSize.Should().Be(8192);
        r.Crc32.Should().Be(0xDEADBEEFu);
    }

    [Fact]
    public void SaveChunk_round_trips_with_payload()
    {
        var payload = new byte[8192];
        for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i & 0xFF);
        var original = new SaveChunk { Index = 7, Payload = payload };
        var framed = Serializer.Pack(original);
        var (tag, decoded) = Serializer.Unpack(framed);
        tag.Should().Be(TypeTag.SaveChunk);
        var r = decoded.Should().BeOfType<SaveChunk>().Subject;
        r.Index.Should().Be(7);
        r.Payload.Should().Equal(payload);
    }

    [Fact]
    public void SaveChunk_round_trips_with_empty_payload()
    {
        var original = new SaveChunk { Index = 0, Payload = new byte[0] };
        var framed = Serializer.Pack(original);
        var (tag, decoded) = Serializer.Unpack(framed);
        tag.Should().Be(TypeTag.SaveChunk);
        var r = decoded.Should().BeOfType<SaveChunk>().Subject;
        r.Index.Should().Be(0);
        r.Payload.Should().HaveCount(0);
    }

    [Fact]
    public void SaveTransferEnd_round_trips()
    {
        var original = new SaveTransferEnd();
        var framed = Serializer.Pack(original);
        var (tag, decoded) = Serializer.Unpack(framed);
        tag.Should().Be(TypeTag.SaveTransferEnd);
        decoded.Should().BeOfType<SaveTransferEnd>();
    }
}
