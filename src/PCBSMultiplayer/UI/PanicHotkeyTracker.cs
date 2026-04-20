namespace PCBSMultiplayer.UI;

public delegate void PanicFiredHandler();

public sealed class PanicHotkeyTracker
{
    private readonly long _holdMs;
    private long _pressedAtMs;
    private bool _isPressed;
    public bool Fired { get; private set; }
    public PanicFiredHandler OnFired { get; set; }
    public float Progress01 { get; private set; }

    public PanicHotkeyTracker(long holdMs) { _holdMs = holdMs; }

    public void Update(bool isDown, long nowMs)
    {
        if (!isDown)
        {
            _isPressed = false;
            Progress01 = 0f;
            Fired = false;
            return;
        }
        if (!_isPressed) { _pressedAtMs = nowMs; _isPressed = true; }
        var held = nowMs - _pressedAtMs;
        Progress01 = held >= _holdMs ? 1f : (float)held / _holdMs;
        if (!Fired && held >= _holdMs)
        {
            Fired = true;
            if (OnFired != null) OnFired();
        }
    }

    public void Reset()
    {
        _isPressed = false;
        Progress01 = 0f;
        Fired = false;
    }
}
