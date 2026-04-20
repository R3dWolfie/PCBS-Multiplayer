using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PCBSMultiplayer.State;

public static class SnapshotBuilder
{
    public static byte[] Serialize(WorldState state)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8);
        w.Write(state.Money);
        w.Write(state.XP);
        w.Write(state.DayIndex);
        WriteJobs(w, state.JobBoard.Available.Select(Dto));
        WriteJobs(w, state.JobBoard.Claimed.Values.Select(Dto));
        WriteJobs(w, state.JobBoard.Completed.Select(Dto));
        w.Flush();
        return ms.ToArray();
    }

    public static WorldState Deserialize(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes, false);
        using var r = new BinaryReader(ms, Encoding.UTF8);
        var state = new WorldState
        {
            Money = r.ReadInt64(),
            XP = r.ReadInt64(),
            DayIndex = r.ReadInt32()
        };
        foreach (var j in ReadJobs(r)) state.JobBoard.AddAvailable(new Job { Id = j.Id });
        foreach (var j in ReadJobs(r))
        {
            state.JobBoard.AddAvailable(new Job { Id = j.Id });
            state.JobBoard.TryClaim(j.Id, j.ClaimedBySlot);
        }
        foreach (var j in ReadJobs(r))
        {
            state.JobBoard.AddAvailable(new Job { Id = j.Id });
            state.JobBoard.TryClaim(j.Id, j.ClaimedBySlot);
            state.JobBoard.Complete(j.Id);
        }
        return state;
    }

    private static JobDto Dto(Job j) => new() { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot };

    private static void WriteJobs(BinaryWriter w, IEnumerable<JobDto> jobs)
    {
        var list = jobs.ToList();
        w.Write(list.Count);
        foreach (var j in list) { w.Write(j.Id); w.Write(j.ClaimedBySlot); }
    }

    private static List<JobDto> ReadJobs(BinaryReader r)
    {
        var n = r.ReadInt32();
        var list = new List<JobDto>(n);
        for (int i = 0; i < n; i++)
            list.Add(new JobDto { Id = r.ReadString(), ClaimedBySlot = r.ReadInt32() });
        return list;
    }

    public sealed class JobDto
    {
        public string Id { get; set; } = "";
        public int ClaimedBySlot { get; set; } = -1;
    }
}
