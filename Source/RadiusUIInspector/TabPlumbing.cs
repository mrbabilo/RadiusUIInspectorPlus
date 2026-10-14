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

	// A hosted tab is stretched to the pane, since its size drives FillTab's layout. The size the tab asks for is
	// kept here so the pane is still sized from it, and given back once the tab is no longer hosted.
	private static readonly Dictionary<InspectTabBase, Vector2> naturalSize = new Dictionary<InspectTabBase, Vector2>();

	private static readonly Dictionary<InspectTabBase, Vector2> stretchedTo = new Dictionary<InspectTabBase, Vector2>();

	private static readonly List<InspectTabBase> unstretchScratch = new List<InspectTabBase>();

	internal static bool HostedTabPainted { get; private set; }

	public static void DrawOpenTab(IInspectPane pane)
	{
		InspectTabBase inspectTabBase = FindOpenTab(pane);
		Unstretch(InspectorCore.HostingTab ? inspectTabBase : null);
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
			Restore(tab);
			UpdateSizeOf?.Invoke(tab);
			Vector2 result = SizeOf(tab);
			// FillTab runs later, when the window stack draws the tab: the tab must be left stretched.
			if (stretchedTo.TryGetValue(tab, out var value))
			{
				Stretch(tab, value);
			}
			return result;
		}
		catch (Exception ex2)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Tab size read failed: " + ex2.Message, 5351397);
			return Vector2.zero;
		}
	}

	// Puts the size the tab asked for back before its UpdateSize runs, so what UpdateSize leaves is the tab's own size:
	// most tabs set it once in the constructor, others recompute one axis (Nice Health Tab) or both (Modern Bio Tab).
	// Guessing it afterwards from the stretched value failed when the tab asked for exactly the pane width (Modern Bio
	// Tab at 1100): the old size came back, the pane shrank, and it grew again on the next frame.
	internal static void Restore(InspectTabBase tab)
	{
		if (SizeOf == null || tab == null || !stretchedTo.TryGetValue(tab, out var value))
		{
			return;
		}
		try
		{
			if (SizeOf(tab) == value && naturalSize.TryGetValue(tab, out var value2))
			{
				SizeOf(tab) = value2;
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Tab size restore failed: " + ex.Message, 5351402);
		}
	}

	// Called from the TabRect getter, after Restore and the tab's own UpdateSize, while the tab is hosted.
	internal static void Stretch(InspectTabBase tab, Vector2 to)
	{
		if (SizeOf == null || tab == null || to.x <= 0f || to.y <= 0f)
		{
			return;
		}
		try
		{
			naturalSize[tab] = SizeOf(tab);
			stretchedTo[tab] = to;
			SizeOf(tab) = to;
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Tab stretch failed: " + ex.Message, 5351399);
		}
	}

	// Gives every stretched tab but keep its own size back, so it draws as designed outside the pane.
	internal static void Unstretch(InspectTabBase? keep)
	{
		if (stretchedTo.Count == 0 || SizeOf == null)
		{
			return;
		}
		unstretchScratch.Clear();
		foreach (InspectTabBase key in stretchedTo.Keys)
		{
			if (key != keep)
			{
				unstretchScratch.Add(key);
			}
		}
		for (int i = 0; i < unstretchScratch.Count; i++)
		{
			InspectTabBase inspectTabBase = unstretchScratch[i];
			try
			{
				if (SizeOf(inspectTabBase) == stretchedTo[inspectTabBase] && naturalSize.TryGetValue(inspectTabBase, out var value))
				{
					SizeOf(inspectTabBase) = value;
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[Radius UI - Inspector] Tab unstretch failed: " + ex.Message, 5351400);
			}
			stretchedTo.Remove(inspectTabBase);
			naturalSize.Remove(inspectTabBase);
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
