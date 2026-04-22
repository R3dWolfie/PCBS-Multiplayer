using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.Session;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class HeartbeatTests
{
    [Fact]
    public void Client_marked_disconnected_if_no_message_in_3_seconds()
    {
        var (hostT, clientT) = InMemoryTransport.CreatePair();
        var host = new SessionManager(SessionRole.Host, hostT);
        host.Host.AttachClient(hostT);
        var client = new SessionManager(SessionRole.Client, clientT);
        client.Client.DisplayName = "ghost"; client.Client.SayHello();
        host.Tick(); client.Tick();

        host.Heartbeat(nowMs: 1000);
        host.Heartbeat(nowMs: 5000);
        host.Host.Clients.Should().ContainKey(1, "still in grace window");
        host.Heartbeat(nowMs: 40000);
        host.Host.Clients.Should().BeEmpty("grace elapsed");
    }

    [Fact]
    public void Recent_activity_keeps_client_connected()
    {
        var (hostT, clientT) = InMemoryTransport.CreatePair();
        var host = new SessionManager(SessionRole.Host, hostT);
        host.Host.AttachClient(hostT);
        var client = new SessionManager(SessionRole.Client, clientT);
        client.Client.DisplayName = "alive"; client.Client.SayHello();
        host.Tick(); client.Tick();

        host.Heartbeat(nowMs: 1000);
        clientT.Send(Serializer.Pack(new Heartbeat { SentAtMs = 3000 }));
        host.Tick();
        host.Heartbeat(nowMs: 3500);

        host.Host.Clients.Should().ContainKey(1);
    }

    [Fact]
    public void Scene_load_time_jump_does_not_false_timeout_when_heartbeats_flowed()
    {
        // Simulates: host tick at 2000, then Unity Update blocks 8s for a scene load, during
        // which the client's Steam networking buffer accumulates heartbeats. On resume at
        // nowMs=10000, Pump drains the backlog and Tick(10000) must stamp last_seen with the
        // FRESH nowMs so the following Heartbeat(10000) doesn't observe a fake 8s gap.
        var (hostT, clientT) = InMemoryTransport.CreatePair();
        var host = new SessionManager(SessionRole.Host, hostT);
        host.Host.AttachClient(hostT);
        var client = new SessionManager(SessionRole.Client, clientT);
        client.Client.DisplayName = "scene-loader"; client.Client.SayHello();
        host.Tick(nowMs: 500); client.Tick();
        host.Heartbeat(nowMs: 2000);

        // 8-second Update pause, client kept sending heartbeats into its Steam buffer.
        clientT.Send(Serializer.Pack(new Heartbeat { SentAtMs = 3000 }));
        clientT.Send(Serializer.Pack(new Heartbeat { SentAtMs = 4000 }));
        clientT.Send(Serializer.Pack(new Heartbeat { SentAtMs = 5000 }));

        host.Tick(nowMs: 10000);
        host.Heartbeat(nowMs: 10000);

        host.Host.Clients.Should().ContainKey(1, "fresh nowMs stamp prevents false timeout");
    }

    [Fact]
    public void Frame_receipt_cancels_grace_started_by_transient_stall()
    {
        // Belt-and-suspenders: even if grace somehow started (e.g., a tick where no frames
        // arrived), a subsequent frame receipt must cancel it so broadcasts keep landing.
        var (hostT, clientT) = InMemoryTransport.CreatePair();
        var host = new SessionManager(SessionRole.Host, hostT);
        host.Host.AttachClient(hostT);
        var client = new SessionManager(SessionRole.Client, clientT);
        client.Client.DisplayName = "recovering"; client.Client.SayHello();
        host.Tick(nowMs: 500); client.Tick();

        host.Heartbeat(nowMs: 1000);
        host.Heartbeat(nowMs: 5000); // grace starts (no frames since 1000)
        host.Host.Clients.Should().ContainKey(1, "still in grace");

        // Client recovers and frames start flowing again within the grace window.
        clientT.Send(Serializer.Pack(new Heartbeat { SentAtMs = 6000 }));
        host.Tick(nowMs: 6000);

        // Grace should be cancelled so a later heartbeat past the 30s mark doesn't wipe the slot.
        host.Heartbeat(nowMs: 40000);
        host.Host.Clients.Should().ContainKey(1, "grace was cancelled on frame receipt");
    }
}
