using System;
using PCBSMultiplayer.Net.Messages;

namespace PCBSMultiplayer.State;

public static class DeltaApplier
{
    public static void Apply(WorldState state, IMessage delta)
    {
        switch (delta)
        {
            case MoneyChanged m: state.Money = m.NewTotal; break;
            case XPChanged x: state.XP = x.NewTotal; break;
            case TimeChanged t: state.DayIndex = t.NewDayIndex; break;
            default: throw new ArgumentException($"unknown delta {delta.GetType().Name}");
        }
    }
}
