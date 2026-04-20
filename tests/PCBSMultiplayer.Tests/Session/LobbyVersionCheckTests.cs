using FluentAssertions;
using PCBSMultiplayer.Session;
using Xunit;

namespace PCBSMultiplayer.Tests.Session;

public class LobbyVersionCheckTests
{
    [Fact]
    public void Exact_match_is_compatible()
    {
        LobbyVersionCheck.IsCompatible("0.1.0", "0.1.0").Should().BeTrue();
    }

    [Fact]
    public void Patch_mismatch_is_incompatible()
    {
        LobbyVersionCheck.IsCompatible("0.1.0", "0.1.1").Should().BeFalse();
    }

    [Fact]
    public void Missing_remote_version_is_incompatible()
    {
        LobbyVersionCheck.IsCompatible("0.1.0", null).Should().BeFalse();
        LobbyVersionCheck.IsCompatible("0.1.0", "").Should().BeFalse();
    }

    [Fact]
    public void Reason_explains_mismatch()
    {
        LobbyVersionCheck.Describe("0.1.0", "0.2.0")
            .Should().Contain("0.1.0").And.Contain("0.2.0");
    }

    [Fact]
    public void Reason_handles_missing_remote()
    {
        LobbyVersionCheck.Describe("0.1.0", null).Should().Contain("missing");
    }
}
