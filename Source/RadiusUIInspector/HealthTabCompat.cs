using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

// Radius UI - Health Tab writes its own size into ITab_Pawn_Health while its modern tab is on, and nothing puts the
// old size back when the player switches to the vanilla tab. The game and Nice Health Tab only set that size in the
// constructor (Nice Health Tab then fixes the width alone), so the height stayed the Radius one and the vanilla or
// Nice Health Tab card was drawn stretched. The size a health tab has before Radius first touches it is kept here
// and given back on the switch. The tab instances are shared and built while defs load, before this mod patches
// anything, so that size is read on the first UpdateSize rather than in the constructor.
internal static class HealthTabCompat
{
	internal const string PackageId = "astryl.RadiusUI.HealthTab";

	private const string DrawerType = "RadiusUI.HealthTab.HealthTabDrawer";

	private static readonly Vector2 VanillaSize = new Vector2(630f, 430f);

	private static bool? active;

	private static Func<bool>? cfgModern;

	// Size each health tab had on its first UpdateSize, before any other postfix (Radius) rewrote it.
	private static readonly Dictionary<InspectTabBase, Vector2> builtSize = new Dictionary<InspectTabBase, Vector2>();

	// Health tabs last seen while the Radius modern tab was on.
	private static readonly HashSet<InspectTabBase> sizedByRadius = new HashSet<InspectTabBase>();

	internal static bool Active
	{
		get
		{
			if (!active.HasValue)
			{
				active = ModCompat.IsActive(PackageId);
				if (active.Value)
				{
					MethodInfo? methodInfo = ModCompat.FindType(DrawerType)?.GetProperty("CfgModern", BindingFlags.Static | BindingFlags.Public)?.GetGetMethod();
					if (methodInfo != null)
					{
						cfgModern = AccessTools.MethodDelegate<Func<bool>>(methodInfo);
					}
					else
					{
						Log.Warning("[Radius UI - Inspector] Radius UI - Health Tab is active but " + DrawerType + ".CfgModern did not resolve (version drift?) - the health tab size is not restored when switching back to vanilla.");
					}
				}
			}
			return active.Value && cfgModern != null;
		}
	}

	internal static void AfterUpdateSize(InspectTabBase tab, ref Vector2 size)
	{
		if (!Active)
		{
			return;
		}
		if (!builtSize.ContainsKey(tab))
		{
			builtSize[tab] = size;
		}
		bool flag;
		try
		{
			flag = cfgModern!();
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Reading Radius Health Tab mode failed: " + ex.Message, 5351401);
			return;
		}
		if (flag)
		{
			sizedByRadius.Add(tab);
		}
		else if (sizedByRadius.Remove(tab))
		{
			size = (builtSize.TryGetValue(tab, out var value) ? value : VanillaSize);
		}
	}
}

[HarmonyPatch(typeof(InspectTabBase), "UpdateSize")]
internal static class Patch_HealthTabCompat_UpdateSize
{
	// First: the size is read before Radius rewrites it, and Nice Health Tab still fixes the width after it is put back.
	[HarmonyPriority(Priority.First)]
	private static void Postfix(InspectTabBase __instance, ref Vector2 ___size)
	{
		if (__instance is ITab_Pawn_Health)
		{
			HealthTabCompat.AfterUpdateSize(__instance, ref ___size);
		}
	}
}
