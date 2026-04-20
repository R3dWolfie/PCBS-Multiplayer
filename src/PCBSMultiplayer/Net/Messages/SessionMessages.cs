namespace PCBSMultiplayer.Net.Messages;

public sealed class Heartbeat : IMessage
{
    public long SentAtMs { get; set; }
    public TypeTag Tag => TypeTag.Heartbeat;
}

public sealed class Hello : IMessage
{
    public string ModVersion { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public ulong SteamId { get; set; }
    public string DisplayName { get; set; } = "";
    public TypeTag Tag => TypeTag.Hello;
}

public sealed class Welcome : IMessage
{
    public int AssignedSlot { get; set; }
    public byte[] SnapshotBytes { get; set; } = new byte[0];
    public TypeTag Tag => TypeTag.Welcome;
}

public sealed class Bye : IMessage
{
    public string Reason { get; set; } = "";
    public TypeTag Tag => TypeTag.Bye;
}
