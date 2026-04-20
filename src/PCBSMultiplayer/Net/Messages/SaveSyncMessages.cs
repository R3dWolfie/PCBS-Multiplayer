namespace PCBSMultiplayer.Net.Messages;

public sealed class SaveTransferBegin : IMessage
{
    public string SaveName { get; set; } = "";
    public string SceneName { get; set; } = "";
    public int TotalBytes { get; set; }
    public int ChunkCount { get; set; }
    public int ChunkSize { get; set; }
    public uint Crc32 { get; set; }
    public TypeTag Tag => TypeTag.SaveTransferBegin;
}

public sealed class SaveChunk : IMessage
{
    public int Index { get; set; }
    public byte[] Payload { get; set; } = new byte[0];
    public TypeTag Tag => TypeTag.SaveChunk;
}

public sealed class SaveTransferEnd : IMessage
{
    public TypeTag Tag => TypeTag.SaveTransferEnd;
}
