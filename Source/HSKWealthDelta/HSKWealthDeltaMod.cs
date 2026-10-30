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

    public override string SettingsCategory() => "HSK Wealth Delta";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Rect buttonRect = new Rect(inRect.x, inRect.y, 240f, 32f);
        if (Widgets.ButtonText(buttonRect, "WD_ResetWidgetPosition".Translate()))
        {
            Settings.widgetX = 200f;
            Settings.widgetY = 230f;
            Settings.Write();
        }
    }
}
