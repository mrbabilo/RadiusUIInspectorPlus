using System;
using System.Reflection;
using HarmonyLib;
using RadiusUI.Framework;
using RimWorld;
using Verse;

namespace RadiusUIInspector;

internal static class BIPCompat
{
	internal const string HarmonyId = "assssssqwww.MyModTemplate";

	internal static bool Active { get; private set; }

	internal static void TryHook(Harmony harmony)
	{
		if (!ModCompat.IsActive("assssssqwww.BetterInspectPane"))
		{
			return;
		}
		try
		{
			Type typeInAnyAssembly = GenTypes.GetTypeInAnyAssembly("ASQBetterInspectPane.InspectPaneOnGUIPatch");
			MethodInfo methodInfo = ((typeInAnyAssembly == null) ? null : AccessTools.Method(typeInAnyAssembly, "Prefix"));
			if (methodInfo == null)
			{
				Log.Warning("[Radius UI - Inspector] Better Inspect Pane is active but InspectPaneOnGUIPatch.Prefix did not resolve (version drift?) - coexistence hook skipped. If its 'override inspect pane' setting is enabled the pane may double-draw.");
				return;
			}
			harmony.Patch(methodInfo, new HarmonyMethod(typeof(BIPCompat), "SkipTheirTakeoverWhileHosting"));
			Active = true;
			Log.Message("[Radius UI - Inspector] Better Inspect Pane detected - Radius draws the selections it hosts; BIP keeps the world map, mixed selections, and its bars wherever vanilla draws.");
		}
		catch (Exception ex)
		{
			Log.Warning("[Radius UI - Inspector] Better Inspect Pane coexistence hook failed: " + ex.Message);
		}
	}

	private static bool SkipTheirTakeoverWhileHosting(IInspectPane pane, ref bool __result)
	{
		try
		{
			if (pane is MainTabWindow_Inspect && InspectorCore.Active)
			{
				__result = true;
				return false;
			}
		}
		catch (Exception)
		{
		}
		return true;
	}
}
