using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.Session;
using PCBSMultiplayer.State;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class DisconnectTests
{
    [Fact]
    public void Dropped_client_releases_claimed_job_after_30_seconds()
    {
        var (hostT, clientT) = InMemoryTransport.CreatePair();
        var host = new SessionManager(SessionRole.Host, hostT);
        host.World.JobBoard.AddAvailable(new Job { Id = "orphan-job" });
        host.Host.AttachClient(hostT);
        var client = new SessionManager(SessionRole.Client, clientT);
        client.Client.DisplayName = "gone"; client.Client.SayHello();
        host.Tick(); client.Tick();

        client.Transport.Send(Serializer.Pack(new ClaimJobRequest { JobId = "orphan-job", RequestId = "q" }));
        host.Tick();

        host.World.JobBoard.Claimed.Should().ContainKey("orphan-job");
        host.Heartbeat(nowMs: 1000);
        host.Heartbeat(nowMs: 5000);
        host.World.JobBoard.Claimed.Should().ContainKey("orphan-job", "still in grace");
        host.Heartbeat(nowMs: 40000);
        host.World.JobBoard.Claimed.Should().BeEmpty();
        host.World.JobBoard.Available.Should().ContainSingle(j => j.Id == "orphan-job");
    }
}
