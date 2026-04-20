using MessagePack;

namespace PCBSMultiplayer.Net.Messages;

[MessagePackObject]
public sealed class Heartbeat : IMessage
{
    [Key(0)] public long SentAtMs { get; set; }
    [IgnoreMember] public TypeTag Tag => TypeTag.Heartbeat;
}

[MessagePackObject]
public sealed class Hello : IMessage
{
    [Key(0)] public string ModVersion { get; set; } = "";
    [Key(1)] public string GameVersion { get; set; } = "";
    [Key(2)] public ulong SteamId { get; set; }
    [Key(3)] public string DisplayName { get; set; } = "";
    [IgnoreMember] public TypeTag Tag => TypeTag.Hello;
}

[MessagePackObject]
public sealed class Welcome : IMessage
{
    [Key(0)] public int AssignedSlot { get; set; }
    [Key(1)] public byte[] SnapshotBytes { get; set; } = System.Array.Empty<byte>();
    [IgnoreMember] public TypeTag Tag => TypeTag.Welcome;
}

[MessagePackObject]
public sealed class Bye : IMessage
{
    [Key(0)] public string Reason { get; set; } = "";
    [IgnoreMember] public TypeTag Tag => TypeTag.Bye;
}
