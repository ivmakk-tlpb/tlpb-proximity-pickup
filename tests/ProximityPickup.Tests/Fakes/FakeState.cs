using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

// Puts the static state of the fake game types back to a known start.
internal static class FakeState
{
    public static void Reset()
    {
        Time.frameCount = 100;
        Screen.width = 1920;
        Screen.height = 1080;
        Physics.Hits.Clear();
        Physics.Calls = 0;
        Physics.LastCall = default;
        Harmony.Patched.Clear();
        Harmony.Unpatched.Clear();
        Harmony.ThrowFor = null;
        UIComponent.uiManager = null;
        LayoutRebuilder.Rebuilt.Clear();
        L.Texts.Clear();
        L.Texts["ui.or"] = "or";
    }
}
