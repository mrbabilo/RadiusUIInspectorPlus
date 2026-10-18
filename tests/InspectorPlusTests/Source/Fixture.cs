using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RUIInspectorTests;

// Scene builder shared by the checks and the measurement scenes: colonists, a full stockpile with varied items,
// small shelves, a bed. Built once per game on the quick test map.
internal static class Fixture
{
	internal static Map Map => Find.CurrentMap;

	internal static IntVec3 Home;

	internal static Zone_Stockpile Stockpile;

	internal static readonly List<Building_Storage> Shelves = new List<Building_Storage>();

	internal static Building_Bed Bed;

	internal static bool Built;

	internal static void Build(int colonists)
	{
		if (Built)
		{
			return;
		}
		Built = true;
		Home = Map.mapPawns.FreeColonistsSpawned.FirstOrDefault()?.Position ?? Map.Center;
		Colonists(colonists);
		Stockpile = NewStockpile(8);
		Fill(Stockpile);
		ThingDef shelf = DefDatabase<ThingDef>.GetNamedSilentFail("ShelfSmall") ?? ThingDef.Named("Shelf");
		for (int i = 0; i < 2; i++)
		{
			Building_Storage building = (Building_Storage)ThingMaker.MakeThing(shelf, ThingDefOf.WoodLog);
			building.SetFaction(Faction.OfPlayer);
			GenSpawn.Spawn(building, FreeCell(10), Map, Rot4.North, WipeMode.Vanish);
			Shelves.Add(building);
		}
		Bed = (Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
		Bed.SetFaction(Faction.OfPlayer);
		GenSpawn.Spawn(Bed, FreeCell(10), Map, Rot4.North, WipeMode.Vanish);
	}

	internal static List<Pawn> Pawns => Map.mapPawns.FreeColonistsSpawned.ToList();

	internal static int Colonists(int target)
	{
		int guard = 0;
		while (Map.mapPawns.FreeColonistsSpawnedCount < target && guard++ < target * 4)
		{
			Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, PawnGenerationContext.NonPlayer, null, forceGenerateNewPawn: true));
			if (pawn.InMentalState)
			{
				pawn.Destroy();
				continue;
			}
			GenSpawn.Spawn(pawn, FreeCell(6), Map);
		}
		return Map.mapPawns.FreeColonistsSpawnedCount;
	}

	// A square stockpile on free ground: every cell is standable, reachable and empty.
	private static Zone_Stockpile NewStockpile(int side)
	{
		IntVec3 center = FreeCell(14);
		Zone_Stockpile zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Map.zoneManager);
		Map.zoneManager.RegisterZone(zone);
		foreach (IntVec3 c in CellRect.CenteredOn(center, side / 2))
		{
			if (c.InBounds(Map) && Free(c) && Map.zoneManager.ZoneAt(c) == null)
			{
				zone.AddCell(c);
			}
		}
		if (zone.Cells.Count < 12)
		{
			throw new Exception("stockpile too small: " + zone.Cells.Count + " cells");
		}
		return zone;
	}

	// One stack per cell, spread over raw resources, food, medicine, apparel and weapons so the tree has many
	// open categories and the contents list several rows.
	private static void Fill(Zone_Stockpile zone)
	{
		List<IntVec3> cells = zone.Cells.ToList();
		int i = 0;
		void Put(Thing t)
		{
			if (i < cells.Count)
			{
				GenSpawn.Spawn(t, cells[i++], Map);
			}
		}
		foreach (string name in new string[] { "Steel", "WoodLog", "Silver", "ComponentIndustrial", "MealSimple", "MedicineIndustrial", "Cloth", "RawPotatoes", "Plasteel", "Gold", "Jade", "Leather_Plain" })
		{
			ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
			if (def != null)
			{
				Thing thing = ThingMaker.MakeThing(def);
				thing.stackCount = Math.Min(def.stackLimit, 25);
				Put(thing);
			}
		}
		foreach (string name in new string[] { "Apparel_Parka", "Apparel_Pants", "Apparel_CollarShirt", "MeleeWeapon_Knife", "Gun_Revolver", "Gun_BoltActionRifle" })
		{
			ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
			if (def != null)
			{
				Put(ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null));
			}
		}
	}

	internal static bool Free(IntVec3 c)
	{
		return c.Standable(Map) && !c.Fogged(Map) && c.GetFirstItem(Map) == null && c.GetFirstPawn(Map) == null && c.GetPlant(Map) == null && c.GetFirstBuilding(Map) == null && Map.reachability.CanReach(Home, c, PathEndMode.OnCell, TraverseMode.PassDoors, Danger.Deadly);
	}

	internal static IntVec3 FreeCell(int radius)
	{
		for (int r = radius; r < 60; r += 4)
		{
			if (CellFinder.TryFindRandomCellNear(Home, Map, r, (IntVec3 c) => Free(c) && Map.zoneManager.ZoneAt(c) == null, out var result))
			{
				return result;
			}
		}
		throw new Exception("no free cell");
	}

	// Unspawned things for the filter checks.
	internal static Thing Make(string defName, ThingDef stuff = null, QualityCategory? quality = null)
	{
		ThingDef def = ThingDef.Named(defName);
		Thing thing = ThingMaker.MakeThing(def, stuff ?? (def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null));
		if (quality.HasValue)
		{
			thing.TryGetComp<CompQuality>()?.SetQuality(quality.Value, ArtGenerationContext.Colony);
		}
		return thing;
	}

	internal static void Select(params object[] objects)
	{
		Find.Selector.ClearSelection();
		foreach (object obj in objects)
		{
			Find.Selector.Select(obj, playSound: false, forceDesignatorDeselect: false);
		}
	}
}
