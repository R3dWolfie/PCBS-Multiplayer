using System.Collections.Generic;
using MessagePack;
using PCBSMultiplayer.State;

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

[MessagePackObject]
public sealed class JobBoardDelta : IMessage
{
    [Key(0)] public List<SnapshotBuilder.JobDto> Available { get; set; } = new();
    [Key(1)] public List<SnapshotBuilder.JobDto> Claimed { get; set; } = new();
    [Key(2)] public List<SnapshotBuilder.JobDto> Completed { get; set; } = new();
    [IgnoreMember] public TypeTag Tag => TypeTag.JobBoardDelta;
}
