using System.Collections.Generic;
using Verse;

namespace HSKWealthDelta;

public class WealthEntry : IExposable
{
    public string label;
    public string defName;
    public string groupKey; // stable key for matching: defName + stuff + quality (no HP%)
    public int count;
    public float marketValue;
    public float marketValueTotal;

    // For unique items (weapons, apparel)
    public int thingId;

    // For pawns
    public int pawnId;
    public string pawnName;

    public WealthEntry() { }

    public WealthEntry(string label, string defName, string groupKey, int count, float marketValue, float marketValueTotal)
    {
        this.label = label;
        this.defName = defName;
        this.groupKey = groupKey;
        this.count = count;
        this.marketValue = marketValue;
        this.marketValueTotal = marketValueTotal;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref label, "label");
        Scribe_Values.Look(ref defName, "defName");
        Scribe_Values.Look(ref groupKey, "groupKey");
        Scribe_Values.Look(ref count, "count");
        Scribe_Values.Look(ref marketValue, "marketValue");
        Scribe_Values.Look(ref marketValueTotal, "marketValueTotal");
        Scribe_Values.Look(ref thingId, "thingId");
        Scribe_Values.Look(ref pawnId, "pawnId");
        Scribe_Values.Look(ref pawnName, "pawnName");
    }
}

public class WealthSnapshot : IExposable
{
    public int dayTaken;
    public int quadrumTaken;
    public int yearTaken;
    public List<WealthEntry> items = new List<WealthEntry>();
    public List<WealthEntry> buildings = new List<WealthEntry>();
    public List<WealthEntry> pawns = new List<WealthEntry>();

    public float TotalItems
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < items.Count; i++)
                sum += items[i].marketValueTotal;
            return sum;
        }
    }

    public float TotalBuildings
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < buildings.Count; i++)
                sum += buildings[i].marketValueTotal;
            return sum;
        }
    }

    public float TotalPawns
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < pawns.Count; i++)
                sum += pawns[i].marketValueTotal;
            return sum;
        }
    }

    public float TotalWealth => TotalItems + TotalBuildings + TotalPawns;

    public WealthSnapshot() { }

    public void ExposeData()
    {
        Scribe_Values.Look(ref dayTaken, "dayTaken");
        Scribe_Values.Look(ref quadrumTaken, "quadrumTaken");
        Scribe_Values.Look(ref yearTaken, "yearTaken");
        Scribe_Collections.Look(ref items, "items", LookMode.Deep);
        Scribe_Collections.Look(ref buildings, "buildings", LookMode.Deep);
        Scribe_Collections.Look(ref pawns, "pawns", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            items ??= new List<WealthEntry>();
            buildings ??= new List<WealthEntry>();
            pawns ??= new List<WealthEntry>();
        }
    }
}
