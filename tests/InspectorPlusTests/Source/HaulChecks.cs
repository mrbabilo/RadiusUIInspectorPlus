using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RadiusUIInspector;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RUIInspectorTests;

// Pawn behaviour with the special filters: two stockpiles with opposite special filters, things dropped on the
// ground next to them, then the game ticks until the colonists have hauled everything. Each scenario checks where
// every thing ended up, so a filter is held to what pawns do, not only to what ThingFilter.Allows answers.
// The rest of the map stays out of the way: every other haulable is forbidden and the fixture's storage refuses
// everything until the last step puts them back.
internal static class HaulChecks
{
	private const int MaxTicks = 8000;

	private const int CheckEvery = 250;

	private static Zone_Stockpile zoneA;

	private static Zone_Stockpile zoneB;

	private static readonly List<Thing> forbidden = new List<Thing>();

	private static readonly List<(StorageSettings settings, ThingFilter copy)> parked = new List<(StorageSettings, ThingFilter)>();

	private static int ticksUsed;

	// Every thing a scenario dropped, so the next one starts from clean ground.
	private static readonly List<Thing> dropped = new List<Thing>();

	private static string Expect(bool ok, string why)
	{
		return ok ? null : why;
	}

	private static string First(params string[] reasons)
	{
		return reasons.FirstOrDefault((string r) => r != null);
	}

	private static SpecialThingFilterDef Special(string defName)
	{
		return DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail(defName) ?? throw new Exception("special filter " + defName + " not loaded");
	}

	internal static IEnumerable<ValueTuple<string, Action, int, Func<string>>> Steps()
	{
		yield return ("haul: setup, two zones, rest of the map out of the way, colonists on hauling", Setup, 0, delegate
		{
			int haulers = Fixture.Pawns.Count((Pawn p) => !p.WorkTypeIsDisabled(WorkTypeDefOf.Hauling));
			return First(Expect(zoneA.Cells.Count >= 9 && zoneB.Cells.Count >= 9, "zones " + zoneA.Cells.Count + " / " + zoneB.Cells.Count + " cells"), Expect(haulers >= 2, "only " + haulers + " colonists can haul"));
		});
		yield return Scenario("haul: useless apparel goes to the useless zone, the rest to the other", delegate(ThingFilter a, ThingFilter b)
		{
			AllowOnly(a, ThingCategoryDefOf.Apparel);
			AllowOnly(b, ThingCategoryDefOf.Apparel);
			a.SetAllow(Special("NonUselessApparelFilter"), allow: false);
			b.SetAllow(Special("UselessApparelFilter"), allow: false);
		}, () => new List<(Thing, char)>
		{
			(Worn("Apparel_Parka", 0.3f, QualityCategory.Normal), 'A'),
			(Worn("Apparel_Pants", 1f, QualityCategory.Awful), 'A'),
			(Worn("Apparel_CollarShirt", 1f, QualityCategory.Good), 'B')
		});
		yield return Scenario("haul: useless weapons go to the useless zone, the rest to the other", delegate(ThingFilter a, ThingFilter b)
		{
			AllowOnly(a, ThingCategoryDefOf.Weapons);
			AllowOnly(b, ThingCategoryDefOf.Weapons);
			a.SetAllow(Special("NonUselessWeaponFilter"), allow: false);
			b.SetAllow(Special("UselessWeaponFilter"), allow: false);
		}, () => new List<(Thing, char)>
		{
			(Worn("MeleeWeapon_Knife", 0.2f, QualityCategory.Normal), 'A'),
			(Worn("Gun_Revolver", 1f, QualityCategory.Good), 'B')
		});
		yield return Scenario("haul: things with a quality and things without split by the quality pair", delegate(ThingFilter a, ThingFilter b)
		{
			a.SetAllowAll(null);
			b.SetAllowAll(null);
			a.SetAllow(Special("UsefulFilters_AllowNoQuality"), allow: false);
			b.SetAllow(Special("UsefulFilters_AllowHasQuality"), allow: false);
		}, () => new List<(Thing, char)>
		{
			(Worn("Apparel_Parka", 1f, QualityCategory.Normal), 'A'),
			(Stack("Steel", 20), 'B')
		});
		yield return Scenario("haul: rottable food and non-rottable things split by the rottable pair", delegate(ThingFilter a, ThingFilter b)
		{
			a.SetAllowAll(null);
			b.SetAllowAll(null);
			a.SetAllow(Special("UsefulFilters_AllowNonRottable"), allow: false);
			b.SetAllow(Special("UsefulFilters_AllowRottable"), allow: false);
		}, () => new List<(Thing, char)>
		{
			(Stack("RawPotatoes", 20), 'A'),
			(Stack("Steel", 20), 'B')
		});
		yield return Scenario("haul: flammable and non-flammable things split by the flammable pair", delegate(ThingFilter a, ThingFilter b)
		{
			a.SetAllowAll(null);
			b.SetAllowAll(null);
			a.SetAllow(Special("UsefulFilters_AllowNonFlammable"), allow: false);
			b.SetAllow(Special("UsefulFilters_AllowFlammable"), allow: false);
		}, () => new List<(Thing, char)>
		{
			(Stack("WoodLog", 20), 'A'),
			(Stack("Steel", 20), 'B')
		});
		yield return Scenario("haul: stackable and non-stackable things split by the stackable pair", delegate(ThingFilter a, ThingFilter b)
		{
			a.SetAllowAll(null);
			b.SetAllowAll(null);
			a.SetAllow(Special("RUI_AllowNonStackable"), allow: false);
			b.SetAllow(Special("RUI_AllowStackable"), allow: false);
		}, () => new List<(Thing, char)>
		{
			(Stack("Steel", 20), 'A'),
			(Worn("Apparel_Parka", 1f, QualityCategory.Normal), 'B')
		});
		yield return Scenario("haul: things with and without hit points split by the hit point pair", delegate(ThingFilter a, ThingFilter b)
		{
			a.SetAllowAll(null);
			b.SetAllowAll(null);
			a.SetAllow(Special("RUI_AllowNoHitPoints"), allow: false);
			b.SetAllow(Special("RUI_AllowHasHitPoints"), allow: false);
		}, () => new List<(Thing, char)>
		{
			(Stack("WoodLog", 20), 'A'),
			(Stack(NoHitPointsDef(), 20), 'B')
		});
		yield return Scenario("haul: a steel-made weapon avoids the zone refusing steel", delegate(ThingFilter a, ThingFilter b)
		{
			AllowOnly(a, ThingCategoryDefOf.Weapons);
			AllowOnly(b, ThingCategoryDefOf.Weapons);
			a.SetAllow(Special("MaterialFilter_allowSteel"), allow: false);
		}, () => new List<(Thing, char)> { (Worn("MeleeWeapon_Knife", 1f, QualityCategory.Normal, ThingDefOf.Steel), 'B') });

		// A thing already stored becomes refused: the panel shows it leaving, then a colonist moves it.
		Thing stored = null;
		yield return ("haul: a stored thing refused by a special filter shows as leaving in the panel", delegate
		{
			ClearZones();
			zoneA.settings.filter.SetAllowAll(null);
			zoneB.settings.filter.SetAllowAll(null);
			zoneB.settings.Priority = StoragePriority.Low;
			stored = Stack("Steel", 20);
			GenSpawn.Spawn(stored, zoneA.Cells.First(), Fixture.Map);
			dropped.Add(stored);
			zoneA.settings.filter.SetAllow(Special("UsefulFilters_AllowNonFlammable"), allow: false);
			Fixture.Select(zoneA);
			OpenStorage();
		}, 40, delegate
		{
			Dictionary<ThingDef, int> leaving = (Dictionary<ThingDef, int>)AccessTools.Field(typeof(StoragePanel), "leavingCount").GetValue(null);
			leaving.TryGetValue(ThingDefOf.Steel, out var count);
			return First(Expect(StorageField("lastParent") == zoneA, "panel drew " + StorageField("lastParent")), Expect(count == 20, "steel leaving " + count + ", expected 20"));
		});
		yield return ("haul: a colonist moves the refused stack to the zone that takes it", delegate
		{
			Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
			Fixture.Select();
		}, 0, delegate
		{
			string reason = RunUntil(() => CountIn(zoneB, ThingDefOf.Steel) == 20 && CountIn(zoneA, ThingDefOf.Steel) == 0);
			zoneB.settings.Priority = StoragePriority.Normal;
			return First(reason, Expect(CountIn(zoneB, ThingDefOf.Steel) == 20, "steel in B " + CountIn(zoneB, ThingDefOf.Steel) + ", in A " + CountIn(zoneA, ThingDefOf.Steel)));
		});

		// Saves keep refused special filters by defName.
		yield return ("haul: a zone filter with refused special filters survives save and load", null, 0, delegate
		{
			ThingFilter f = zoneA.settings.filter;
			f.SetAllowAll(null);
			string[] refused = new string[4] { "UselessApparelFilter", "UsefulFilters_AllowRottable", "RUI_AllowStackable", "MaterialFilter_allowSteel" };
			foreach (string name in refused)
			{
				f.SetAllow(Special(name), allow: false);
			}
			string path = Path.Combine(Path.GetDirectoryName(Boot.ResultsPath), "ruitest-zonefilter.xml");
			Scribe.saver.InitSaving(path, "filter");
			Scribe_Deep.Look(ref f, "f");
			Scribe.saver.FinalizeSaving();
			ThingFilter loaded = null;
			Scribe.loader.InitLoading(path);
			Scribe_Deep.Look(ref loaded, "f");
			Scribe.loader.FinalizeLoading();
			File.Delete(path);
			if (loaded == null)
			{
				return "nothing loaded";
			}
			List<string> differ = DefDatabase<SpecialThingFilterDef>.AllDefs.Where((SpecialThingFilterDef s) => loaded.Allows(s) != f.Allows(s)).Select((SpecialThingFilterDef s) => s.defName).ToList();
			return First(Expect(differ.Count == 0, "specials differ after load: " + string.Join(",", differ)), Expect(refused.All((string n) => !loaded.Allows(Special(n))), "a refused special came back allowed"));
		});
		yield return ("haul: teardown, the rest of the map back as it was", Teardown, 0, delegate
		{
			return Expect(Fixture.Stockpile.settings.filter.AllowedDefCount > 0, "fixture stockpile still refuses everything");
		});
	}

	private static object StorageField(string name)
	{
		return AccessTools.Field(typeof(StoragePanel), name).GetValue(null);
	}

	private static void OpenStorage()
	{
		if (Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect)
		{
			Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect, playSound: false);
		}
		IInspectPane pane = (MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;
		InspectTabBase tab = pane.CurTabs?.FirstOrDefault((InspectTabBase t) => t is ITab_Storage) ?? throw new Exception("ITab_Storage not offered");
		if (pane.OpenTabType != typeof(ITab_Storage))
		{
			TabPlumbing.Toggle(tab, pane);
		}
	}

	// One scenario: clear both zones, configure them, drop the things on free ground, tick until every thing sits in
	// its expected zone (or MaxTicks), then name each thing that did not.
	private static ValueTuple<string, Action, int, Func<string>> Scenario(string name, Action<ThingFilter, ThingFilter> configure, Func<List<(Thing thing, char zone)>> things)
	{
		List<(Thing thing, char zone)> placed = null;
		return (name, delegate
		{
			ClearZones();
			configure(zoneA.settings.filter, zoneB.settings.filter);
			placed = things();
			foreach ((Thing thing, char zone) item in placed)
			{
				GenSpawn.Spawn(item.thing, Fixture.FreeCell(5), Fixture.Map);
				dropped.Add(item.thing);
			}
		}, 0, delegate
		{
			foreach ((Thing thing, char zone) item in placed)
			{
				Zone_Stockpile target = ((item.zone == 'A') ? zoneA : zoneB);
				if (!target.settings.AllowedToAccept(item.thing))
				{
					return item.thing.LabelCap + " is refused by its own expected zone " + item.zone;
				}
				Zone_Stockpile other = ((item.zone == 'A') ? zoneB : zoneA);
				if (other.settings.AllowedToAccept(item.thing))
				{
					return item.thing.LabelCap + " is also accepted by the other zone";
				}
			}
			string timeout = RunUntil(() => placed.All(((Thing thing, char zone) p) => Where(p.thing) == p.zone.ToString()));
			List<string> wrong = (from p in placed
				where Where(p.thing) != p.zone.ToString()
				select p.thing.def.label + " expected " + p.zone + ", is " + Where(p.thing)).ToList();
			return (wrong.Count == 0) ? null : (string.Join("; ", wrong) + ((timeout != null) ? (" (" + timeout + ")") : ""));
		});
	}

	// "A", "B", "ground", "carried by X" or "gone": where a thing (or the stack it merged into) is now.
	private static string Where(Thing thing)
	{
		if (thing.Destroyed)
		{
			return "gone";
		}
		if (!thing.Spawned)
		{
			Pawn carrier = (thing.ParentHolder as Pawn_CarryTracker)?.pawn;
			return (carrier != null) ? ("carried by " + carrier.LabelShort) : "unspawned";
		}
		if (zoneA.ContainsCell(thing.Position))
		{
			return "A";
		}
		if (zoneB.ContainsCell(thing.Position))
		{
			return "B";
		}
		return "ground";
	}

	private static int CountIn(Zone_Stockpile zone, ThingDef def)
	{
		int num = 0;
		foreach (IntVec3 cell in zone.Cells)
		{
			foreach (Thing thing in cell.GetThingList(Fixture.Map))
			{
				if (thing.def == def)
				{
					num += thing.stackCount;
				}
			}
		}
		return num;
	}

	// Ticks the game in this frame, checking every CheckEvery ticks; null when done, otherwise why it stopped.
	private static string RunUntil(Func<bool> done)
	{
		int i = 0;
		while (i < MaxTicks)
		{
			for (int j = 0; j < CheckEvery; j++)
			{
				Find.TickManager.DoSingleTick();
			}
			i += CheckEvery;
			if (done())
			{
				ticksUsed += i;
				return null;
			}
		}
		ticksUsed += i;
		return "not done after " + MaxTicks + " ticks";
	}

	private static Thing Stack(string defName, int count)
	{
		return Stack(ThingDef.Named(defName), count);
	}

	private static Thing Stack(ThingDef def, int count)
	{
		Thing thing = ThingMaker.MakeThing(def);
		thing.stackCount = Math.Min(count, def.stackLimit);
		return thing;
	}

	// A stackable resource without hit points, from the game's own defs.
	private static ThingDef NoHitPointsDef()
	{
		return new string[3] { "Silver", "Gold", "Uranium" }.Select(DefDatabase<ThingDef>.GetNamedSilentFail).FirstOrDefault((ThingDef d) => d != null && !d.useHitPoints) ?? DefDatabase<ThingDef>.AllDefs.First((ThingDef d) => d.category == ThingCategory.Item && !d.useHitPoints && d.stackLimit > 1 && d.EverHaulable && !d.IsCorpse);
	}

	private static Thing Worn(string defName, float hitPoints, QualityCategory quality, ThingDef stuff = null)
	{
		Thing thing = Fixture.Make(defName, stuff, quality);
		thing.HitPoints = Mathf.Max(1, Mathf.RoundToInt((float)thing.MaxHitPoints * hitPoints));
		return thing;
	}

	private static void AllowOnly(ThingFilter filter, ThingCategoryDef category)
	{
		filter.SetDisallowAll();
		filter.SetAllow(category, allow: true);
	}

	private static void ClearZones()
	{
		foreach (Zone_Stockpile zone in new Zone_Stockpile[2] { zoneA, zoneB })
		{
			foreach (IntVec3 cell in zone.Cells)
			{
				foreach (Thing item in cell.GetThingList(Fixture.Map).Where((Thing t) => t.def.category == ThingCategory.Item).ToList())
				{
					item.Destroy();
				}
			}
			zone.settings.filter.SetAllowAll(null);
			zone.settings.Priority = StoragePriority.Normal;
		}
		foreach (Thing thing in dropped)
		{
			if (!thing.Destroyed)
			{
				thing.Destroy();
			}
		}
		dropped.Clear();
		// Things a colonist still carries from the previous scenario.
		foreach (Pawn pawn in Fixture.Pawns)
		{
			pawn.carryTracker.DestroyCarriedThing();
			pawn.jobs.StopAll();
		}
	}

	private static void Setup()
	{
		Find.TickManager.Pause();
		Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
		Fixture.Select();
		zoneA = Stockpile();
		zoneB = Stockpile();
		// Everything else stays put: other haulables forbidden, the fixture's storage refusing everything.
		foreach (Thing thing in Fixture.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).ToList())
		{
			if (thing.Spawned && !thing.IsForbidden(Faction.OfPlayer))
			{
				thing.SetForbidden(value: true, warnOnFail: false);
				forbidden.Add(thing);
			}
		}
		List<StorageSettings> list = new List<StorageSettings> { Fixture.Stockpile.settings };
		list.AddRange(Fixture.Shelves.Select((Building_Storage s) => s.settings));
		foreach (StorageSettings settings in list)
		{
			ThingFilter copy = new ThingFilter();
			copy.CopyAllowancesFrom(settings.filter);
			parked.Add((settings, copy));
			settings.filter.SetDisallowAll();
		}
		// Colonists only haul, are fed, rested and entertained, and may work at any hour.
		foreach (Pawn pawn in Fixture.Pawns)
		{
			pawn.workSettings?.EnableAndInitialize();
			pawn.workSettings?.DisableAll();
			if (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling))
			{
				pawn.workSettings?.SetPriority(WorkTypeDefOf.Hauling, 1);
			}
			Need need = pawn.needs?.food;
			if (need != null)
			{
				need.CurLevelPercentage = 1f;
			}
			if (pawn.needs?.rest != null)
			{
				pawn.needs.rest.CurLevelPercentage = 1f;
			}
			if (pawn.needs?.joy != null)
			{
				pawn.needs.joy.CurLevelPercentage = 1f;
			}
			if (pawn.timetable != null)
			{
				for (int i = 0; i < 24; i++)
				{
					pawn.timetable.SetAssignment(i, TimeAssignmentDefOf.Work);
				}
			}
			pawn.jobs.StopAll();
		}
	}

	// The map is crowded by the earlier checks: a few centers may be tried before one leaves enough free cells.
	private static Zone_Stockpile Stockpile()
	{
		Exception last = null;
		for (int i = 0; i < 8; i++)
		{
			try
			{
				return Fixture.NewStockpile(6);
			}
			catch (Exception ex)
			{
				last = ex;
			}
		}
		throw last;
	}

	private static void Teardown()
	{
		ClearZones();
		foreach (Thing thing in forbidden)
		{
			if (thing.Spawned)
			{
				thing.SetForbidden(value: false, warnOnFail: false);
			}
		}
		forbidden.Clear();
		foreach ((StorageSettings settings, ThingFilter copy) item in parked)
		{
			item.settings.filter.CopyAllowancesFrom(item.copy);
		}
		parked.Clear();
		zoneA.Delete();
		zoneB.Delete();
		TestRunner.Write("# haul scenarios: " + ticksUsed + " game ticks");
	}
}
