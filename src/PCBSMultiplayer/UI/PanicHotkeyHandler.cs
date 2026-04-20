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

    private static GUIStyle _labelStyle;

    private void OnGUI()
    {
        string label;
        if (_tracker.Progress01 > 0f)
            label = "Panic: hold F7 (" + ((int)(_tracker.Progress01 * 100f)) + "%)";
        else
            label = "F7 hold 3s - Disable mod";

        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 28;
            _labelStyle.fontStyle = FontStyle.Bold;
            _labelStyle.alignment = TextAnchor.MiddleLeft;
        }
        _labelStyle.normal.textColor = Color.yellow;

        var size = _labelStyle.CalcSize(new GUIContent(label));
        float w = size.x + 24f;
        float h = size.y + 12f;
        float x = Screen.width - w - 16f;
        float y = 16f;

        var prevColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = prevColor;
        GUI.Label(new Rect(x + 12f, y + 6f, size.x, size.y), label, _labelStyle);
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
