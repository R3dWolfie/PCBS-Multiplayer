using System.Linq;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.State;

namespace PCBSMultiplayer.Session;

public sealed class ClientSession
{
    public const string ModVersion = "0.1.0";

    private readonly SessionManager _mgr;
    public string DisplayName { get; set; } = "";
    public ulong SteamId { get; set; }
    public string? DisconnectReason { get; private set; }

    public ClientSession(SessionManager mgr)
    {
        _mgr = mgr;
        _mgr.Router.On<Welcome>(OnWelcome);
        _mgr.Router.On<Bye>(OnBye);
    }

    public void SayHello()
    {
        var hello = new Hello
        {
            ModVersion = ModVersion,
            GameVersion = "1.15.2",
            SteamId = SteamId,
            DisplayName = DisplayName
        };
        _mgr.Transport.Send(Serializer.Pack(hello));
    }

    private void OnWelcome(Welcome w)
    {
        var snapshot = SnapshotBuilder.Deserialize(w.SnapshotBytes);
        _mgr.World.Money = snapshot.Money;
        _mgr.World.XP = snapshot.XP;
        _mgr.World.DayIndex = snapshot.DayIndex;
        _mgr.World.JobBoard.ReplaceAll(
            snapshot.JobBoard.Available.Select(j => new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot }).ToList(),
            snapshot.JobBoard.Claimed.Values.Select(j => new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot }).ToList(),
            snapshot.JobBoard.Completed.Select(j => new SnapshotBuilder.JobDto { Id = j.Id, ClaimedBySlot = j.ClaimedBySlot }).ToList());
        _mgr.LocalSlot = w.AssignedSlot;
        _mgr.IsLive = true;
    }

    private void OnBye(Bye b)
    {
        DisconnectReason = b.Reason;
        _mgr.IsLive = false;
        _mgr.Transport.Disconnect();
    }
}
