using FluentAssertions;
using PCBSMultiplayer.Session;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class SaveSyncReassemblerTests
{
    private static byte[] MakeBytes(int n, int seed = 0)
    {
        var b = new byte[n];
        for (int i = 0; i < n; i++) b[i] = (byte)((i + seed) & 0xFF);
        return b;
    }

    [Fact]
    public void Happy_path_round_trips_bytes()
    {
        var original = MakeBytes(20000);
        var packer = new SaveSyncPacker(original, "s", "sc", chunkSize: 8192);
        var r = new SaveSyncReassembler();

        r.OnBegin(packer.Begin);
        foreach (var c in packer.Chunks) r.OnChunk(c);
        var ok = r.OnEnd(packer.End, out var bytes, out var err);

        ok.Should().BeTrue();
        err.Should().BeNullOrEmpty();
        bytes.Should().Equal(original);
    }

    [Fact]
    public void In_progress_tracking_reflects_received_count()
    {
        var original = MakeBytes(20000);
        var packer = new SaveSyncPacker(original, "s", "sc", chunkSize: 8192);
        var r = new SaveSyncReassembler();

        r.InProgress.Should().BeFalse();
        r.OnBegin(packer.Begin);
        r.InProgress.Should().BeTrue();
        r.Expected.Should().Be(3);
        r.Received.Should().Be(0);

        r.OnChunk(packer.Chunks[0]);
        r.Received.Should().Be(1);
        r.OnChunk(packer.Chunks[1]);
        r.Received.Should().Be(2);
    }

    [Fact]
    public void Crc_mismatch_rejected_on_end()
    {
        var original = MakeBytes(1000);
        var packer = new SaveSyncPacker(original, "s", "sc");
        var r = new SaveSyncReassembler();

        r.OnBegin(packer.Begin);
        // corrupt one chunk byte
        var corrupt = new PCBSMultiplayer.Net.Messages.SaveChunk
        {
            Index = packer.Chunks[0].Index,
            Payload = (byte[])packer.Chunks[0].Payload.Clone(),
        };
        corrupt.Payload[0] ^= 0xFF;
        r.OnChunk(corrupt);
        var ok = r.OnEnd(packer.End, out var bytes, out var err);

        ok.Should().BeFalse();
        err.Should().Contain("crc");
        bytes.Should().BeNull();
    }

    [Fact]
    public void Missing_chunk_rejected_on_end()
    {
        var original = MakeBytes(20000);
        var packer = new SaveSyncPacker(original, "s", "sc", chunkSize: 8192);
        var r = new SaveSyncReassembler();

        r.OnBegin(packer.Begin);
        r.OnChunk(packer.Chunks[0]);
        r.OnChunk(packer.Chunks[1]);
        // skip index 2
        var ok = r.OnEnd(packer.End, out var bytes, out var err);

        ok.Should().BeFalse();
        err.Should().Contain("chunk");
        bytes.Should().BeNull();
    }

    [Fact]
    public void Reset_clears_state()
    {
        var original = MakeBytes(1000);
        var packer = new SaveSyncPacker(original, "s", "sc");
        var r = new SaveSyncReassembler();
        r.OnBegin(packer.Begin);
        r.OnChunk(packer.Chunks[0]);
        r.InProgress.Should().BeTrue();

        r.Reset();

        r.InProgress.Should().BeFalse();
        r.Received.Should().Be(0);
        r.Expected.Should().Be(0);
    }

    [Fact]
    public void Empty_save_succeeds_with_zero_chunks()
    {
        var packer = new SaveSyncPacker(new byte[0], "s", "sc");
        var r = new SaveSyncReassembler();
        r.OnBegin(packer.Begin);
        var ok = r.OnEnd(packer.End, out var bytes, out var err);
        ok.Should().BeTrue();
        err.Should().BeNullOrEmpty();
        bytes.Should().HaveCount(0);
    }

    [Fact]
    public void Out_of_range_chunk_index_rejected_on_end()
    {
        var original = MakeBytes(10000);
        var packer = new SaveSyncPacker(original, "s", "sc", chunkSize: 8192);
        var r = new SaveSyncReassembler();
        r.OnBegin(packer.Begin);
        // index 5 — out of range for a 2-chunk transfer
        r.OnChunk(new PCBSMultiplayer.Net.Messages.SaveChunk { Index = 5, Payload = new byte[8192] });
        var ok = r.OnEnd(packer.End, out var bytes, out var err);
        ok.Should().BeFalse();
        err.Should().NotBeNullOrEmpty();
    }
}
