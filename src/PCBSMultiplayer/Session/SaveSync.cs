using System;
using System.Collections.Generic;
using PCBSMultiplayer.Net.Messages;

namespace PCBSMultiplayer.Session;

public static class Crc32
{
    private static readonly uint[] _table = BuildTable();

    private static uint[] BuildTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = ((c & 1) != 0) ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
            t[i] = c;
        }
        return t;
    }

    public static uint Compute(byte[] bytes)
    {
        uint c = 0xFFFFFFFFu;
        for (int i = 0; i < bytes.Length; i++)
            c = _table[(c ^ bytes[i]) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}

public sealed class SaveSyncPacker
{
    public SaveTransferBegin Begin { get; }
    public List<SaveChunk> Chunks { get; }
    public SaveTransferEnd End { get; } = new SaveTransferEnd();

    public SaveSyncPacker(byte[] saveBytes, string saveName, string sceneName, int chunkSize = 8192)
    {
        if (saveBytes == null) throw new ArgumentNullException(nameof(saveBytes));
        if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));

        int total = saveBytes.Length;
        int count = total == 0 ? 0 : (total + chunkSize - 1) / chunkSize;

        Begin = new SaveTransferBegin
        {
            SaveName = saveName ?? "",
            SceneName = sceneName ?? "",
            TotalBytes = total,
            ChunkCount = count,
            ChunkSize = chunkSize,
            Crc32 = Crc32.Compute(saveBytes),
        };

        Chunks = new List<SaveChunk>(count);
        for (int i = 0; i < count; i++)
        {
            int offset = i * chunkSize;
            int len = Math.Min(chunkSize, total - offset);
            var payload = new byte[len];
            Buffer.BlockCopy(saveBytes, offset, payload, 0, len);
            Chunks.Add(new SaveChunk { Index = i, Payload = payload });
        }
    }
}

public sealed class SaveSyncReassembler
{
    private SaveTransferBegin _begin;
    private byte[][] _slots; // indexed by chunk index; nulls mean missing
    private int _received;

    public bool InProgress { get { return _begin != null; } }
    public int Received { get { return _received; } }
    public int Expected { get { return _begin == null ? 0 : _begin.ChunkCount; } }

    public void OnBegin(SaveTransferBegin msg)
    {
        if (msg == null) throw new ArgumentNullException(nameof(msg));
        _begin = msg;
        _slots = msg.ChunkCount > 0 ? new byte[msg.ChunkCount][] : new byte[0][];
        _received = 0;
    }

    public void OnChunk(SaveChunk msg)
    {
        if (msg == null) throw new ArgumentNullException(nameof(msg));
        if (_begin == null) return; // no-op before Begin
        if (msg.Index < 0 || msg.Index >= _slots.Length) return; // out of range; Reject at End
        if (_slots[msg.Index] != null) return; // duplicate; keep first
        _slots[msg.Index] = msg.Payload ?? new byte[0];
        _received++;
    }

    public bool OnEnd(SaveTransferEnd msg, out byte[] bytes, out string err)
    {
        bytes = null;
        err = null;
        if (_begin == null) { err = "no transfer in progress"; return false; }

        if (_received != _begin.ChunkCount)
        {
            err = "chunk count mismatch: received " + _received + " of " + _begin.ChunkCount;
            return false;
        }

        var assembled = new byte[_begin.TotalBytes];
        int offset = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            var p = _slots[i];
            if (p == null) { err = "missing chunk " + i; return false; }
            if (offset + p.Length > assembled.Length) { err = "chunk " + i + " overruns buffer"; return false; }
            Buffer.BlockCopy(p, 0, assembled, offset, p.Length);
            offset += p.Length;
        }

        uint crc = Crc32.Compute(assembled);
        if (crc != _begin.Crc32)
        {
            err = "crc mismatch: got " + crc.ToString("X8") + " expected " + _begin.Crc32.ToString("X8");
            return false;
        }

        bytes = assembled;
        return true;
    }

    public void Reset()
    {
        _begin = null;
        _slots = null;
        _received = 0;
    }
}
