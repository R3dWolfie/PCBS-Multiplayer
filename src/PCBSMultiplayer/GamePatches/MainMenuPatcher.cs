using System;
using BepInEx.Logging;
using HarmonyLib;
using PCBSMultiplayer.Session;
using UnityEngine;

namespace PCBSMultiplayer.GamePatches;

[HarmonyPatch(typeof(MainMenu), "Start")]
public static class MainMenuPatcher
{
    private static readonly ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("PCBSMultiplayer.MainMenu");

    public static void Postfix(MainMenu __instance)
    {
        try
        {
            FallbackMenu.Enable(__instance.gameObject);
            Log.LogInfo("OnGUI multiplayer buttons attached to MainMenu.");
        }
        catch (Exception ex)
        {
            Log.LogError("MainMenuPatcher failed: " + ex);
        }
    }
}

internal static class FallbackMenu
{
    public static void Enable(GameObject host)
    {
        if (host.GetComponent<FallbackMenuBehaviour>() == null)
            host.AddComponent<FallbackMenuBehaviour>();
    }
}

internal sealed class FallbackMenuBehaviour : MonoBehaviour
{
    private static GUIStyle _btnStyle;

    private void OnGUI()
    {
        if (_btnStyle == null)
        {
            _btnStyle = new GUIStyle(GUI.skin.button);
            _btnStyle.fontSize = 28;
            _btnStyle.fontStyle = FontStyle.Bold;
        }

        const float w = 420f;
        const float h = 70f;
        const float gap = 16f;
        float totalW = w * 2 + gap;
        float x = (Screen.width - totalW) * 0.5f;
        float y = 24f;

        if (GUI.Button(new Rect(x, y, w, h), "Host Multiplayer", _btnStyle))
            SessionLifecycle.StartHost();
        if (GUI.Button(new Rect(x + w + gap, y, w, h), "Join Multiplayer", _btnStyle))
            SessionLifecycle.JoinPendingInvite();
    }
}
