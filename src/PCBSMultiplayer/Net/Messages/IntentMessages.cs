namespace PCBSMultiplayer.Net.Messages;

public sealed class ClaimJobRequest : IMessage
{
    public string JobId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public TypeTag Tag => TypeTag.ClaimJobRequest;
}

public sealed class SpendMoneyRequest : IMessage
{
    public long Amount { get; set; }
    public string Reason { get; set; } = "";
    public string RequestId { get; set; } = "";
    public TypeTag Tag => TypeTag.SpendMoneyRequest;
}
