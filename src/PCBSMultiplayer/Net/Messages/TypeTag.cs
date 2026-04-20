namespace PCBSMultiplayer.Net.Messages;

public enum TypeTag : byte
{
    Hello = 1,
    Welcome = 2,
    Heartbeat = 3,
    Bye = 4,
    TransformUpdate = 10,
    HeldItemUpdate = 11,
    MoneyChanged = 20,
    XPChanged = 21,
    TimeChanged = 22,
    JobBoardDelta = 23,
    ClaimJobRequest = 30,
    SpendMoneyRequest = 31,
    InstallPartRequest = 32,
    ClaimJobResult = 40,
    SpendMoneyResult = 41,
    InstallPartResult = 42,
    PartInstalled = 50,
    TestRan = 51,
    JobCompleted = 52,
    BenchReleased = 53,
    ChatMessage = 60
}
