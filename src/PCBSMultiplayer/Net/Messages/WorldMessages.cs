using MessagePack;

namespace PCBSMultiplayer.Net.Messages;

[MessagePackObject]
public sealed class MoneyChanged : IMessage
{
    [Key(0)] public long NewTotal { get; set; }
    [IgnoreMember] public TypeTag Tag => TypeTag.MoneyChanged;
}

[MessagePackObject]
public sealed class XPChanged : IMessage
{
    [Key(0)] public long NewTotal { get; set; }
    [IgnoreMember] public TypeTag Tag => TypeTag.XPChanged;
}

[MessagePackObject]
public sealed class TimeChanged : IMessage
{
    [Key(0)] public int NewDayIndex { get; set; }
    [IgnoreMember] public TypeTag Tag => TypeTag.TimeChanged;
}
