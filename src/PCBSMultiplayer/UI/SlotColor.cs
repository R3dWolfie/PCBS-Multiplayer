namespace PCBSMultiplayer.UI;

public readonly struct RgbF
{
    public readonly float r, g, b;
    public RgbF(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
}

public static class SlotColor
{
    public static RgbF For(int slot) => slot switch
    {
        1 => new RgbF(0.9f, 0.3f, 0.3f),
        2 => new RgbF(0.3f, 0.5f, 0.9f),
        3 => new RgbF(0.3f, 0.8f, 0.3f),
        4 => new RgbF(0.9f, 0.8f, 0.3f),
        _ => new RgbF(1f, 1f, 1f),
    };
}
