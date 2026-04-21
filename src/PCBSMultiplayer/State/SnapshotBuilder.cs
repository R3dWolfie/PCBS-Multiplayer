using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PCBSMultiplayer.State;

public static class SnapshotBuilder
{
    // No LINQ / Func`2: Mono 2018 mscorlib 4.0.0.0 cannot JIT methods that reference Func<T,TResult>,
    // so .Select(Dto) crashes HostSession.OnHello on the live runtime with
    // "Could not load type 'System.Func`2' from assembly 'mscorlib'".
    public static byte[] Serialize(WorldState state)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8);
        w.Write(state.Money);
        w.Write(state.XP);
        w.Write(state.DayIndex);
        WriteAvailable(w, state.JobBoard.Available);
        WriteClaimed(w, state.JobBoard.Claimed.Values);
        WriteAvailable(w, state.JobBoard.Completed);
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

    private static void WriteAvailable(BinaryWriter w, List<Job> jobs)
    {
        w.Write(jobs.Count);
        foreach (var j in jobs) { w.Write(j.Id); w.Write(j.ClaimedBySlot); }
    }

    private static void WriteClaimed(BinaryWriter w, Dictionary<string, Job>.ValueCollection jobs)
    {
        w.Write(jobs.Count);
        foreach (var j in jobs) { w.Write(j.Id); w.Write(j.ClaimedBySlot); }
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
