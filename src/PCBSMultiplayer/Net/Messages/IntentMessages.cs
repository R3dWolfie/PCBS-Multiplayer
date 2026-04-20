using MessagePack;

namespace PCBSMultiplayer.Net.Messages;

[MessagePackObject]
public sealed class ClaimJobRequest : IMessage
{
    [Key(0)] public string JobId { get; set; } = "";
    [Key(1)] public string RequestId { get; set; } = "";
    [IgnoreMember] public TypeTag Tag => TypeTag.ClaimJobRequest;
}

[MessagePackObject]
public sealed class SpendMoneyRequest : IMessage
{
    [Key(0)] public long Amount { get; set; }
    [Key(1)] public string Reason { get; set; } = "";
    [Key(2)] public string RequestId { get; set; } = "";
    [IgnoreMember] public TypeTag Tag => TypeTag.SpendMoneyRequest;
}
