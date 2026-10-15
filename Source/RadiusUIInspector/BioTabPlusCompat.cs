using System;
using System.Reflection;
using HarmonyLib;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

// Bio Tab+ draws its card at its own size (GetInspectBioCardSize) whatever size the tab was given, so in the pane it
// left an empty strip on the right. While the pane hosts the bio tab, that size is grown to the hosted area, during
// the tab's drawing only: the size the tab asks for (UpdateSize, TabRect, DoTabGUI) stays Bio Tab+'s own, or the pane,
// which is sized from it, would keep whatever width it had drawn at.
internal static class BioTabPlusCompat
{
	internal const string PackageId = "Axolki.BioTabPlus";

	internal const string PatchType = "BioTabEnhanced.BioTabPatch";

	// Bio Tab+ draws the card 17 px in from each side of the tab.
	private const float CardInset = 34f;

	private static bool bound;

	private static FieldInfo? settingsField;

	private static FieldInfo? enabledField;

	private static FieldInfo? hideRevertField;

	// Set while the hosted bio tab draws with Bio Tab+ on.
	internal static bool Filling;

	private static void Bind()
	{
		if (bound)
		{
			return;
		}
		bound = true;
		settingsField = AccessTools.Field(ModCompat.FindType("BioTabEnhanced.BioTabEnhancedMod"), "Settings");
		enabledField = AccessTools.Field(ModCompat.FindType("BioTabEnhanced.BioTabSettings"), "enabled");
		hideRevertField = AccessTools.Field(ModCompat.FindType("BioTabEnhanced.BioTabSettings"), "hideRevertButton");
		if (settingsField == null || enabledField == null)
		{
			Log.Warning("[Radius UI - Inspector] Bio Tab+ is active but its settings did not resolve (version drift?) - its card keeps its own width in the pane and the Modern Bio icon is not added to its header.");
		}
	}

	// Same rule as Bio Tab+'s IsEnabled: no settings yet means on.
	internal static bool Enabled
	{
		get
		{
			Bind();
			if (settingsField == null || enabledField == null)
			{
				return false;
			}
			object? value = settingsField.GetValue(null);
			return value == null || (bool)enabledField.GetValue(value);
		}
	}

	// Bio Tab+'s setting that hides its header icon back to the vanilla card.
	internal static bool RevertHidden
	{
		get
		{
			Bind();
			object? value = settingsField?.GetValue(null);
			return value != null && hideRevertField != null && (bool)hideRevertField.GetValue(value);
		}
	}

	internal static void Disable()
	{
		Bind();
		object? value = settingsField?.GetValue(null);
		if (value != null)
		{
			enabledField!.SetValue(value, false);
		}
	}

	internal static bool Hosting => InspectorCore.Active && InspectorCore.HostingTab && !InspectorMod.Settings.showVanillaTabs;

	internal static void FillSize(ref Vector2 size)
	{
		Rect rect = InspectorCore.HostedTabLocalRect();
		// Only ever grown: a pane narrower than the card keeps Bio Tab+'s own layout.
		size.x = Mathf.Max(size.x, rect.width - CardInset);
		size.y = Mathf.Max(size.y, rect.height - CardInset);
	}
}

[HarmonyPatch(typeof(ITab_Pawn_Character), "FillTab")]
internal static class Patch_BioTabPlus_FillTab
{
	private static bool Prepare()
	{
		return ModCompat.IsActive(BioTabPlusCompat.PackageId);
	}

	// First: Bio Tab+'s own prefix reads the card size and draws within this call.
	[HarmonyPriority(Priority.First)]
	private static void Prefix()
	{
		BioTabPlusCompat.Filling = BioTabPlusCompat.Hosting && BioTabPlusCompat.Enabled;
	}

	private static Exception? Finalizer(Exception? __exception)
	{
		BioTabPlusCompat.Filling = false;
		return __exception;
	}
}

[HarmonyPatch]
internal static class Patch_BioTabPlus_CardSize
{
	private static bool Prepare()
	{
		return ModCompat.IsActive(BioTabPlusCompat.PackageId) && TargetMethod() != null;
	}

	private static MethodBase? TargetMethod()
	{
		Type? type = ModCompat.FindType(BioTabPlusCompat.PatchType);
		return (type == null) ? null : AccessTools.Method(type, "GetInspectBioCardSize");
	}

	private static void Postfix(ref Vector2 __result)
	{
		if (BioTabPlusCompat.Filling)
		{
			BioTabPlusCompat.FillSize(ref __result);
		}
	}
}
