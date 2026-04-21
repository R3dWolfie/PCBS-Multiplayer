using BepInEx.Configuration;
using BepInEx.Logging;
using PCBSMultiplayer.Session;
using UnityEngine;

namespace PCBSMultiplayer.UI;

public sealed class PanicHotkeyHandler : MonoBehaviour
{
    public ConfigEntry<bool> EnableEntry;
    public ManualLogSource Log;
    private PanicHotkeyTracker _tracker;

    private void Awake()
    {
        _tracker = new PanicHotkeyTracker(holdMs: 3000);
        _tracker.OnFired = OnPanic;
    }

    private void Update()
    {
        var down = Input.GetKey(KeyCode.F7);
        _tracker.Update(down, (long)(Time.unscaledTime * 1000f));
    }

    private void OnPanic()
    {
        try
        {
            var mgr = SessionManager.Current;
            if (mgr != null && mgr.Transport != null) mgr.Transport.Disconnect();
            SessionManager.Current = null;
            if (EnableEntry != null)
            {
                EnableEntry.Value = false;
                EnableEntry.ConfigFile.Save();
            }
            if (Log != null) Log.LogWarning("Panic fired: mod disabled. Restart game for full vanilla restore.");
            enabled = false;
        }
        catch (System.Exception ex)
        {
            if (Log != null) Log.LogError("Panic handler failed: " + ex);
        }
    }
}
