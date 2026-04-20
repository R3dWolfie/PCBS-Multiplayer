using MessagePack;

namespace PCBSMultiplayer.Net.Messages;

[MessagePackObject]
public sealed class ClaimJobResult : IMessage
{
    [Key(0)] public string RequestId { get; set; } = "";
    [Key(1)] public bool Accepted { get; set; }
    [Key(2)] public string DenyReason { get; set; } = "";
    [IgnoreMember] public TypeTag Tag => TypeTag.ClaimJobResult;
}

[MessagePackObject]
public sealed class SpendMoneyResult : IMessage
{
    [Key(0)] public string RequestId { get; set; } = "";
    [Key(1)] public bool Accepted { get; set; }
    [Key(2)] public string DenyReason { get; set; } = "";
    [IgnoreMember] public TypeTag Tag => TypeTag.SpendMoneyResult;
}
