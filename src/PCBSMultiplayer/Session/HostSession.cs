namespace PCBSMultiplayer.Session;

public sealed class HostSession
{
    private readonly SessionManager _mgr;
    public HostSession(SessionManager mgr) { _mgr = mgr; }
}
