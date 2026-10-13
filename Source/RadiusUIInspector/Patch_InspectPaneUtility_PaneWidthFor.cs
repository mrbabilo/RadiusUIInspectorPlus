using HarmonyLib;
using RimWorld;

namespace RadiusUIInspector;

[HarmonyPatch(typeof(InspectPaneUtility), "PaneWidthFor")]
public static class Patch_InspectPaneUtility_PaneWidthFor
{
	[HarmonyAfter(new string[] { "assssssqwww.MyModTemplate" })]
	public static void Postfix(IInspectPane pane, ref float __result)
	{
		if (pane is MainTabWindow_Inspect && InspectorCore.Active)
		{
			__result = InspectorCore.CurrentPaneWidth;
		}
	}
}
