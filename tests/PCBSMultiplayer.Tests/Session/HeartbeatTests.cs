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
}
