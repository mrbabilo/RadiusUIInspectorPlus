using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

// One special filter per material, taken over from Material Filter (CheaterEater): refusing a material refuses
// everything made of it. The defs keep Material Filter's defNames, so a save that refused a material keeps it
// refused. While Material Filter itself runs it makes the same defs: none are made here, and the storage panel
// shows its defs in the same materials section.
//
// Material Filter also refused armour and weapons through the materials of their recipe (costList), and kept
// them only while one of those materials was allowed. That rule patched every ThingFilter.Allows(Thing) call;
// here a material filter acts through vanilla alone: on the material a thing is made of.
[StaticConstructorOnStartup]
public static class MaterialFilters
{
	internal const string DefPrefix = "MaterialFilter_allow";

	internal const string PackageId = "CheaterEater.MaterialFilter";

	// The defs made here, sorted by label. Empty while Material Filter runs.
	internal static readonly List<SpecialThingFilterDef> Generated = new List<SpecialThingFilterDef>();

	private static readonly Dictionary<SpecialThingFilterDef, ThingDef?> stuffOf = new Dictionary<SpecialThingFilterDef, ThingDef?>();

	static MaterialFilters()
	{
		if (ModCompat.IsActive(PackageId))
		{
			return;
		}
		try
		{
			ModContentPack? content = InspectorMod.Instance?.Content;
			foreach (ThingDef stuff in DefDatabase<ThingDef>.AllDefsListForReading)
			{
				if (stuff.stuffProps?.categories == null || stuff.stuffProps.categories.Count == 0 || DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail(DefPrefix + stuff.defName) != null)
				{
					continue;
				}
				SpecialThingFilterDef sf = new SpecialThingFilterDef
				{
					defName = DefPrefix + stuff.defName,
					label = stuff.label,
					description = "RUI.Inspector.MaterialFilterDesc".Translate(stuff.label),
					parentCategory = ThingCategoryDefOf.Root,
					allowedByDefault = true,
					saveKey = "allow" + stuff.defName,
					configurable = true,
					workerClass = typeof(MaterialFilterWorker),
					modContentPack = content
				};
				ThingCategoryDefOf.Root.childSpecialFilters ??= new List<SpecialThingFilterDef>();
				ThingCategoryDefOf.Root.childSpecialFilters.Add(sf);
				DefGenerator.AddImpliedDef(sf);
				((MaterialFilterWorker)sf.Worker).stuff = stuff;
				stuffOf[sf] = stuff;
				Generated.Add(sf);
			}
			Generated.SortBy((SpecialThingFilterDef d) => d.label);
		}
		catch (Exception ex)
		{
			Log.Error("[Radius UI - Inspector] material filters could not be made: " + ex);
		}
	}

	private static readonly ConditionalWeakTable<ThingFilter, List<SpecialThingFilterDef>> relevantMemo = new ConditionalWeakTable<ThingFilter, List<SpecialThingFilterDef>>();

	// The materials something the parent filter allows can be made of (all of them without a parent).
	// Parent filters are fixed (a recipe's ingredients, every apparel, a building's storable defs).
	internal static List<SpecialThingFilterDef> RelevantTo(ThingFilter? parentFilter)
	{
		if (parentFilter == null)
		{
			return Generated;
		}
		if (!relevantMemo.TryGetValue(parentFilter, out var list))
		{
			list = new List<SpecialThingFilterDef>();
			List<ThingDef> stuffed = new List<ThingDef>();
			foreach (ThingDef def in parentFilter.AllowedThingDefs)
			{
				if (def.MadeFromStuff)
				{
					stuffed.Add(def);
				}
			}
			for (int i = 0; i < Generated.Count; i++)
			{
				SpecialThingFilterWorker worker = Generated[i].Worker;
				for (int j = 0; j < stuffed.Count; j++)
				{
					if (worker.CanEverMatch(stuffed[j]))
					{
						list.Add(Generated[i]);
						break;
					}
				}
			}
			relevantMemo.Add(parentFilter, list);
		}
		return list;
	}

	internal static bool IsMaterial(SpecialThingFilterDef sf)
	{
		return sf.defName != null && sf.defName.StartsWith(DefPrefix, StringComparison.Ordinal);
	}

	// The material behind a material filter, ours or Material Filter's own.
	internal static ThingDef? StuffOf(SpecialThingFilterDef sf)
	{
		if (!stuffOf.TryGetValue(sf, out var stuff))
		{
			stuff = IsMaterial(sf) ? DefDatabase<ThingDef>.GetNamedSilentFail(sf.defName.Substring(DefPrefix.Length)) : null;
			stuffOf[sf] = stuff;
		}
		return stuff;
	}
}

public class MaterialFilterWorker : SpecialThingFilterWorker
{
	internal ThingDef? stuff;

	public override bool Matches(Thing t)
	{
		return t.Stuff != null && t.Stuff == stuff;
	}

	public override bool CanEverMatch(ThingDef def)
	{
		return stuff?.stuffProps != null && stuff.stuffProps.CanMake(def);
	}
}

// The vanilla tree (storage tab when the panel does not own it, bills, apparel policies) would list every
// material among its special filters: they stay out of it, and a bar above the tree opens Dialog_Materials
// with the materials that tree can take. First, so a prefix that replaces the window ([UR] Better Filter)
// still gets the list and the narrowed rect.
[HarmonyPatch(typeof(ThingFilterUI), "DoThingFilterConfigWindow")]
[HarmonyPriority(Priority.First)]
internal static class Patch_ThingFilterUI_HideMaterialFilters
{
	private const float BarH = 30f;

	private static void Prefix(ref Rect rect, ThingFilter filter, ThingFilter parentFilter, ref IEnumerable<SpecialThingFilterDef> forceHiddenFilters)
	{
		if (MaterialFilters.Generated.Count == 0)
		{
			return;
		}
		forceHiddenFilters = (forceHiddenFilters == null) ? MaterialFilters.Generated : forceHiddenFilters.Concat(MaterialFilters.Generated);
		try
		{
			List<SpecialThingFilterDef> materials = MaterialFilters.RelevantTo(parentFilter);
			if (filter == null || materials.Count == 0 || rect.height < BarH * 3f)
			{
				return;
			}
			int allowed = 0;
			for (int i = 0; i < materials.Count; i++)
			{
				if (filter.Allows(materials[i]))
				{
					allowed++;
				}
			}
			Rect button = new Rect(rect.x, rect.y, rect.width, BarH - 4f);
			string label = "RUI.Inspector.StorageMaterials".Translate() + "  " + allowed + "/" + materials.Count;
			if (StoragePanel.SmallButton(button, label, true, "Action/Popout"))
			{
				Dialog_Materials? open = Find.WindowStack.WindowOfType<Dialog_Materials>();
				open?.Close();
				Find.WindowStack.Add(new Dialog_Materials(filter, materials));
			}
			TooltipHandler.TipRegion(button, "RUI.Inspector.StorageMaterialsTip".Translate());
			rect.yMin += BarH;
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] materials bar failed: " + ex.Message, 5351412);
		}
	}
}

// Material Filter saved refused materials under their own node: read it once into the vanilla list, which
// carries them from the next save on. Nothing is written here. The list is filled directly: SetAllow would
// run the owner's change callback in the middle of loading.
[HarmonyPatch(typeof(ThingFilter), "ExposeData")]
internal static class Patch_ThingFilter_ReadMaterialFilterSave
{
	private static readonly AccessTools.FieldRef<ThingFilter, List<SpecialThingFilterDef>> DisallowedSpecialFilters = AccessTools.FieldRefAccess<ThingFilter, List<SpecialThingFilterDef>>("disallowedSpecialFilters");

	private static void Postfix(ThingFilter __instance)
	{
		if (Scribe.mode != LoadSaveMode.LoadingVars || MaterialFilters.Generated.Count == 0)
		{
			return;
		}
		List<string>? names = null;
		Scribe_Collections.Look(ref names, "materialFilterDisallowed", LookMode.Value);
		if (names == null)
		{
			return;
		}
		ref List<SpecialThingFilterDef> list = ref DisallowedSpecialFilters(__instance);
		list ??= new List<SpecialThingFilterDef>();
		for (int i = 0; i < names.Count; i++)
		{
			SpecialThingFilterDef? sf = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail(names[i]);
			if (sf != null && MaterialFilters.IsMaterial(sf) && !list.Contains(sf))
			{
				list.Add(sf);
			}
		}
	}
}
