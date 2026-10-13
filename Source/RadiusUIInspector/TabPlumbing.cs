using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RadiusUIInspector;

public static class TabPlumbing
{
	private static readonly MethodInfo? InterfaceToggleTab = AccessTools.Method(typeof(InspectPaneUtility), "InterfaceToggleTab");

	private static readonly object[] toggleArgs = new object[2];

	private static Type? shownType;

	private static bool blankLogged;

	private static int blankFrames;

	private const int MaxBlankFrames = 20;

	private static readonly AccessTools.FieldRef<WindowStack, List<Window>> WindowsOf = AccessTools.FieldRefAccess<WindowStack, List<Window>>("windows");

	private static readonly AccessTools.FieldRef<WindowStack, bool> OrderDirtyOf = AccessTools.FieldRefAccess<WindowStack, bool>("updateInternalWindowsOrderLater");

	private const int VanillaTabWindowId = -235086;

	// Bound once: the tab size is read on every GUI event while a tab is hosted, so no per-call reflection.
	private static readonly Action<InspectTabBase>? UpdateSizeOf = Bind(() => AccessTools.MethodDelegate<Action<InspectTabBase>>(AccessTools.Method(typeof(InspectTabBase), "UpdateSize")));

	private static readonly AccessTools.FieldRef<InspectTabBase, Vector2>? SizeOf = Bind(() => AccessTools.FieldRefAccess<InspectTabBase, Vector2>("size"));

	internal static bool HostedTabPainted { get; private set; }

	public static void DrawOpenTab(IInspectPane pane)
	{
		InspectTabBase inspectTabBase = FindOpenTab(pane);
		if (inspectTabBase == null)
		{
			shownType = null;
			blankLogged = false;
			blankFrames = 0;
			HostedTabPainted = false;
		}
		else if (InspectorCore.NativeTabOpen)
		{
			HostedTabPainted = false;
		}
		else if (InspectorCore.HostingTab)
		{
			pane.RecentHeight = InspectorCore.CurrentPaneHeight;
			bool flag = shownType == inspectTabBase.GetType();
			bool flag2 = blankFrames > 20;
			if (InspectorCore.AnimSettled || flag || flag2)
			{
				if (flag2 && !blankLogged && Prefs.DevMode)
				{
					blankLogged = true;
					Log.Warning("[Radius UI - Inspector] hosted tab never settled, painting anyway after " + blankFrames + " passes. open=" + inspectTabBase.GetType().Name + " paneH=" + InspectorCore.CurrentPaneHeight.ToString("F0") + " target=" + InspectorCore.AnimTargetH.ToString("F0") + " pillH=" + InspectorCore.PillBlockH.ToString("F0") + " pinned=" + InspectorCore.Pinned);
				}
				Vector2 tabSize = GetTabSize(inspectTabBase);
				if (tabSize.x > 0f && tabSize.y > 0f && (Mathf.Abs(tabSize.x - InspectorCore.HostedTabSize.x) > 0.5f || Mathf.Abs(tabSize.y - InspectorCore.HostedTabSize.y) > 0.5f))
				{
					InspectorCore.SetHosting(hosting: true, tabSize);
				}
				inspectTabBase.DoTabGUI();
				shownType = inspectTabBase.GetType();
				blankFrames = 0;
				HostedTabPainted = true;
				EnforceTabPairing();
			}
			else
			{
				blankFrames++;
				HostedTabPainted = false;
			}
		}
		else
		{
			HostedTabPainted = false;
			inspectTabBase.DoTabGUI();
			pane.RecentHeight = 700f;
		}
	}

	private static void EnforceTabPairing()
	{
		WindowStack windowStack = Find.WindowStack;
		List<Window> list = WindowsOf(windowStack);
		int num = -1;
		int num2 = -1;
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] is MainTabWindow_Inspect)
			{
				num = i;
			}
			else if (list[i].ID == -235086)
			{
				num2 = i;
			}
		}
		if (num >= 0 && num2 >= 0 && num2 != num + 1)
		{
			if (num2 < num)
			{
				Window item = list[num2];
				list.RemoveAt(num2);
				list.Insert(num, item);
			}
			else
			{
				Window item2 = list[num];
				list.RemoveAt(num);
				list.Insert(num2 - 1, item2);
			}
			OrderDirtyOf(windowStack) = true;
		}
	}

	public static InspectTabBase? FindOpenTab(IInspectPane pane)
	{
		IEnumerable<InspectTabBase> curTabs = pane.CurTabs;
		if (curTabs == null || pane.OpenTabType == null)
		{
			return null;
		}
		if (curTabs is IList list)
		{
			for (int i = 0; i < list.Count; i++)
			{
				InspectTabBase inspectTabBase = (InspectTabBase)list[i];
				if (inspectTabBase != null && inspectTabBase.IsVisible && inspectTabBase.GetType() == pane.OpenTabType)
				{
					return inspectTabBase;
				}
			}
			return null;
		}
		foreach (InspectTabBase item in curTabs)
		{
			if (item != null && item.IsVisible && item.GetType() == pane.OpenTabType)
			{
				return item;
			}
		}
		return null;
	}

	public static Vector2 GetTabSize(InspectTabBase tab)
	{
		if (SizeOf == null)
		{
			return Vector2.zero;
		}
		try
		{
			UpdateSizeOf?.Invoke(tab);
			return SizeOf(tab);
		}
		catch (Exception ex2)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Tab size read failed: " + ex2.Message, 5351397);
			return Vector2.zero;
		}
	}

	private static T? Bind<T>(Func<T> bind) where T : class
	{
		try
		{
			return bind();
		}
		catch (Exception ex)
		{
			Log.Warning("[Radius UI - Inspector] Tab size accessor unavailable: " + ex.Message);
			return null;
		}
	}

	public static void Toggle(InspectTabBase tab, IInspectPane pane)
	{
		try
		{
			if (InterfaceToggleTab != null)
			{
				toggleArgs[0] = tab;
				toggleArgs[1] = pane;
				InterfaceToggleTab.Invoke(null, toggleArgs);
				return;
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] InterfaceToggleTab failed: " + ex.Message, 5351398);
		}
		if (tab.GetType() == pane.OpenTabType)
		{
			pane.CloseOpenTab();
			SoundDefOf.TabClose.PlayOneShotOnCamera();
		}
		else
		{
			pane.OpenTabType = tab.GetType();
			tab.OnOpen();
			SoundDefOf.TabOpen.PlayOneShotOnCamera();
		}
	}
}
