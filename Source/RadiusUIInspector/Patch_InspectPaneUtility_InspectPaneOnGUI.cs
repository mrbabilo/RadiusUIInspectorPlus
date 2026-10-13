using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

[HarmonyPatch(typeof(InspectPaneUtility), "InspectPaneOnGUI")]
public static class Patch_InspectPaneUtility_InspectPaneOnGUI
{
	public static bool Prefix(Rect inRect, IInspectPane pane)
	{
		try
		{
			if (!(pane is MainTabWindow_Inspect) || !InspectorCore.Active)
			{
				return true;
			}
			pane.RecentHeight = (InspectorCore.HostingTab ? InspectorCore.CurrentPaneHeight : InspectorCore.PaneHeight);
			Thing thing = Find.Selector.SingleSelectedThing;
			if (thing == null)
			{
				List<object> selectedObjects = Find.Selector.SelectedObjects;
				if (selectedObjects != null && selectedObjects.Count > 0)
				{
					thing = selectedObjects[0] as Thing;
				}
			}
			switch (InspectorCore.Mode)
			{
			case PaneMode.SinglePawn:
				if (!(thing is Pawn pawn))
				{
					return true;
				}
				InspectorPanel.DrawPane(inRect, pawn, pane);
				return false;
			case PaneMode.SingleThing:
				if (thing == null || thing is Pawn)
				{
					return true;
				}
				ThingPanel.DrawPane(inRect, thing, pane);
				return false;
			case PaneMode.SingleZone:
			{
				Zone selectedZone = Find.Selector.SelectedZone;
				if (selectedZone == null)
				{
					return true;
				}
				ZonePanel.DrawPane(inRect, selectedZone, pane);
				return false;
			}
			case PaneMode.Multi:
				InspectorPanel.DrawPane(inRect, null, pane);
				return false;
			default:
				return true;
			}
		}
		catch (Exception e)
		{
			InspectorCore.Trip(e);
			return true;
		}
	}
}
