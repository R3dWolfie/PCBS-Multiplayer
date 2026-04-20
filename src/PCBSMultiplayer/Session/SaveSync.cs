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
