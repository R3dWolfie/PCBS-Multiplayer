using System;
using UnityEngine;

namespace PCBSMultiplayer.UI;

// Palette sourced from the PCBS decompile so the mod's screens read as part of the game
// rather than a BepInEx overlay:
//   Accent  — Color(0.992, 0.49, 0.0863) / #FD7D16, the "optional objective" orange
//             (csharp-firstpass.cs:171133, referenced as GetOptionalColour throughout).
//   Ready   — Color(0.22, 0.69, 0.38) / #39B062, the completion/tick green (:171155).
// The navy family matches the in-game panels; the button hovers are hand-picked to stay
// tonally correct next to orange.
public static class PcbsTheme
{
    public static readonly Color BgDeep    = new Color(0.07f, 0.09f, 0.18f, 0.96f);
    public static readonly Color BgPanel   = new Color(0.11f, 0.14f, 0.28f, 1f);
    public static readonly Color Row       = new Color(0.15f, 0.19f, 0.36f, 1f);
    public static readonly Color RowAlt    = new Color(0.13f, 0.16f, 0.30f, 1f);
    public static readonly Color RowHover  = new Color(0.19f, 0.24f, 0.45f, 1f);
    public static readonly Color Divider   = new Color(0.25f, 0.30f, 0.55f, 1f);
    public static readonly Color Accent    = new Color(0.992f, 0.49f, 0.0863f, 1f);
    public static readonly Color AccentHi  = new Color(1.0f, 0.62f, 0.22f, 1f);
    public static readonly Color Ready     = new Color(0.22f, 0.69f, 0.38f, 1f);
    public static readonly Color ReadyHi   = new Color(0.32f, 0.82f, 0.48f, 1f);
    public static readonly Color SelRow    = new Color(0.16f, 0.34f, 0.24f, 1f);
    public static readonly Color SelRowHi  = new Color(0.22f, 0.46f, 0.32f, 1f);
    public static readonly Color TextLight = new Color(0.94f, 0.95f, 0.97f, 1f);
    public static readonly Color TextDim   = new Color(0.68f, 0.72f, 0.82f, 1f);
    public static readonly Color TextError = new Color(1.0f, 0.48f, 0.48f, 1f);
    public static readonly Color BtnIdle   = new Color(0.15f, 0.19f, 0.36f, 1f);
    public static readonly Color BtnHover  = new Color(0.22f, 0.28f, 0.52f, 1f);
    public static readonly Color BtnDis    = new Color(0.15f, 0.17f, 0.22f, 1f);
    public static readonly Color Danger    = new Color(0.55f, 0.22f, 0.26f, 1f);
    public static readonly Color DangerHi  = new Color(0.75f, 0.30f, 0.34f, 1f);
    public static readonly Color EmptyRow  = new Color(0.10f, 0.12f, 0.22f, 1f);
    public static readonly Color NewCareer = new Color(0.14f, 0.22f, 0.38f, 1f);
    public static readonly Color ErrBg     = new Color(0.30f, 0.08f, 0.10f, 0.80f);
    public static readonly Color InfoBg    = new Color(0.08f, 0.14f, 0.26f, 0.80f);

    private static Font _font;
    private static bool _fontSearched;

    // IMGUI defaults to Arial, which instantly reads as "developer tool". PCBS bundles its
    // own fonts (Rawline / Roboto / OpenSans depending on locale); we pick whichever is
    // loaded by the time this is first called (after the main menu is up).
    public static Font GetFont()
    {
        if (_font != null) return _font;
        if (_fontSearched) return null;
        _fontSearched = true;
        try
        {
            var fonts = Resources.FindObjectsOfTypeAll<Font>();
            if (fonts == null || fonts.Length == 0) return null;
            string[] prefer = { "Rawline", "Roboto", "OpenSans", "Raleway", "Exo", "Inter" };
            foreach (var name in prefer)
            {
                foreach (var f in fonts)
                {
                    if (f == null || string.IsNullOrEmpty(f.name)) continue;
                    if (f.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    { _font = f; return _font; }
                }
            }
            // Last resort: first non-builtin, non-Arial font Unity has loaded.
            foreach (var f in fonts)
            {
                if (f == null || string.IsNullOrEmpty(f.name)) continue;
                if (f.name.StartsWith("Arial", StringComparison.OrdinalIgnoreCase)) continue;
                _font = f;
                return _font;
            }
        }
        catch { }
        return null;
    }

    public static Texture2D SolidTex(Color c)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var px = new Color[4]; for (int i = 0; i < 4; i++) px[i] = c;
        t.SetPixels(px); t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }
}
