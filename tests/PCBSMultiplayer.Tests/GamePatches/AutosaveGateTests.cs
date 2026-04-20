using FluentAssertions;
using PCBSMultiplayer.GamePatches;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Session;
using Xunit;

namespace PCBSMultiplayer.Tests.GamePatches;

public class AutosaveGateTests
{
    [Fact]
    public void Suppresses_when_client_session_live()
    {
        var (a, _) = InMemoryTransport.CreatePair();
        SessionManager.Current = new SessionManager(SessionRole.Client, a);
        try
        {
            AutosavePatches.ShouldSuppress().Should().BeTrue();
        }
        finally { SessionManager.Current = null; }
    }

    [Fact]
    public void Does_not_suppress_when_host_session_live()
    {
        var (a, _) = InMemoryTransport.CreatePair();
        SessionManager.Current = new SessionManager(SessionRole.Host, a);
        try
        {
            AutosavePatches.ShouldSuppress().Should().BeFalse();
        }
        finally { SessionManager.Current = null; }
    }

    [Fact]
    public void Does_not_suppress_when_no_session()
    {
        SessionManager.Current = null;
        AutosavePatches.ShouldSuppress().Should().BeFalse();
    }
}
