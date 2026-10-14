using HarmonyLib;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

[HarmonyPatch(typeof(InspectTabBase), "TabRect", MethodType.Getter)]
public static class Patch_InspectTabBase_TabRect
{
	// The getter calls UpdateSize first: the tab must see its own size there, not the stretched one.
	public static void Prefix(InspectTabBase __instance)
	{
		TabPlumbing.Restore(__instance);
	}

	public static void Postfix(InspectTabBase __instance, ref Rect __result)
	{
		if (InspectorCore.Active && !InspectorMod.Settings.showVanillaTabs)
		{
			if (InspectorCore.HostingTab)
			{
				Rect rect = InspectorCore.HostedTabLocalRect();
				float num = (float)UI.screenHeight - 35f - InspectorCore.CurrentPaneHeight;
				__result.x = rect.x;
				__result.y = num + rect.y;
				__result.width = rect.width;
				__result.height = rect.height;
				TabPlumbing.Stretch(__instance, rect.size);
			}
			else
			{
				TabPlumbing.Unstretch(null);
				__result.y += 30f;
			}
		}
	}
}
