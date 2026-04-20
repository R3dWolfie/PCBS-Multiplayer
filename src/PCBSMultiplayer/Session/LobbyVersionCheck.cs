namespace PCBSMultiplayer.Session;

public static class LobbyVersionCheck
{
    public static bool IsCompatible(string local, string? remote)
        => !string.IsNullOrEmpty(remote) && local == remote;

    public static string Describe(string local, string? remote)
    {
        if (string.IsNullOrEmpty(remote))
            return $"host advertises no mod version (local: {local}); lobby is missing mod_version metadata — host likely isn't running the mod.";
        return $"mod version mismatch: local={local}, host={remote}. Both players need the same mod version to connect.";
    }
}
