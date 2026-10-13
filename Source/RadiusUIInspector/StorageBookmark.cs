using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RadiusUIInspector;

// A saved storage filter, shared across saves (merged from RimKeeper Storage and Filters Helper).
// Defs are kept by defName: settings load before the DefDatabase exists, and a def from a removed mod must
// drop out silently instead of failing the whole list.
public class StorageBookmark : IExposable
{
	public enum Mode
	{
		Replace,
		Add,
		Remove
	}

	public string name = "";

	public List<string> allowedDefs = new List<string>();

	public List<string> disallowedSpecialFilters = new List<string>();

	public FloatRange hitPoints = FloatRange.ZeroToOne;

	public QualityRange quality = QualityRange.All;

	public static StorageBookmark Capture(string name, ThingFilter filter)
	{
		StorageBookmark bookmark = new StorageBookmark
		{
			name = name,
			hitPoints = filter.AllowedHitPointsPercents,
			quality = filter.AllowedQualityLevels
		};
		foreach (ThingDef def in filter.AllowedThingDefs)
		{
			bookmark.allowedDefs.Add(def.defName);
		}
		// Only configurable filters the player can toggle; hiddenSpecialFilters is unrelated to what is disallowed.
		List<SpecialThingFilterDef> specials = DefDatabase<SpecialThingFilterDef>.AllDefsListForReading;
		for (int i = 0; i < specials.Count; i++)
		{
			if (specials[i].configurable && !filter.Allows(specials[i]))
			{
				bookmark.disallowedSpecialFilters.Add(specials[i].defName);
			}
		}
		return bookmark;
	}

	public void ApplyTo(ThingFilter filter, ThingFilter? parentFilter, Mode mode)
	{
		if (mode == Mode.Replace)
		{
			// SetDisallowAll clears the defs but re-allows every configurable special filter.
			filter.SetDisallowAll();
			for (int i = 0; i < disallowedSpecialFilters.Count; i++)
			{
				SpecialThingFilterDef special = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail(disallowedSpecialFilters[i]);
				if (special != null && special.configurable)
				{
					filter.SetAllow(special, allow: false);
				}
			}
			if (filter.allowedHitPointsConfigurable)
			{
				filter.AllowedHitPointsPercents = hitPoints;
			}
			if (filter.allowedQualitiesConfigurable)
			{
				filter.AllowedQualityLevels = quality;
			}
		}
		bool allow = mode != Mode.Remove;
		for (int j = 0; j < allowedDefs.Count; j++)
		{
			ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(allowedDefs[j]);
			if (def != null && (!allow || parentFilter == null || parentFilter.Allows(def)))
			{
				filter.SetAllow(def, allow);
			}
		}
	}

	public void ExposeData()
	{
		Scribe_Values.Look(ref name, "name", "");
		Scribe_Collections.Look(ref allowedDefs, "allowedDefs", LookMode.Value);
		Scribe_Collections.Look(ref disallowedSpecialFilters, "disallowedSpecialFilters", LookMode.Value);
		Scribe_Values.Look(ref hitPoints, "hitPoints", FloatRange.ZeroToOne);
		Scribe_Values.Look(ref quality, "quality", QualityRange.All);
		if (allowedDefs == null)
		{
			allowedDefs = new List<string>();
		}
		if (disallowedSpecialFilters == null)
		{
			disallowedSpecialFilters = new List<string>();
		}
	}
}
