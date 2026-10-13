using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

[HarmonyPatch(typeof(MainTabWindow_Inspect), "PaneTopY", MethodType.Getter)]
public static class Patch_MainTabWindow_Inspect_PaneTopY
{
	public static void Postfix(ref float __result)
	{
		if (InspectorCore.Active)
		{
			__result = (float)UI.screenHeight - Mathf.Max(InspectorCore.PaneHeight, InspectorCore.MultiMaxHeight) - 35f;
		}
	}
}
