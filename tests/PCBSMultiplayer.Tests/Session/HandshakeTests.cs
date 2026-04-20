using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Session;
using PCBSMultiplayer.State;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class HandshakeTests
{
    [Fact]
    public void Client_joins_and_receives_welcome_with_world_snapshot()
    {
        var (transportA, transportB) = InMemoryTransport.CreatePair();

        var host = new SessionManager(SessionRole.Host, transportA);
        host.World.Money = 2500;
        host.World.XP = 300;
        host.World.DayIndex = 4;
        host.World.JobBoard.AddAvailable(new Job { Id = "cpu-fix" });

        var client = new SessionManager(SessionRole.Client, transportB);
        client.Client.DisplayName = "alice";
        client.Client.SteamId = 7656119800001UL;
        client.Client.SayHello();

        host.Tick();
        client.Tick();

        client.IsLive.Should().BeTrue();
        client.LocalSlot.Should().Be(1);
        client.World.Money.Should().Be(2500);
        client.World.XP.Should().Be(300);
        client.World.DayIndex.Should().Be(4);
        client.World.JobBoard.Available.Should().ContainSingle(j => j.Id == "cpu-fix");
        host.Host.Clients.Should().ContainKey(1);
        host.Host.Clients[1].DisplayName.Should().Be("alice");
    }
}
