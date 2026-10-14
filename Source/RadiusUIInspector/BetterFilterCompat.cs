using System;
using System.Collections.Generic;
using RadiusUI.Framework;
using Verse;

namespace RadiusUIInspector;

// [UR] Better Filter draws two fake items of its "filter" category as buttons inside the vanilla tree. The
// storage panel has no such rows: the fake items are kept out of it, and its advanced filter dialog opens
// from a button of the panel instead.
internal static class BetterFilterCompat
{
	internal const string PackageId = "uranv.betterfilter";

	private const string DialogType = "BetterFilter.Contents.Dialog.Dialog_AdvancedFilters";

	private static bool? active;

	private static Type? dialogType;

	internal static bool Active
	{
		get
		{
			if (!active.HasValue)
			{
				active = ModCompat.IsActive(PackageId);
				if (active.Value)
				{
					dialogType = ModCompat.FindType(DialogType);
					if (dialogType == null)
					{
						Log.Warning("[Radius UI - Inspector] [UR] Better Filter is active but " + DialogType + " did not resolve (version drift?) - its advanced filter button is left out.");
					}
				}
			}
			return active.Value;
		}
	}

	internal static bool CanOpenAdvanced => Active && dialogType != null;

	internal static bool IsDummy(ThingDef def)
	{
		string defName = def.defName;
		return defName == "UR_DummyFilterItem" || defName == "UR_DummyApplyFilterItem";
	}

	// Same context as Better Filter's own button: the defs the parent filter lets in, or every storable def.
	internal static void OpenAdvanced(ThingFilter filter, ThingFilter? parentFilter)
	{
		if (!CanOpenAdvanced)
		{
			return;
		}
		try
		{
			List<ThingDef>? context = null;
			if (parentFilter != null)
			{
				context = new List<ThingDef>();
				foreach (ThingDef def in parentFilter.AllowedThingDefs)
				{
					if (def.PlayerAcquirable && def.virtualDefParent == null && !IsDummy(def))
					{
						context.Add(def);
					}
				}
				if (context.Count == 0)
				{
					context = null;
				}
			}
			Find.WindowStack.Add((Window)Activator.CreateInstance(dialogType, filter, context));
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] [UR] Better Filter advanced dialog failed to open: " + ex.Message, 5351410);
		}
	}
}
