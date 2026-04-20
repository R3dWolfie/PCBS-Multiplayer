using System.Linq;
using FluentAssertions;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.Session;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class SaveSyncPackerTests
{
    [Fact]
    public void Begin_metadata_matches_inputs()
    {
        var bytes = new byte[20000];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i % 251);

        var packer = new SaveSyncPacker(bytes, "auto", "Workshop_V2", chunkSize: 8192);

        packer.Begin.SaveName.Should().Be("auto");
        packer.Begin.SceneName.Should().Be("Workshop_V2");
        packer.Begin.TotalBytes.Should().Be(20000);
        packer.Begin.ChunkSize.Should().Be(8192);
        packer.Begin.ChunkCount.Should().Be(3); // ceil(20000/8192)
        packer.Begin.Crc32.Should().Be(Crc32.Compute(bytes));
    }

    [Fact]
    public void Chunk_count_math_handles_exact_multiples()
    {
        var bytes = new byte[16384]; // 2 × 8192
        var packer = new SaveSyncPacker(bytes, "s", "sc", chunkSize: 8192);
        packer.Begin.ChunkCount.Should().Be(2);
        packer.Chunks.Should().HaveCount(2);
        packer.Chunks[0].Payload.Length.Should().Be(8192);
        packer.Chunks[1].Payload.Length.Should().Be(8192);
    }

    [Fact]
    public void Last_chunk_is_truncated_when_total_is_not_a_multiple()
    {
        var bytes = new byte[10000];
        var packer = new SaveSyncPacker(bytes, "s", "sc", chunkSize: 8192);
        packer.Chunks.Should().HaveCount(2);
        packer.Chunks[0].Payload.Length.Should().Be(8192);
        packer.Chunks[1].Payload.Length.Should().Be(10000 - 8192);
    }

    [Fact]
    public void Chunk_payload_bytes_match_source_ranges()
    {
        var bytes = new byte[10000];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i & 0xFF);
        var packer = new SaveSyncPacker(bytes, "s", "sc", chunkSize: 4096);
        var reassembled = packer.Chunks.SelectMany(c => c.Payload).ToArray();
        reassembled.Should().Equal(bytes);
    }

    [Fact]
    public void Indices_are_zero_based_and_sequential()
    {
        var bytes = new byte[10000];
        var packer = new SaveSyncPacker(bytes, "s", "sc", chunkSize: 4096);
        packer.Chunks.Select(c => c.Index).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void Empty_input_produces_zero_chunks_and_zero_crc()
    {
        var packer = new SaveSyncPacker(new byte[0], "s", "sc");
        packer.Begin.TotalBytes.Should().Be(0);
        packer.Begin.ChunkCount.Should().Be(0);
        packer.Chunks.Should().HaveCount(0);
        packer.Begin.Crc32.Should().Be(Crc32.Compute(new byte[0]));
    }

    [Fact]
    public void Crc32_known_vector_matches_ieee_reference()
    {
        // RFC 3720 / zlib known value: CRC32 of "123456789" (ASCII) = 0xCBF43926
        var input = System.Text.Encoding.ASCII.GetBytes("123456789");
        Crc32.Compute(input).Should().Be(0xCBF43926u);
    }
}
