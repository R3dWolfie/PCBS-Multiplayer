using MessagePack;

namespace PCBSMultiplayer.Net.Messages;

[MessagePackObject]
public sealed class Heartbeat : IMessage
{
    [Key(0)] public long SentAtMs { get; set; }
    [IgnoreMember] public TypeTag Tag => TypeTag.Heartbeat;
}
