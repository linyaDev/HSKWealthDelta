using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HSKWealthDelta;

public static class WealthSnapshotCollector
{
    public static WealthSnapshot TakeSnapshot(Map map)
    {
        var snapshot = new WealthSnapshot();

        int ticks = GenTicks.TicksGame;
        snapshot.dayTaken = GenDate.DayOfYear(ticks, 0);
        snapshot.quadrumTaken = (int)GenDate.Quadrum(ticks, 0);
        snapshot.yearTaken = GenDate.Year(ticks, 0);

        CollectItems(map, snapshot.items);
        CollectBuildings(map, snapshot.buildings);
        CollectFloors(map, snapshot.buildings);
        CollectPawns(map, snapshot.pawns);

        return snapshot;
    }

    private static bool IsUnique(Thing thing)
    {
        return thing.TryGetComp<CompQuality>() != null;
    }

    private static string GetGroupKey(Thing thing)
    {
        string key = thing.def.defName;
        if (thing.Stuff != null)
            key += "_" + thing.Stuff.defName;
        return key;
    }

    private static void CollectItems(Map map, List<WealthEntry> entries)
    {
        var tmpThings = new List<Thing>();
        ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
            tmpThings, allowUnreal: false, x =>
            {
                if (x is PassingShip || x is MapComponent)
                    return false;
                if (x is Pawn pawn && pawn.Faction != Faction.OfPlayer)
                    return false;
                if (x is Pawn p2 && p2.IsQuestLodger())
                    return false;
                return true;
            });

        var grouped = new Dictionary<string, WealthEntry>();
        for (int i = 0; i < tmpThings.Count; i++)
        {
            var thing = tmpThings[i];
            if (!thing.SpawnedOrAnyParentSpawned || thing.PositionHeld.Fogged(map))
                continue;

            float val = thing.MarketValue * thing.stackCount;

            if (IsUnique(thing))
            {
                // Unique item — track by thingIDNumber
                string key = "id_" + thing.thingIDNumber;
                var entry = new WealthEntry(thing.LabelNoCount, thing.def.defName, key, 1, thing.MarketValue, val);
                entry.thingId = thing.thingIDNumber;
                entries.Add(entry);
            }
            else
            {
                // Stackable — group by def + stuff
                string key = GetGroupKey(thing);
                if (grouped.TryGetValue(key, out var existing))
                {
                    existing.count += thing.stackCount;
                    existing.marketValueTotal += val;
                }
                else
                {
                    grouped[key] = new WealthEntry(thing.LabelNoCount, thing.def.defName, key, thing.stackCount, thing.MarketValue, val);
                }
            }
        }

        entries.AddRange(grouped.Values);
    }

    private static void CollectBuildings(Map map, List<WealthEntry> entries)
    {
        var list = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
        var grouped = new Dictionary<string, WealthEntry>();

        for (int i = 0; i < list.Count; i++)
        {
            var thing = list[i];
            if (thing.Faction != Faction.OfPlayer)
                continue;

            float val = thing.GetStatValue(StatDefOf.MarketValueIgnoreHp);

            if (IsUnique(thing))
            {
                string key = "id_" + thing.thingIDNumber;
                var entry = new WealthEntry(thing.LabelShort, thing.def.defName, key, 1, val, val);
                entry.thingId = thing.thingIDNumber;
                entries.Add(entry);
            }
            else
            {
                string key = GetGroupKey(thing);
                if (grouped.TryGetValue(key, out var existing))
                {
                    existing.count += 1;
                    existing.marketValueTotal += val;
                }
                else
                {
                    grouped[key] = new WealthEntry(thing.LabelShort, thing.def.defName, key, 1, val, val);
                }
            }
        }

        entries.AddRange(grouped.Values);
    }

    private static void CollectFloors(Map map, List<WealthEntry> entries)
    {
        var topGrid = map.terrainGrid.topGrid;
        var grouped = new Dictionary<string, WealthEntry>();
        var cellIndices = map.cellIndices;

        for (int i = 0; i < topGrid.Length; i++)
        {
            var terrain = topGrid[i];
            if (terrain == null)
                continue;

            float val = terrain.GetStatValueAbstract(StatDefOf.MarketValue);
            if (val <= 0f)
                continue;

            // Skip fogged cells
            IntVec3 cell = cellIndices.IndexToCell(i);
            if (cell.Fogged(map))
                continue;

            string key = "floor_" + terrain.defName;
            if (grouped.TryGetValue(key, out var existing))
            {
                existing.count += 1;
                existing.marketValueTotal += val;
            }
            else
            {
                grouped[key] = new WealthEntry(terrain.label, terrain.defName, key, 1, val, val);
            }
        }

        entries.AddRange(grouped.Values);
    }

    private static void CollectPawns(Map map, List<WealthEntry> entries)
    {
        foreach (var pawn in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
        {
            if (pawn.IsQuestLodger())
                continue;

            float val = pawn.MarketValue;
            if (pawn.IsSlave)
                val *= 0.75f;

            string name = pawn.LabelShortCap;
            string key = "pawn_" + pawn.thingIDNumber;

            var entry = new WealthEntry(name, pawn.kindDef.defName, key, 1, val, val);
            entry.pawnId = pawn.thingIDNumber;
            entry.pawnName = name;
            entries.Add(entry);
        }
    }
}
