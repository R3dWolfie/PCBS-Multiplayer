using System.Collections.Generic;
using System.Linq;
using MessagePack;

namespace PCBSMultiplayer.State;

public static class SnapshotBuilder
{
    public static byte[] Serialize(WorldState state)
    {
        var dto = new Snapshot
        {
            Money = state.Money,
            XP = state.XP,
            DayIndex = state.DayIndex,
            Available = state.JobBoard.Available.Select(j => new JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot }).ToList(),
            Claimed = state.JobBoard.Claimed.Values.Select(j => new JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot }).ToList(),
            Completed = state.JobBoard.Completed.Select(j => new JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot }).ToList()
        };
        return MessagePackSerializer.Serialize(dto);
    }

    public static WorldState Deserialize(byte[] bytes)
    {
        var dto = MessagePackSerializer.Deserialize<Snapshot>(bytes);
        var state = new WorldState { Money = dto.Money, XP = dto.XP, DayIndex = dto.DayIndex };
        foreach (var j in dto.Available) state.JobBoard.AddAvailable(new Job { Id = j.Id });
        foreach (var j in dto.Claimed)
        {
            state.JobBoard.AddAvailable(new Job { Id = j.Id });
            state.JobBoard.TryClaim(j.Id, j.ClaimedBySlot);
        }
        foreach (var j in dto.Completed)
        {
            state.JobBoard.AddAvailable(new Job { Id = j.Id });
            state.JobBoard.TryClaim(j.Id, j.ClaimedBySlot);
            state.JobBoard.Complete(j.Id);
        }
        return state;
    }

    [MessagePackObject]
    public sealed class Snapshot
    {
        [Key(0)] public long Money { get; set; }
        [Key(1)] public long XP { get; set; }
        [Key(2)] public int DayIndex { get; set; }
        [Key(3)] public List<JobDto> Available { get; set; } = new();
        [Key(4)] public List<JobDto> Claimed { get; set; } = new();
        [Key(5)] public List<JobDto> Completed { get; set; } = new();
    }

    [MessagePackObject]
    public sealed class JobDto
    {
        [Key(0)] public string Id { get; set; } = "";
        [Key(1)] public int ClaimedBySlot { get; set; } = -1;
    }
}
