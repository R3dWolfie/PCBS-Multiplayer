using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.Session;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class HostSessionClientAcceptedTests
{
    [Fact]
    public void ClientAccepted_fires_after_Welcome_sent_for_new_client()
    {
        var (hostT, clientT) = InMemoryTransport.CreatePair();
        var mgr = new SessionManager(SessionRole.Host, hostT);
        int? acceptedSlot = null;
        mgr.Host.ClientAccepted += slot => acceptedSlot = slot;

        mgr.Host.AttachClient(hostT);

        var hello = new Hello { ModVersion = HostSession.ModVersion, GameVersion = "1.15.2", SteamId = 42, DisplayName = "tester" };
        clientT.Send(Serializer.Pack(hello));

        mgr.Tick();

        acceptedSlot.Should().NotBeNull();
        acceptedSlot.Value.Should().BeGreaterThan(0);
        mgr.Host.Clients.Should().ContainKey(acceptedSlot.Value);
    }

    [Fact]
    public void ClientAccepted_does_not_fire_on_version_mismatch()
    {
        var (hostT, clientT) = InMemoryTransport.CreatePair();
        var mgr = new SessionManager(SessionRole.Host, hostT);
        bool fired = false;
        mgr.Host.ClientAccepted += _ => fired = true;

        mgr.Host.AttachClient(hostT);

        var hello = new Hello { ModVersion = "wrong-version", GameVersion = "1.15.2", SteamId = 1, DisplayName = "x" };
        clientT.Send(Serializer.Pack(hello));

        mgr.Tick();

        fired.Should().BeFalse();
    }
}
