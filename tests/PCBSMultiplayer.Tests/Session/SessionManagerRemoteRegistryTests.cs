using PCBSMultiplayer.Net;
using PCBSMultiplayer.Session;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public sealed class SessionManagerRemoteRegistryTests
{
    [Fact]
    public void SessionManager_exposes_empty_registry_on_construction()
    {
        var (a, _) = InMemoryTransport.CreatePair();
        var mgr = new SessionManager(SessionRole.Client, a);
        Assert.NotNull(mgr.RemoteRegistry);
        Assert.Equal(0, mgr.RemoteRegistry.Count);
    }
}
