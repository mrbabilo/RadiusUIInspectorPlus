using System;
using HarmonyLib;
using RimWorld;

namespace RadiusUIInspector;

[HarmonyPatch(typeof(InspectPaneUtility), "ExtraOnGUI")]
public static class Patch_InspectPaneUtility_ExtraOnGUI
{
	public static bool Prefix(IInspectPane pane)
	{
		try
		{
			if (!(pane is MainTabWindow_Inspect) || !InspectorCore.Active)
			{
				return true;
			}
			if (InspectorMod.Settings.showVanillaTabs)
			{
				return true;
			}
			if (pane.AnythingSelected)
			{
				if (KeyBindingDefOf.SelectNextInCell.KeyDownEvent)
				{
					pane.SelectNextInCell();
				}
				TabPlumbing.DrawOpenTab(pane);
			}
			return false;
		}
		catch (Exception e)
		{
			InspectorCore.Trip(e);
			return true;
		}
	}
}
