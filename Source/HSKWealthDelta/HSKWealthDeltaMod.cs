using UnityEngine;
using Verse;

namespace HSKWealthDelta;

public class HSKWealthDeltaSettings : ModSettings
{
    public float widgetX = -1f;
    public float widgetY = -1f;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref widgetX, "widgetX", -1f);
        Scribe_Values.Look(ref widgetY, "widgetY", -1f);
        base.ExposeData();
    }
}

public class HSKWealthDeltaMod : Mod
{
    public static HSKWealthDeltaSettings Settings;

    public HSKWealthDeltaMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<HSKWealthDeltaSettings>();
    }

    public override string SettingsCategory() => null;
}
