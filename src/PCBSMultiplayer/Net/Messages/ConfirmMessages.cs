namespace PCBSMultiplayer.Net.Messages;

public sealed class ClaimJobResult : IMessage
{
    public string RequestId { get; set; } = "";
    public bool Accepted { get; set; }
    public string DenyReason { get; set; } = "";
    public TypeTag Tag => TypeTag.ClaimJobResult;
}

public sealed class SpendMoneyResult : IMessage
{
    public string RequestId { get; set; } = "";
    public bool Accepted { get; set; }
    public string DenyReason { get; set; } = "";
    public TypeTag Tag => TypeTag.SpendMoneyResult;
}
