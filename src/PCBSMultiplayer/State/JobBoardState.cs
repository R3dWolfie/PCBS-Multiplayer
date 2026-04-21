using System.Collections.Generic;

namespace PCBSMultiplayer.State;

public sealed class Job
{
    public string Id { get; set; } = "";
    public int ClaimedBySlot { get; set; } = -1;
}

public sealed class JobBoardState
{
    private readonly List<Job> _available = new();
    private readonly Dictionary<string, Job> _claimed = new();
    private readonly List<Job> _completed = new();

    public List<Job> Available => _available;
    public Dictionary<string, Job> Claimed => _claimed;
    public List<Job> Completed => _completed;

    public void AddAvailable(Job job) => _available.Add(job);

    public bool TryClaim(string jobId, int slot)
    {
        var idx = _available.FindIndex(j => j.Id == jobId);
        if (idx < 0) return false;
        var job = _available[idx];
        _available.RemoveAt(idx);
        job.ClaimedBySlot = slot;
        _claimed[jobId] = job;
        return true;
    }

    public void Release(string jobId)
    {
        if (!_claimed.TryGetValue(jobId, out var job)) return;
        _claimed.Remove(jobId);
        job.ClaimedBySlot = -1;
        _available.Add(job);
    }

    public void Complete(string jobId)
    {
        if (!_claimed.TryGetValue(jobId, out var job)) return;
        _claimed.Remove(jobId);
        _completed.Add(job);
    }

    public void ReplaceAll(
        List<SnapshotBuilder.JobDto> available,
        List<SnapshotBuilder.JobDto> claimed,
        List<SnapshotBuilder.JobDto> completed)
    {
        _available.Clear();
        _claimed.Clear();
        _completed.Clear();
        foreach (var j in available) _available.Add(new Job { Id = j.Id });
        foreach (var j in claimed) _claimed[j.Id] = new Job { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot };
        foreach (var j in completed) _completed.Add(new Job { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot });
    }
}
