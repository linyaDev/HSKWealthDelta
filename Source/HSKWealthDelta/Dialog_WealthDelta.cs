using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKWealthDelta;

public enum DeltaPeriod
{
    Days,
    Quadrum,
    Year
}

public enum DeltaCategory
{
    All,
    Items,
    Buildings,
    Pawns
}

public enum DeltaSortMode
{
    Name,
    ValueDelta,
    CountDelta
}

public class Dialog_WealthDelta : Window
{
    private static DeltaPeriod period = DeltaPeriod.Days;
    private static int daysAgo = 0; // 0 = current snapshot (no delta)
    private static bool daysPerStep; // false = cumulative (vs today), true = single-day delta
    private static bool defaultApplied; // pick "1 day ago" as the initial view once, if data exists
    private static DeltaCategory category = DeltaCategory.All;
    private static DeltaSortMode sortMode = DeltaSortMode.ValueDelta;
    private static bool sortAscending;
    private static HashSet<int> collapsedGroups = new HashSet<int>();
    private static bool durabilityCollapsed;
    private Vector2 scrollPos;
    private WealthDelta cachedDelta;
    private DeltaPeriod cachedPeriod = (DeltaPeriod)(-1);
    private int cachedDaysAgo = -1;
    private bool cachedStep;
    private int cachedTick = -1;

    private static readonly Color ColorGreen = new Color(0.3f, 0.9f, 0.3f);
    private static readonly Color ColorRed = new Color(0.9f, 0.3f, 0.3f);
    private static readonly Color ColorGray = new Color(0.6f, 0.6f, 0.6f);
    private static readonly Color HeaderBg = new Color(0.15f, 0.15f, 0.15f, 0.8f);
    private float cachedCurrentTotal;

    public override Vector2 InitialSize => new Vector2(650f, 550f);

    public Dialog_WealthDelta()
    {
        doCloseX = true;
        draggable = true;
        resizeable = true;
        absorbInputAroundWindow = false;

        // Default to "1 day ago" the first time the dialog opens, if there is a
        // previous day to compare against; otherwise stay on the current snapshot.
        if (!defaultApplied)
        {
            defaultApplied = true;
            var mgr = Current.Game?.GetComponent<WealthSnapshotManager>();
            if (mgr != null && mgr.GetYesterdaySnapshot() != null)
                daysAgo = 1;
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.WordWrap = false;

        float y = inRect.y;

        // Period tabs
        Rect periodRow = new Rect(inRect.x, y, inRect.width, 30f);
        DrawPeriodTabs(periodRow);
        y += 35f;

        // Category tabs
        Rect categoryRow = new Rect(inRect.x, y, inRect.width, 30f);
        DrawCategoryTabs(categoryRow);
        y += 35f;

        bool isCurrentView = period == DeltaPeriod.Days && daysAgo == 0;

        if (isCurrentView)
        {
            // Current snapshot content (must run before header to calc total)
            Rect contentRect = new Rect(inRect.x, y + 26f, inRect.width, inRect.yMax - y - 26f);
            DrawCurrentContent(contentRect);

            // Header for current snapshot
            Rect headerRect = new Rect(inRect.x, y, inRect.width - 16f, 24f);
            DrawCurrentHeader(headerRect, cachedCurrentTotal);
            y += 26f;
        }
        else
        {
            // Summary line
            var delta = GetCachedDelta();
            Rect summaryRect = new Rect(inRect.x, y, inRect.width, 24f);
            DrawSummary(summaryRect, delta);
            y += 28f;

            // Header
            Rect headerRect = new Rect(inRect.x, y, inRect.width - 16f, 24f);
            DrawHeader(headerRect);
            y += 26f;

            // Content
            Rect contentRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            DrawContent(contentRect, delta);
        }
        Text.WordWrap = true;
    }

    private void DrawPeriodTabs(Rect rect)
    {
        float daysTabW = 200f;
        float remainW = rect.width - daysTabW;
        float tabW = remainW / 2f;

        const float dropW = 156f;
        const float toggleW = 40f;

        // Days dropdown button
        Rect daysRect = new Rect(rect.x, rect.y, dropW, rect.height);
        if (period == DeltaPeriod.Days)
            Widgets.DrawBoxSolid(daysRect, new Color(0.3f, 0.3f, 0.4f, 0.8f));
        else
            Widgets.DrawBoxSolid(daysRect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
        Widgets.DrawBox(daysRect, 1);

        Text.Anchor = TextAnchor.MiddleCenter;
        string daysLabel;
        if (daysPerStep)
            daysLabel = "WD_DayStep".Translate((daysAgo - 1).ToString(), daysAgo.ToString());
        else
            daysLabel = daysAgo == 0
                ? (string)"WD_Now".Translate()
                : (string)"WD_DaysAgo".Translate(daysAgo.ToString());
        Widgets.Label(daysRect, daysLabel + " ▾");
        Text.Anchor = TextAnchor.UpperLeft;

        if (Widgets.ButtonInvisible(daysRect))
        {
            period = DeltaPeriod.Days;
            var options = new List<FloatMenuOption>();
            if (daysPerStep)
            {
                for (int d = 1; d <= 6; d++)
                {
                    int day = d;
                    options.Add(new FloatMenuOption(
                        "WD_DayStep".Translate((day - 1).ToString(), day.ToString()), () =>
                    {
                        daysAgo = day;
                        InvalidateCache();
                    }));
                }
            }
            else
            {
                options.Add(new FloatMenuOption("WD_Now".Translate(), () =>
                {
                    daysAgo = 0;
                    InvalidateCache();
                }));
                for (int d = 1; d <= 6; d++)
                {
                    int days = d;
                    options.Add(new FloatMenuOption("WD_DaysAgo".Translate(days.ToString()), () =>
                    {
                        daysAgo = days;
                        InvalidateCache();
                    }));
                }
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        // Mode toggle: cumulative (Σ, vs today) <-> per-day (Δ, single day)
        Rect modeRect = new Rect(rect.x + dropW + 4f, rect.y, toggleW, rect.height);
        Widgets.DrawBoxSolid(modeRect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
        Widgets.DrawBox(modeRect, 1);
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(modeRect, daysPerStep ? "Δ" : "Σ");
        Text.Anchor = TextAnchor.UpperLeft;
        if (Mouse.IsOver(modeRect))
            TooltipHandler.TipRegion(modeRect,
                daysPerStep ? "WD_ModeStepTip".Translate() : "WD_ModeCumulativeTip".Translate());
        if (Widgets.ButtonInvisible(modeRect))
        {
            daysPerStep = !daysPerStep;
            period = DeltaPeriod.Days;
            if (daysPerStep && daysAgo == 0)
                daysAgo = 1;
            InvalidateCache();
        }

        // Quadrum tab
        float x = rect.x + daysTabW;
        if (DrawTab(new Rect(x, rect.y, tabW, rect.height),
            "WD_Quadrum".Translate(), period == DeltaPeriod.Quadrum))
        {
            period = DeltaPeriod.Quadrum;
            InvalidateCache();
        }

        // Year tab
        if (DrawTab(new Rect(x + tabW, rect.y, tabW, rect.height),
            "WD_Year".Translate(), period == DeltaPeriod.Year))
        {
            period = DeltaPeriod.Year;
            InvalidateCache();
        }
    }

    private void DrawCategoryTabs(Rect rect)
    {
        // Row 1: All, Pawns, Animals, Buildings
        float tabW = rect.width / 4f;
        if (DrawTab(new Rect(rect.x, rect.y, tabW, rect.height),
            "WD_All".Translate(), category == DeltaCategory.All))
            category = DeltaCategory.All;
        if (DrawTab(new Rect(rect.x + tabW, rect.y, tabW, rect.height),
            "WD_Items".Translate(), category == DeltaCategory.Items))
            category = DeltaCategory.Items;
        if (DrawTab(new Rect(rect.x + tabW * 2, rect.y, tabW, rect.height),
            "WD_Buildings".Translate(), category == DeltaCategory.Buildings))
            category = DeltaCategory.Buildings;
        if (DrawTab(new Rect(rect.x + tabW * 3, rect.y, tabW, rect.height),
            "WD_Pawns".Translate(), category == DeltaCategory.Pawns))
            category = DeltaCategory.Pawns;
    }

    private bool DrawTab(Rect rect, string label, bool active)
    {
        if (active)
            Widgets.DrawBoxSolid(rect, new Color(0.3f, 0.3f, 0.4f, 0.8f));
        else
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));

        Widgets.DrawBox(rect, 1);
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;

        return Widgets.ButtonInvisible(rect);
    }

    private void DrawSummary(Rect rect, WealthDelta delta)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        if (!delta.hasData)
        {
            GUI.color = ColorGray;
            Widgets.Label(rect, "WD_NoData".Translate());
        }
        else
        {
            float totalDelta = delta.TotalDelta;
            GUI.color = totalDelta >= 0 ? ColorGreen : ColorRed;
            string sign = totalDelta >= 0 ? "+" : "";
            Widgets.Label(rect, "WD_TotalDelta".Translate(sign + totalDelta.ToString("F0") + " $"));
        }

        // Next update timer on the right
        GUI.color = ColorGray;
        Text.Anchor = TextAnchor.MiddleRight;
        int ticksPerDay = 60000;
        int checkInterval = 2500;
        int currentDayTick = GenTicks.TicksGame % ticksPerDay;
        int ticksLeft = ticksPerDay - currentDayTick;
        // Add up to 1 hour delay for the check interval
        int ticksLeftWithDelay = ticksLeft + checkInterval - (GenTicks.TicksGame % checkInterval);
        float hoursLeft = ticksLeftWithDelay / 2500f;
        string timeStr;
        if (hoursLeft < 1f)
        {
            int minutes = (int)(hoursLeft * 60f);
            if (minutes < 1) minutes = 1;
            timeStr = "WD_NextUpdateMin".Translate(minutes.ToString());
        }
        else
        {
            timeStr = "WD_NextUpdate".Translate(hoursLeft.ToString("F0"));
        }
        Widgets.Label(rect, timeStr);

        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static readonly string[] GroupLabels = { "WD_GrpPawns", "WD_GrpAnimals", "WD_GrpWeapons", "WD_GrpApparel", "WD_GrpFood", "WD_GrpTextile", "WD_GrpItems", "WD_GrpBuildings" };

    // 0=pawns, 1=animals, 2=weapons, 3=apparel, 4=food, 5=textile, 6=items, 7=buildings
    private static int GetTypeGroup(string defName, int pawnId)
    {
        if (pawnId != 0)
            return IsAnimalKind(defName) ? 1 : 0;
        if (IsWeaponDef(defName)) return 2;
        if (IsApparelDef(defName)) return 3;
        var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if (def == null)
        {
            // Check if it's a floor terrain
            var terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(defName);
            if (terrain != null) return 7; // buildings group
            return 6;
        }
        if (def.building != null) return 7;
        if (def.IsIngestible) return 4;
        if (def.IsStuff && def.stuffProps?.categories != null)
        {
            foreach (var cat in def.stuffProps.categories)
                if (cat == StuffCategoryDefOf.Fabric || cat == StuffCategoryDefOf.Leathery)
                    return 5;
        }
        return 6;
    }

    private bool DrawGroupSeparator(Rect rect, string label, float total, int groupId)
    {
        bool collapsed = collapsedGroups.Contains(groupId);
        Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.25f, 0.8f));
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = Color.white;
        string arrow = collapsed ? "▶ " : "▼ ";
        Widgets.Label(new Rect(rect.x + 4f, rect.y, rect.width * 0.6f, rect.height), arrow + label);
        Text.Anchor = TextAnchor.MiddleRight;
        string sign = total >= 0 ? "+" : "";
        GUI.color = total > 0 ? ColorGreen : total < 0 ? ColorRed : ColorGray;
        Widgets.Label(new Rect(rect.x, rect.y, rect.width - 4f, rect.height), sign + total.ToString("F0") + " $");
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        if (Widgets.ButtonInvisible(rect))
        {
            if (collapsed) collapsedGroups.Remove(groupId);
            else collapsedGroups.Add(groupId);
        }
        return collapsed;
    }

    private bool DrawGroupSeparatorPlain(Rect rect, string label, float total, int groupId)
    {
        bool collapsed = collapsedGroups.Contains(groupId);
        Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.25f, 0.8f));
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = Color.white;
        string arrow = collapsed ? "▶ " : "▼ ";
        Widgets.Label(new Rect(rect.x + 4f, rect.y, rect.width * 0.6f, rect.height), arrow + label);
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = Color.white;
        Widgets.Label(new Rect(rect.x, rect.y, rect.width - 4f, rect.height), total.ToString("F0") + " $");
        Text.Anchor = TextAnchor.UpperLeft;
        if (Widgets.ButtonInvisible(rect))
        {
            if (collapsed) collapsedGroups.Remove(groupId);
            else collapsedGroups.Add(groupId);
        }
        return collapsed;
    }

    private static bool IsWeaponDef(string defName)
    {
        var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if (def == null || !def.IsWeapon) return false;
        if (def.IsStuff) return false;
        return true;
    }

    private static bool IsApparelDef(string defName)
    {
        var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        return def != null && def.IsApparel;
    }

    private static bool IsAnimalKind(string defName)
    {
        var def = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
        return def != null && def.RaceProps.Animal;
    }

    private void DrawHeader(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, HeaderBg);

        float x = rect.x;
        float nameW = rect.width - 220f;

        // Name column header
        Rect nameRect = new Rect(x, rect.y, nameW, rect.height);
        DrawSortButton(nameRect, "WD_Name".Translate(), DeltaSortMode.Name);
        x += nameW;

        // Count delta
        Rect countRect = new Rect(x, rect.y, 80f, rect.height);
        DrawSortButton(countRect, "WD_Count".Translate(), DeltaSortMode.CountDelta);
        x += 80f;

        // Value delta
        Rect valueRect = new Rect(x, rect.y, 140f, rect.height);
        DrawSortButton(valueRect, "WD_ValueDelta".Translate(), DeltaSortMode.ValueDelta);
    }

    private void DrawSortButton(Rect rect, string label, DeltaSortMode mode)
    {
        Text.Anchor = TextAnchor.MiddleCenter;
        string arrow = "";
        if (sortMode == mode)
            arrow = sortAscending ? " ▲" : " ▼";
        Widgets.Label(rect, label + arrow);
        Text.Anchor = TextAnchor.UpperLeft;

        if (Widgets.ButtonInvisible(rect))
        {
            if (sortMode == mode)
                sortAscending = !sortAscending;
            else
            {
                sortMode = mode;
                sortAscending = false;
            }
        }
    }

    private const float IconSize = 24f;
    private const float IconPad = 4f;

    private static void DrawDefIcon(Rect rect, string defName)
    {
        var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if (def == null) return;
        GUI.color = Color.white;
        Widgets.DefIcon(rect, def);
    }

    private void DrawCurrentHeader(Rect rect, float total)
    {
        Widgets.DrawBoxSolid(rect, HeaderBg);

        float x = rect.x;
        float nameW = rect.width - 220f;

        // Show total wealth instead of "Name"
        Rect nameRect = new Rect(x, rect.y, nameW, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = Color.white;
        Widgets.Label(nameRect, "WD_TotalWealth".Translate(total.ToString("F0") + " $"));
        if (Widgets.ButtonInvisible(nameRect))
        {
            if (sortMode == DeltaSortMode.Name) sortAscending = !sortAscending;
            else { sortMode = DeltaSortMode.Name; sortAscending = false; }
        }
        x += nameW;

        Rect countRect = new Rect(x, rect.y, 80f, rect.height);
        DrawSortButton(countRect, "WD_Count".Translate(), DeltaSortMode.CountDelta);
        x += 80f;

        Rect valueRect = new Rect(x, rect.y, 140f, rect.height);
        DrawSortButton(valueRect, "WD_Value".Translate(), DeltaSortMode.ValueDelta);
    }

    private void DrawCurrentContent(Rect rect)
    {
        var manager = Current.Game?.GetComponent<WealthSnapshotManager>();
        var snapshot = manager?.GetLatestSnapshot() ?? manager?.EnsureInitialSnapshot();
        if (snapshot == null)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ColorGray;
            Widgets.Label(rect, "WD_NoData".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            return;
        }

        var entries = GetSnapshotEntries(snapshot);

        // Sort
        var sorted = new List<WealthEntry>(entries);
        Comparison<WealthEntry> innerComp = sortMode switch
        {
            DeltaSortMode.Name => (a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase),
            DeltaSortMode.CountDelta => (a, b) => b.count.CompareTo(a.count),
            DeltaSortMode.ValueDelta => (a, b) => b.marketValueTotal.CompareTo(a.marketValueTotal),
            _ => (a, b) => 0
        };
        sorted.Sort((a, b) =>
        {
            if (category == DeltaCategory.All)
            {
                int ga = GetTypeGroup(a.defName, a.pawnId);
                int gb = GetTypeGroup(b.defName, b.pawnId);
                if (ga != gb) return ga.CompareTo(gb);
            }
            return innerComp(a, b);
        });
        if (sortAscending) sorted.Reverse();

        // Total for header
        float total = 0f;
        for (int i = 0; i < sorted.Count; i++) total += sorted[i].marketValueTotal;
        cachedCurrentTotal = total;

        // Pre-calculate visible row count
        float rowHeight = 28f;
        int visibleRows = 0;
        {
            int lg = -1;
            bool showAll = category == DeltaCategory.All;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (showAll)
                {
                    int g = GetTypeGroup(sorted[i].defName, sorted[i].pawnId);
                    if (g != lg) { visibleRows++; lg = g; } // separator
                    if (collapsedGroups.Contains(g)) continue;
                }
                visibleRows++;
            }
        }

        Rect viewRect = new Rect(0f, 0f, rect.width - 16f, visibleRows * rowHeight);
        Widgets.BeginScrollView(rect, ref scrollPos, viewRect);

        float nameW = viewRect.width - 220f;
        int lastGrp = -1;
        float drawY = 0f;
        for (int i = 0; i < sorted.Count; i++)
        {
            var entry = sorted[i];

            // Group separator
            if (category == DeltaCategory.All)
            {
                int grp = GetTypeGroup(entry.defName, entry.pawnId);
                if (grp != lastGrp)
                {
                    float grpTotal = 0f;
                    for (int j = i; j < sorted.Count; j++)
                    {
                        if (GetTypeGroup(sorted[j].defName, sorted[j].pawnId) != grp) break;
                        grpTotal += sorted[j].marketValueTotal;
                    }
                    DrawGroupSeparatorPlain(
                        new Rect(0f, drawY, viewRect.width, rowHeight),
                        GroupLabels[grp].Translate(), grpTotal, grp);
                    drawY += rowHeight;
                    lastGrp = grp;
                }
                if (collapsedGroups.Contains(lastGrp))
                    continue;
            }

            Rect rowRect = new Rect(0f, drawY, viewRect.width, rowHeight);
            drawY += rowHeight;

            if (i % 2 == 0)
                Widgets.DrawBoxSolid(rowRect, new Color(0.1f, 0.1f, 0.1f, 0.3f));

            if (Mouse.IsOver(rowRect))
                Widgets.DrawHighlight(rowRect);

            float x = rowRect.x + 4f;

            // Icon
            DrawDefIcon(new Rect(x, rowRect.y + 2f, IconSize, IconSize), entry.defName);
            x += IconSize + IconPad;

            // Name
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            Widgets.Label(new Rect(x, rowRect.y, nameW - IconSize - IconPad - 8f, rowRect.height), entry.label);
            x = rowRect.x + 4f + nameW;

            // Count
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(x, rowRect.y, 80f, rowRect.height),
                category == DeltaCategory.Pawns ? "1" : entry.count.ToString());
            x += 80f;

            // Value
            Widgets.Label(new Rect(x, rowRect.y, 140f, rowRect.height), entry.marketValueTotal.ToString("F0") + " $");

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        Widgets.EndScrollView();
    }

    private void DrawContent(Rect rect, WealthDelta delta)
    {
        if (!delta.hasData)
            return;

        var entries = GetDeltaEntries(delta);

        var sorted = SortEntries(entries);

        // Pre-calculate visible rows
        float rowHeight = 28f;
        int visibleRows = 0;
        {
            int lg = -1;
            bool showAll = category == DeltaCategory.All;
            bool hasDur = false;
            for (int i = 0; i < sorted.Count; i++)
            {
                var e = sorted[i];
                if (!e.isValueChangeOnly && showAll)
                {
                    int g = GetTypeGroup(e.defName, e.pawnId);
                    if (g != lg) { visibleRows++; lg = g; }
                    if (collapsedGroups.Contains(g)) continue;
                }
                if (e.isValueChangeOnly && !hasDur) { visibleRows++; hasDur = true; }
                if (e.isValueChangeOnly && durabilityCollapsed) continue;
                visibleRows++;
            }
        }

        Rect viewRect = new Rect(0f, 0f, rect.width - 16f, visibleRows * rowHeight);
        Widgets.BeginScrollView(rect, ref scrollPos, viewRect);

        float nameW = viewRect.width - 220f;
        bool drawnDurabilitySep = false;
        int lastGroup = -1;
        float drawY = 0f;
        for (int i = 0; i < sorted.Count; i++)
        {
            var entry = sorted[i];

            // Group separator in All tab
            if (category == DeltaCategory.All && !entry.isValueChangeOnly)
            {
                int grp = GetTypeGroup(entry.defName, entry.pawnId);
                if (grp != lastGroup)
                {
                    float grpTotal = 0f;
                    for (int j = i; j < sorted.Count; j++)
                    {
                        var e2 = sorted[j];
                        if (e2.isValueChangeOnly) break;
                        if (GetTypeGroup(e2.defName, e2.pawnId) != grp) break;
                        grpTotal += e2.valueDelta;
                    }
                    DrawGroupSeparator(
                        new Rect(0f, drawY, viewRect.width, rowHeight),
                        GroupLabels[grp].Translate(), grpTotal, grp);
                    drawY += rowHeight;
                    lastGroup = grp;
                }
                if (collapsedGroups.Contains(lastGroup))
                    continue;
            }

            // Durability separator
            if (!drawnDurabilitySep && entry.isValueChangeOnly)
            {
                float durTotal = 0f;
                for (int j = i; j < sorted.Count; j++)
                    durTotal += sorted[j].valueDelta;
                DrawGroupSeparator(
                    new Rect(0f, drawY, viewRect.width, rowHeight),
                    "WD_Durability".Translate(), durTotal, 100);
                drawY += rowHeight;
                drawnDurabilitySep = true;
                durabilityCollapsed = collapsedGroups.Contains(100);
            }
            if (entry.isValueChangeOnly && durabilityCollapsed)
                continue;

            Rect rowRect = new Rect(0f, drawY, viewRect.width, rowHeight);
            drawY += rowHeight;

            if (i % 2 == 0)
                Widgets.DrawBoxSolid(rowRect, new Color(0.1f, 0.1f, 0.1f, 0.3f));

            if (Mouse.IsOver(rowRect))
                Widgets.DrawHighlight(rowRect);

            float x = rowRect.x + 4f;

            // Icon
            DrawDefIcon(new Rect(x, rowRect.y + 2f, IconSize, IconSize), entry.defName);
            x += IconSize + IconPad;

            // Name
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = entry.isNew ? ColorGreen : entry.isGone ? ColorRed : Color.white;

            string nameLabel = entry.label;
            if (entry.isNew)
                nameLabel += " ★";

            Widgets.Label(new Rect(x, rowRect.y, nameW - IconSize - IconPad - 8f, rowRect.height), nameLabel);
            x = rowRect.x + 4f + nameW;

            // Count delta
            Text.Anchor = TextAnchor.MiddleCenter;
            string countStr;
            if (category == DeltaCategory.Pawns)
            {
                if (entry.isNew) countStr = "WD_Arrived".Translate();
                else if (entry.isGone) countStr = "WD_Gone".Translate();
                else countStr = "—";
            }
            else
            {
                countStr = entry.countDelta > 0 ? $"+{entry.countDelta}" : entry.countDelta.ToString();
            }
            GUI.color = entry.countDelta > 0 ? ColorGreen : entry.countDelta < 0 ? ColorRed : ColorGray;
            Widgets.Label(new Rect(x, rowRect.y, 80f, rowRect.height), countStr);
            x += 80f;

            // Value delta
            string valStr = entry.valueDelta >= 0 ? $"+{entry.valueDelta:F0} $" : $"{entry.valueDelta:F0} $";
            GUI.color = entry.valueDelta > 0 ? ColorGreen : entry.valueDelta < 0 ? ColorRed : ColorGray;
            Widgets.Label(new Rect(x, rowRect.y, 140f, rowRect.height), valStr);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        Widgets.EndScrollView();
    }

    private List<DeltaEntry> SortEntries(List<DeltaEntry> entries)
    {
        var sorted = new List<DeltaEntry>(entries);
        Comparison<DeltaEntry> innerSort = sortMode switch
        {
            DeltaSortMode.Name => (a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase),
            DeltaSortMode.CountDelta => (a, b) =>
            {
                // positive first, then negative; within each group by absolute value descending
                int signA = a.countDelta > 0 ? 0 : a.countDelta < 0 ? 1 : 2;
                int signB = b.countDelta > 0 ? 0 : b.countDelta < 0 ? 1 : 2;
                if (signA != signB) return signA.CompareTo(signB);
                return Math.Abs(b.countDelta).CompareTo(Math.Abs(a.countDelta));
            },
            DeltaSortMode.ValueDelta => (a, b) =>
            {
                int signA = a.valueDelta > 0 ? 0 : a.valueDelta < 0 ? 1 : 2;
                int signB = b.valueDelta > 0 ? 0 : b.valueDelta < 0 ? 1 : 2;
                if (signA != signB) return signA.CompareTo(signB);
                return Math.Abs(b.valueDelta).CompareTo(Math.Abs(a.valueDelta));
            },
            _ => (a, b) => 0
        };

        // durability changes always at the bottom; in All tab sort by type group
        sorted.Sort((a, b) =>
        {
            int durCmp = a.isValueChangeOnly.CompareTo(b.isValueChangeOnly);
            if (durCmp != 0) return durCmp;
            if (category == DeltaCategory.All)
            {
                int ga = GetTypeGroup(a.defName, a.pawnId);
                int gb = GetTypeGroup(b.defName, b.pawnId);
                if (ga != gb) return ga.CompareTo(gb);
            }
            return innerSort(a, b);
        });

        if (sortAscending)
            sorted.Reverse();
        return sorted;
    }

    private List<WealthEntry> GetSnapshotEntries(WealthSnapshot snapshot)
    {
        return category switch
        {
            DeltaCategory.All => CombineAll(snapshot.items, snapshot.buildings, snapshot.pawns),
            DeltaCategory.Items => snapshot.items,
            DeltaCategory.Buildings => snapshot.buildings,
            DeltaCategory.Pawns => snapshot.pawns,
            _ => snapshot.items
        };
    }

    private static List<T> CombineAll<T>(List<T> a, List<T> b, List<T> c)
    {
        var all = new List<T>(a.Count + b.Count + c.Count);
        all.AddRange(a);
        all.AddRange(b);
        all.AddRange(c);
        return all;
    }

    private List<DeltaEntry> GetDeltaEntries(WealthDelta delta)
    {
        return category switch
        {
            DeltaCategory.All => CombineAll(delta.items, delta.buildings, delta.pawns),
            DeltaCategory.Items => delta.items,
            DeltaCategory.Buildings => delta.buildings,
            DeltaCategory.Pawns => delta.pawns,
            _ => delta.items
        };
    }

    private WealthDelta GetCachedDelta()
    {
        int tick = GenTicks.TicksGame;
        if (cachedDelta != null && cachedPeriod == period && cachedDaysAgo == daysAgo
            && cachedStep == daysPerStep
            && Math.Abs(tick - cachedTick) < 2500)
            return cachedDelta;

        var manager = Current.Game?.GetComponent<WealthSnapshotManager>();
        if (manager == null)
        {
            cachedDelta = new WealthDelta();
            return cachedDelta;
        }

        var current = manager.GetLatestSnapshot() ?? manager.EnsureInitialSnapshot();
        WealthSnapshot previous;
        switch (period)
        {
            case DeltaPeriod.Days:
                if (daysPerStep)
                {
                    // Single-day delta: snapshot (daysAgo) vs the newer snapshot (daysAgo-1)
                    current = manager.GetDailySnapshot(daysAgo - 1);
                    previous = manager.GetDailySnapshot(daysAgo);
                }
                else
                {
                    previous = manager.GetDailySnapshot(daysAgo);
                }
                break;
            case DeltaPeriod.Quadrum:
                previous = manager.GetLastQuadrumSnapshot();
                break;
            case DeltaPeriod.Year:
                previous = manager.GetLastYearSnapshot();
                break;
            default:
                previous = null;
                break;
        }

        cachedDelta = WealthDelta.Calculate(current, previous);
        cachedPeriod = period;
        cachedDaysAgo = daysAgo;
        cachedStep = daysPerStep;
        cachedTick = tick;
        return cachedDelta;
    }

    private void InvalidateCache()
    {
        cachedTick = -1;
        scrollPos = Vector2.zero;
    }
}
