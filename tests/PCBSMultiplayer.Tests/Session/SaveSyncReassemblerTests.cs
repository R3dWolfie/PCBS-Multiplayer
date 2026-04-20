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
        err.Should().Contain("out-of-range");
    }

    [Fact]
    public void Reassembler_can_be_reused_for_second_transfer_after_success()
    {
        var r = new SaveSyncReassembler();

        // first transfer
        var bytes1 = MakeBytes(1000, seed: 1);
        var p1 = new SaveSyncPacker(bytes1, "s", "sc");
        r.OnBegin(p1.Begin);
        foreach (var c in p1.Chunks) r.OnChunk(c);
        r.OnEnd(p1.End, out var out1, out var err1).Should().BeTrue();
        out1.Should().Equal(bytes1);

        // state should be clean after success — reassembler is reusable
        r.InProgress.Should().BeFalse();
        r.Received.Should().Be(0);
        r.Expected.Should().Be(0);

        // second transfer
        var bytes2 = MakeBytes(5000, seed: 99);
        var p2 = new SaveSyncPacker(bytes2, "s2", "sc2", chunkSize: 2048);
        r.OnBegin(p2.Begin);
        foreach (var c in p2.Chunks) r.OnChunk(c);
        r.OnEnd(p2.End, out var out2, out var err2).Should().BeTrue();
        out2.Should().Equal(bytes2);
    }
}
