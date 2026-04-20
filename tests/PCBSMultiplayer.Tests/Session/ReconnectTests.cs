using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.Session;
using PCBSMultiplayer.State;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class ReconnectTests
{
    [Fact]
    public void Reconnect_within_grace_restores_slot_and_keeps_claimed_job()
    {
        var (hostT, clientT) = InMemoryTransport.CreatePair();
        var host = new SessionManager(SessionRole.Host, hostT);
        host.World.JobBoard.AddAvailable(new Job { Id = "long-job" });
        host.Host.AttachClient(hostT);
        var client = new SessionManager(SessionRole.Client, clientT);
        client.Client.DisplayName = "flaky"; client.Client.SteamId = 76561198000000042;
        client.Client.SayHello();
        host.Tick(); client.Tick();

        clientT.Send(Serializer.Pack(new ClaimJobRequest { JobId = "long-job", RequestId = "q" }));
        host.Tick();
        var originalSlot = client.LocalSlot;
        host.World.JobBoard.Claimed.Should().ContainKey("long-job");

        host.Heartbeat(nowMs: 1000);
        host.Heartbeat(nowMs: 5000);
        host.Host.Clients.Should().ContainKey(originalSlot, "still in grace");

        var (hostT2, clientT2) = InMemoryTransport.CreatePair();
        host.Host.AttachClient(hostT2);
        var client2 = new SessionManager(SessionRole.Client, clientT2);
        client2.Client.DisplayName = "flaky"; client2.Client.SteamId = 76561198000000042;
        client2.Client.SayHello();
        host.Heartbeat(nowMs: 10000);
        host.Tick(); client2.Tick();

        client2.LocalSlot.Should().Be(originalSlot, "same SteamId within grace keeps slot");
        host.World.JobBoard.Claimed.Should().ContainKey("long-job", "reconnect preserves claims");
        host.Host.GraceTimer.Should().NotBeNull();

        host.Heartbeat(nowMs: 45000);
        host.World.JobBoard.Claimed.Should().ContainKey("long-job", "grace was cancelled on reconnect");
    }

    [Fact]
    public void Reconnect_after_grace_elapsed_is_a_fresh_join()
    {
        var (hostT, clientT) = InMemoryTransport.CreatePair();
        var host = new SessionManager(SessionRole.Host, hostT);
        host.World.JobBoard.AddAvailable(new Job { Id = "timed-job" });
        host.Host.AttachClient(hostT);
        var client = new SessionManager(SessionRole.Client, clientT);
        client.Client.DisplayName = "late"; client.Client.SteamId = 7777;
        client.Client.SayHello();
        host.Tick(); client.Tick();

        clientT.Send(Serializer.Pack(new ClaimJobRequest { JobId = "timed-job", RequestId = "q" }));
        host.Tick();

        host.Heartbeat(nowMs: 5000);
        host.Heartbeat(nowMs: 40000);
        host.World.JobBoard.Claimed.Should().BeEmpty("grace elapsed, claim released");

        var (hostT2, clientT2) = InMemoryTransport.CreatePair();
        host.Host.AttachClient(hostT2);
        var client2 = new SessionManager(SessionRole.Client, clientT2);
        client2.Client.DisplayName = "late"; client2.Client.SteamId = 7777;
        client2.Client.SayHello();
        host.Tick(); client2.Tick();

        client2.LocalSlot.Should().NotBe(-1);
        host.Host.Clients.Should().ContainKey(client2.LocalSlot);
    }
}
