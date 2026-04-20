using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.Session;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class SpendMoneyTests
{
    [Fact]
    public void Second_purchase_over_budget_is_denied()
    {
        var (hostT1, clientT1) = InMemoryTransport.CreatePair();
        var (hostT2, clientT2) = InMemoryTransport.CreatePair();
        var host = new SessionManager(SessionRole.Host, hostT1);
        host.World.Money = 1000;
        host.Host.AttachClient(hostT1);
        host.Host.AttachClient(hostT2);

        var alice = new SessionManager(SessionRole.Client, clientT1);
        alice.Client.DisplayName = "alice"; alice.Client.SayHello();
        var bob = new SessionManager(SessionRole.Client, clientT2);
        bob.Client.DisplayName = "bob"; bob.Client.SayHello();
        host.Tick(); alice.Tick(); bob.Tick();

        alice.Transport.Send(Serializer.Pack(new SpendMoneyRequest { Amount = 800, RequestId = "a" }));
        bob.Transport.Send(Serializer.Pack(new SpendMoneyRequest { Amount = 800, RequestId = "b" }));
        host.Tick();
        alice.Tick(); bob.Tick();

        var aOk = alice.Client.LastSpendMoneyResult!.Accepted;
        var bOk = bob.Client.LastSpendMoneyResult!.Accepted;
        (aOk ^ bOk).Should().BeTrue("only one should get through");
        host.World.Money.Should().Be(200);
    }
}
