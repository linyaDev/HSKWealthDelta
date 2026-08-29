using RimWorld;
using UnityEngine;
using Verse;

namespace HSKWealthDelta;

public class WealthWidgetOverlay : GameComponent
{
    public static bool showWidget = true;

    // Muted EPrime green; classic widget red for negatives (CriticalTint reads as orange)
    private static readonly Color Green = new Color(0.55f, 0.78f, 0.45f);
    private static readonly Color Red = new Color(0.9f, 0.46f, 0.42f);
    private static readonly Color BgColor = new Color(0.08f, 0.08f, 0.08f, 0.7f);

    private static bool dragging;
    private static Vector2 dragOffset;
    private const float Width = 200f;
    private const float Height = 40f;

    private static Vector2 WidgetPos
    {
        get
        {
            var s = HSKWealthDeltaMod.Settings;
            if (s == null) return new Vector2(200f, 230f);
            return new Vector2(s.widgetX, s.widgetY);
        }
        set
        {
            var s = HSKWealthDeltaMod.Settings;
            if (s == null) return;
            s.widgetX = value.x;
            s.widgetY = value.y;
        }
    }

    public WealthWidgetOverlay(Game game) : base() { }

    // Drawn from Patch_ReadoutDraw (after map thing labels), not from GameComponentOnGUI —
    // that hook runs before ThingOverlays, so stack-count labels bled through the widget.
    public void DrawOverlay()
    {
        if (!showWidget)
            return;
        if (Current.ProgramState != ProgramState.Playing)
            return;
        if (Find.CurrentMap == null)
            return;

        var manager = Current.Game?.GetComponent<WealthSnapshotManager>();
        if (manager == null)
            return;

        var current = manager.GetLatestSnapshot() ?? manager.EnsureInitialSnapshot();
        if (current == null)
            return;

        var pos = WidgetPos;
        if (pos.x < 0f)
        {
            pos = new Vector2(200f, 230f);
            WidgetPos = pos;
        }

        Rect widgetRect = new Rect(pos.x, pos.y, Width, Height);

        // Dragging (right click)
        if (Event.current.type == EventType.MouseDown && Mouse.IsOver(widgetRect) && Event.current.button == 1)
        {
            dragging = true;
            dragOffset = Event.current.mousePosition - pos;
            Event.current.Use();
        }
        if (dragging)
        {
            if (Event.current.type == EventType.MouseDrag || Event.current.type == EventType.MouseMove)
            {
                pos = Event.current.mousePosition - dragOffset;
                pos.x = Mathf.Clamp(pos.x, 0f, UI.screenWidth - Width);
                pos.y = Mathf.Clamp(pos.y, 0f, UI.screenHeight - Height);
                WidgetPos = pos;
                widgetRect = new Rect(pos.x, pos.y, Width, Height);
            }
            if (Event.current.type == EventType.MouseUp && Event.current.button == 1)
            {
                dragging = false;
                HSKWealthDeltaMod.Settings?.Write();
                Event.current.Use();
            }
        }

        // Background
        Widgets.DrawBoxSolid(widgetRect, BgColor);

        Text.WordWrap = false;

        // Line 1: Current wealth
        float totalWealth = current.TotalWealth;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = Color.white;
        Rect line1 = new Rect(widgetRect.x, widgetRect.y + 1f, widgetRect.width, 20f);
        Widgets.Label(line1, "WD_WidgetWealth".Translate(totalWealth.ToString("F0") + " $"));

        // Line 2: Yesterday delta
        var yesterday = manager.GetYesterdaySnapshot();
        Text.Font = GameFont.Tiny;
        Rect line2 = new Rect(widgetRect.x, widgetRect.y + 20f, widgetRect.width, 18f);
        if (yesterday != null)
        {
            float delta = totalWealth - yesterday.TotalWealth;
            GUI.color = delta >= 0 ? Green : Red;
            string sign = delta >= 0 ? "+" : "";
            Widgets.Label(line2, "WD_WidgetDelta".Translate(sign + delta.ToString("F0") + " $"));
        }
        else
        {
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Widgets.Label(line2, "WD_NoData".Translate());
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.WordWrap = true;
        GUI.color = Color.white;

        // Left-click opens dialog
        if (Widgets.ButtonInvisible(widgetRect) && !dragging)
        {
            Find.WindowStack.Add(new Dialog_WealthDelta());
        }

        // Tooltip
        if (Mouse.IsOver(widgetRect))
        {
            Widgets.DrawHighlight(widgetRect);
            TooltipHandler.TipRegion(widgetRect, "WD_WidgetTooltip".Translate());
        }
    }
}
