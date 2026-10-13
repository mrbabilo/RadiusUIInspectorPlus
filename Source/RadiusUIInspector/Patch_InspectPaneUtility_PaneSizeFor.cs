using System;
using HarmonyLib;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

[HarmonyPatch(typeof(InspectPaneUtility), "PaneSizeFor")]
public static class Patch_InspectPaneUtility_PaneSizeFor
{
	private static int sizeFrame = -1;

	private static bool cachedHosting;

	private static NativeTabKind cachedNative;

	private static Vector2 cachedTabSize;

	private static Vector2 cachedTarget;

	private static Type? cachedOpenType;

	[HarmonyAfter(new string[] { "assssssqwww.MyModTemplate" })]
	public static void Postfix(IInspectPane pane, ref Vector2 __result)
	{
		if (!(pane is MainTabWindow_Inspect) || !InspectorCore.Active)
		{
			return;
		}
		if (!FrameGate.NewFrame(ref sizeFrame) && !(cachedOpenType != pane.OpenTabType))
		{
			InspectorCore.SetNative(cachedNative);
			InspectorCore.SetHosting(cachedHosting, cachedTabSize);
			__result = InspectorCore.AnimatePaneSize(cachedTarget);
			return;
		}
		float num = InspectorCore.PaneWidth;
		if (InspectorCore.Mode == PaneMode.SingleThing || InspectorCore.Mode == PaneMode.SingleZone)
		{
			InspectorCore.PillBlockH = InspectorPanel.MeasurePillBlockForPawn(pane, num - 24f);
		}
		float y = InspectorCore.PaneHeight;
		NativeTabKind nativeTabKind = NativeTabKind.None;
		if (!InspectorMod.Settings.showVanillaTabs && !InspectorCore.Minimized)
		{
			if (StoragePanel.Owns(pane) && StoragePanel.Resolve() != null)
			{
				nativeTabKind = NativeTabKind.Storage;
				y = Mathf.Min(InspectorCore.HostedContentTop + StoragePanel.PreferredHeight, (float)UI.screenHeight - 45f);
			}
			else if (ContentsPanel.Owns(pane))
			{
				nativeTabKind = NativeTabKind.Contents;
				y = Mathf.Min(InspectorCore.HostedContentTop + ContentsPanel.PreferredHeight, (float)UI.screenHeight - 45f);
			}
		}
		bool hosting = false;
		Vector2 tabSize = Vector2.zero;
		if (nativeTabKind == NativeTabKind.None && !InspectorMod.Settings.showVanillaTabs && (InspectorCore.Mode == PaneMode.SinglePawn || InspectorCore.Mode == PaneMode.SingleThing || InspectorCore.Mode == PaneMode.SingleZone) && !InspectorCore.Minimized && pane.OpenTabType != null)
		{
			InspectTabBase inspectTabBase = TabPlumbing.FindOpenTab(pane);
			if (inspectTabBase != null)
			{
				InspectorCore.SetHosting(hosting: true, InspectorCore.HostedTabSize);
				tabSize = TabPlumbing.GetTabSize(inspectTabBase);
				if (tabSize.x > 0f && tabSize.y > 0f)
				{
					hosting = true;
					float num2 = 12f;
					num = Mathf.Max(num, Mathf.Min(tabSize.x + num2 * 2f, (float)UI.screenWidth - 16f));
					y = Mathf.Min(InspectorCore.HostedContentTop + tabSize.y + num2, (float)UI.screenHeight - 45f);
				}
			}
		}
		cachedHosting = hosting;
		cachedNative = nativeTabKind;
		cachedTabSize = tabSize;
		cachedTarget = new Vector2(num, y);
		cachedOpenType = pane.OpenTabType;
		InspectorCore.SetNative(nativeTabKind);
		InspectorCore.SetHosting(hosting, tabSize);
		__result = InspectorCore.AnimatePaneSize(cachedTarget);
	}
}
