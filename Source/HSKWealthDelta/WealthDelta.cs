using System.Collections.Generic;

namespace HSKWealthDelta;

public class DeltaEntry
{
    public string label;
    public string defName;
    public int countNow;
    public int countBefore;
    public int countDelta;
    public float valueDelta;

    // For pawns
    public int pawnId;
    public string pawnName;
    public bool isNew;
    public bool isGone;
    public bool isValueChangeOnly;
}

public class WealthDelta
{
    public List<DeltaEntry> items = new List<DeltaEntry>();
    public List<DeltaEntry> buildings = new List<DeltaEntry>();
    public List<DeltaEntry> pawns = new List<DeltaEntry>();
    public bool hasData;

    public float TotalItemsDelta
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < items.Count; i++)
                sum += items[i].valueDelta;
            return sum;
        }
    }

    public float TotalBuildingsDelta
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < buildings.Count; i++)
                sum += buildings[i].valueDelta;
            return sum;
        }
    }

    public float TotalPawnsDelta
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < pawns.Count; i++)
                sum += pawns[i].valueDelta;
            return sum;
        }
    }

    public float TotalDelta => TotalItemsDelta + TotalBuildingsDelta + TotalPawnsDelta;

    public static WealthDelta Calculate(WealthSnapshot current, WealthSnapshot previous)
    {
        var delta = new WealthDelta();
        if (current == null || previous == null)
        {
            delta.hasData = false;
            return delta;
        }

        delta.hasData = true;
        CompareEntries(current.items, previous.items, delta.items, isPawns: false);
        CompareEntries(current.buildings, previous.buildings, delta.buildings, isPawns: false);
        CompareEntries(current.pawns, previous.pawns, delta.pawns, isPawns: true);

        return delta;
    }

    private static void CompareEntries(List<WealthEntry> now, List<WealthEntry> before, List<DeltaEntry> result, bool isPawns)
    {
        if (isPawns)
        {
            ComparePawns(now, before, result);
            return;
        }

        // Build maps by groupKey
        var beforeMap = new Dictionary<string, WealthEntry>();
        for (int i = 0; i < before.Count; i++)
        {
            string key = before[i].groupKey ?? before[i].label;
            beforeMap[key] = before[i];
        }

        var nowMap = new Dictionary<string, WealthEntry>();
        for (int i = 0; i < now.Count; i++)
        {
            string key = now[i].groupKey ?? now[i].label;
            nowMap[key] = now[i];
        }

        // Items in current snapshot
        foreach (var entry in now)
        {
            string key = entry.groupKey ?? entry.label;
            beforeMap.TryGetValue(key, out var prev);
            int prevCount = prev?.count ?? 0;
            float prevValue = prev?.marketValueTotal ?? 0f;

            int countDelta = entry.count - prevCount;
            float valueDelta = entry.marketValueTotal - prevValue;

            if (System.Math.Abs(valueDelta) < 1f)
                continue;

            result.Add(new DeltaEntry
            {
                label = entry.label,
                defName = entry.defName,
                countNow = entry.count,
                countBefore = prevCount,
                countDelta = countDelta,
                valueDelta = valueDelta,
                isNew = prev == null,
                isValueChangeOnly = entry.thingId != 0 && prev != null && countDelta == 0
            });
        }

        // Items that were in previous but not in current (removed)
        foreach (var entry in before)
        {
            string key = entry.groupKey ?? entry.label;
            if (nowMap.ContainsKey(key))
                continue;

            result.Add(new DeltaEntry
            {
                label = entry.label,
                defName = entry.defName,
                countNow = 0,
                countBefore = entry.count,
                countDelta = -entry.count,
                valueDelta = -entry.marketValueTotal,
                isGone = true
            });
        }
    }

    private static void ComparePawns(List<WealthEntry> now, List<WealthEntry> before, List<DeltaEntry> result)
    {
        var beforeById = new Dictionary<int, WealthEntry>();
        for (int i = 0; i < before.Count; i++)
        {
            if (before[i].pawnId != 0)
                beforeById[before[i].pawnId] = before[i];
        }

        var nowById = new Dictionary<int, WealthEntry>();
        for (int i = 0; i < now.Count; i++)
        {
            if (now[i].pawnId != 0)
                nowById[now[i].pawnId] = now[i];
        }

        // Current pawns
        foreach (var entry in now)
        {
            if (entry.pawnId == 0) continue;
            beforeById.TryGetValue(entry.pawnId, out var prev);
            float prevValue = prev?.marketValueTotal ?? 0f;
            float valueDelta = entry.marketValueTotal - prevValue;

            if (prev != null && System.Math.Abs(valueDelta) < 1f)
                continue;

            result.Add(new DeltaEntry
            {
                label = entry.pawnName,
                defName = entry.defName,
                countNow = 1,
                countBefore = prev != null ? 1 : 0,
                countDelta = prev != null ? 0 : 1,
                valueDelta = valueDelta,
                pawnId = entry.pawnId,
                pawnName = entry.pawnName,
                isNew = prev == null,
                isValueChangeOnly = prev != null
            });
        }

        // Gone pawns
        foreach (var entry in before)
        {
            if (entry.pawnId == 0) continue;
            if (nowById.ContainsKey(entry.pawnId))
                continue;

            result.Add(new DeltaEntry
            {
                label = entry.pawnName,
                defName = entry.defName,
                countNow = 0,
                countBefore = 1,
                countDelta = -1,
                valueDelta = -entry.marketValueTotal,
                pawnId = entry.pawnId,
                pawnName = entry.pawnName,
                isGone = true
            });
        }
    }
}
