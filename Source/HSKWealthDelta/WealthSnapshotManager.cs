using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HSKWealthDelta;

public class WealthSnapshotManager : GameComponent
{
    private List<WealthSnapshot> dailySnapshots = new List<WealthSnapshot>();
    private List<WealthSnapshot> quadrumSnapshots = new List<WealthSnapshot>();
    private List<WealthSnapshot> yearlySnapshots = new List<WealthSnapshot>();

    private int lastSnapshotDay = -1;

    private const int MaxDailySnapshots = 7;
    private const int MaxQuadrumSnapshots = 4;

    public WealthSnapshotManager(Game game) : base() { }

    public WealthSnapshot GetDailySnapshot(int daysAgo)
    {
        if (daysAgo < 0 || daysAgo >= dailySnapshots.Count)
            return null;
        return dailySnapshots[dailySnapshots.Count - 1 - daysAgo];
    }

    public WealthSnapshot GetLatestSnapshot()
    {
        return dailySnapshots.Count > 0 ? dailySnapshots[dailySnapshots.Count - 1] : null;
    }

    // Take the very first snapshot on demand so the widget/dialog have data
    // immediately, instead of waiting up to one in-game hour (and the game is
    // paused at start, so ticks — and GameComponentTick — don't advance yet).
    public WealthSnapshot EnsureInitialSnapshot()
    {
        if (dailySnapshots.Count > 0)
            return dailySnapshots[dailySnapshots.Count - 1];

        var map = Find.CurrentMap;
        if (map == null)
            return null;

        int currentDay = GenDate.DaysPassed;
        TakeDailySnapshot(map, currentDay);
        lastSnapshotDay = currentDay;
        return GetLatestSnapshot();
    }

    public WealthSnapshot GetYesterdaySnapshot()
    {
        return dailySnapshots.Count > 1 ? dailySnapshots[dailySnapshots.Count - 2] : null;
    }

    public WealthSnapshot GetWeekAgoSnapshot()
    {
        return dailySnapshots.Count >= MaxDailySnapshots ? dailySnapshots[0] : null;
    }

    public WealthSnapshot GetLastQuadrumSnapshot()
    {
        return quadrumSnapshots.Count > 0 ? quadrumSnapshots[quadrumSnapshots.Count - 1] : null;
    }

    public WealthSnapshot GetLastYearSnapshot()
    {
        return yearlySnapshots.Count > 0 ? yearlySnapshots[yearlySnapshots.Count - 1] : null;
    }

    public override void GameComponentTick()
    {
        // Check once per in-game hour (2500 ticks)
        if (GenTicks.TicksGame % 2500 != 0)
            return;

        int currentDay = GenDate.DaysPassed;
        if (currentDay <= lastSnapshotDay)
            return;

        var map = Find.CurrentMap;
        if (map == null)
            return;

        TakeDailySnapshot(map, currentDay);
        lastSnapshotDay = currentDay;
    }

    private void TakeDailySnapshot(Map map, int currentDay)
    {
        var snapshot = WealthSnapshotCollector.TakeSnapshot(map);

        // Daily
        dailySnapshots.Add(snapshot);
        while (dailySnapshots.Count > MaxDailySnapshots)
            dailySnapshots.RemoveAt(0);

        // Quadrum snapshot: first day of each quadrum (day 0, 15, 30, 45 of the year)
        int dayOfYear = GenDate.DayOfYear(GenTicks.TicksGame, 0);
        int dayOfQuadrum = GenDate.DayOfQuadrum(GenTicks.TicksGame, 0);
        if (dayOfQuadrum == 0)
        {
            quadrumSnapshots.Add(snapshot);
            while (quadrumSnapshots.Count > MaxQuadrumSnapshots)
                quadrumSnapshots.RemoveAt(0);
        }

        // Yearly snapshot: first day of the year
        if (dayOfYear == 0)
        {
            yearlySnapshots.Add(snapshot);
        }
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref lastSnapshotDay, "lastSnapshotDay", -1);
        Scribe_Collections.Look(ref dailySnapshots, "dailySnapshots", LookMode.Deep);
        Scribe_Collections.Look(ref quadrumSnapshots, "quadrumSnapshots", LookMode.Deep);
        Scribe_Collections.Look(ref yearlySnapshots, "yearlySnapshots", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            dailySnapshots ??= new List<WealthSnapshot>();
            quadrumSnapshots ??= new List<WealthSnapshot>();
            yearlySnapshots ??= new List<WealthSnapshot>();
        }
    }
}
