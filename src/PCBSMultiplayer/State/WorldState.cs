namespace PCBSMultiplayer.State;

public sealed class WorldState
{
    public long Money { get; set; }
    public long XP { get; set; }
    public int DayIndex { get; set; } = 1;
}
