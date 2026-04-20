namespace PCBSMultiplayer.Session;

public sealed class ClientSession
{
    private readonly SessionManager _mgr;
    public ClientSession(SessionManager mgr) { _mgr = mgr; }
}
