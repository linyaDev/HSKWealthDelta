using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HSKWealthDelta;

[StaticConstructorOnStartup]
public static class HSKWealthDeltaInit
{
    public static readonly Texture2D WealthDeltaIcon = ContentFinder<Texture2D>.Get("WealthDeltaIcon", false)
        ?? Widgets.CheckboxOnTex;

    static HSKWealthDeltaInit()
    {
        var harmony = new Harmony("linya.hskwealthdelta");
        harmony.PatchAll();
        Log.Message("[HSKWealthDelta] Loaded.");
    }
}

[HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
public static class Patch_PlaySettings
{
    public static void Postfix(WidgetRow row, bool worldView)
    {
        if (worldView || Find.CurrentMap == null)
            return;

        row.ToggleableIcon(ref WealthWidgetOverlay.showWidget, HSKWealthDeltaInit.WealthDeltaIcon,
            "WD_ButtonTip".Translate(), SoundDefOf.Mouseover_ButtonToggle);
    }
}
