using FluentAssertions;
using PCBSMultiplayer.Net;
using PCBSMultiplayer.Session;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class SessionManagerCurrentTests
{
    [Fact]
    public void Current_is_null_before_any_session_constructed()
    {
        SessionManager.Current = null;
        SessionManager.Current.Should().BeNull();
    }

    [Fact]
    public void Current_tracks_most_recent_session()
    {
        var (a, _) = InMemoryTransport.CreatePair();
        var s = new SessionManager(SessionRole.Host, a);
        SessionManager.Current = s;
        SessionManager.Current.Should().BeSameAs(s);
    }

    [Fact]
    public void ApplyingRemoteDelta_defaults_false_and_is_mutable()
    {
        SessionManager.ApplyingRemoteDelta = false;
        SessionManager.ApplyingRemoteDelta.Should().BeFalse();
        SessionManager.ApplyingRemoteDelta = true;
        SessionManager.ApplyingRemoteDelta.Should().BeTrue();
        SessionManager.ApplyingRemoteDelta = false;
    }
}
