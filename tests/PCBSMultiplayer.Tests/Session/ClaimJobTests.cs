using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.Session;
using PCBSMultiplayer.State;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class ClaimJobTests
{
    [Fact]
    public void First_claim_wins_second_is_denied()
    {
        var (hostT1, clientT1) = InMemoryTransport.CreatePair();
        var (hostT2, clientT2) = InMemoryTransport.CreatePair();
        var host = new SessionManager(SessionRole.Host, hostT1);
        host.World.JobBoard.AddAvailable(new Job { Id = "race-job" });
        host.Host.AttachClient(hostT1);
        host.Host.AttachClient(hostT2);

        var alice = new SessionManager(SessionRole.Client, clientT1);
        alice.Client.DisplayName = "alice";
        alice.Client.SayHello();
        var bob = new SessionManager(SessionRole.Client, clientT2);
        bob.Client.DisplayName = "bob";
        bob.Client.SayHello();

        host.Tick();
        alice.Tick(); bob.Tick();

        alice.Transport.Send(Serializer.Pack(new ClaimJobRequest { JobId = "race-job", RequestId = "a1" }));
        bob.Transport.Send(Serializer.Pack(new ClaimJobRequest { JobId = "race-job", RequestId = "b1" }));

        host.Tick();
        alice.Tick(); bob.Tick();

        var aliceAccepted = alice.Client.LastClaimResult!.Accepted;
        var bobAccepted = bob.Client.LastClaimResult!.Accepted;
        (aliceAccepted ^ bobAccepted).Should().BeTrue("exactly one must succeed");
        host.World.JobBoard.Available.Should().BeEmpty();
        host.World.JobBoard.Claimed.Should().ContainKey("race-job");
    }
}
