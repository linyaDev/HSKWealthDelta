using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKWealthDelta;

/// <summary>
/// Draws the wealth widget right after the vanilla resource readout. This hook runs
/// after ThingOverlays (map stack-count labels) in the same OnGUI pass, so the
/// widget covers the labels instead of the labels bleeding through it.
/// </summary>
[HarmonyPatch(typeof(ResourceReadout), nameof(ResourceReadout.ResourceReadoutOnGUI))]
public static class Patch_ReadoutDraw
{
    public static void Postfix()
    {
        Current.Game?.GetComponent<WealthWidgetOverlay>()?.DrawOverlay();
    }
}
