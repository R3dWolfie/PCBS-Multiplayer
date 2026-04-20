using System.Collections.Generic;
using PCBSMultiplayer.State;

namespace PCBSMultiplayer.Net.Messages;

public sealed class MoneyChanged : IMessage
{
    public long NewTotal { get; set; }
    public TypeTag Tag => TypeTag.MoneyChanged;
}

public sealed class XPChanged : IMessage
{
    public long NewTotal { get; set; }
    public TypeTag Tag => TypeTag.XPChanged;
}

public sealed class TimeChanged : IMessage
{
    public int NewDayIndex { get; set; }
    public TypeTag Tag => TypeTag.TimeChanged;
}

public sealed class JobBoardDelta : IMessage
{
    public List<SnapshotBuilder.JobDto> Available { get; set; } = new();
    public List<SnapshotBuilder.JobDto> Claimed { get; set; } = new();
    public List<SnapshotBuilder.JobDto> Completed { get; set; } = new();
    public TypeTag Tag => TypeTag.JobBoardDelta;
}
