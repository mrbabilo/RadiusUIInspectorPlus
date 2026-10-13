using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public static class ContentsPanel
{
	private const float RowH = 26f;

	private const float HeaderH = 22f;

	private static readonly MethodInfo? OnDropThingMI = AccessTools.Method(typeof(ITab_ContentsBase), "OnDropThing", new Type[2]
	{
		typeof(Thing),
		typeof(int)
	});

	private static readonly object[] dropArgs = new object[2];

	private static Vector2 scroll;

	private static string query = "";

	private static int sortKey;

	private static bool sortDesc = true;

	private static readonly HashSet<string> collapsed = new HashSet<string>();

	private static readonly List<Thing> buffer = new List<Thing>();

	private static readonly List<string> catOrder = new List<string>();

	private static readonly Dictionary<string, List<Thing>> byCat = new Dictionary<string, List<Thing>>();

	private static float lastContentH = 10f;

	private static float lastViewH = 10f;

	public static float PreferredHeight => Mathf.Min(430f, (float)UI.screenHeight * 0.55f);

	public static bool Owns(IInspectPane pane)
	{
		if (pane == null || InspectorMod.Settings == null)
		{
			return false;
		}
		if (!InspectorMod.Settings.nativeContentsTab)
		{
			return false;
		}
		Type openTabType = pane.OpenTabType;
		if (openTabType != null)
		{
			return typeof(ITab_ContentsBase).IsAssignableFrom(openTabType);
		}
		return false;
	}

	public static void Notify_SelectionChanged()
	{
		scroll = Vector2.zero;
		query = "";
		collapsed.Clear();
	}

	public static void Draw(Rect area, IInspectPane pane)
	{
		if (!(TabPlumbing.FindOpenTab(pane) is ITab_ContentsBase tab_ContentsBase))
		{
			return;
		}
		IList<Thing> container;
		bool canRemoveThings;
		try
		{
			container = tab_ContentsBase.container;
			canRemoveThings = tab_ContentsBase.canRemoveThings;
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] contents read failed: " + ex.Message, 5351403);
			return;
		}
		if (container == null)
		{
			return;
		}
		Rebuild(container);
		float y = area.y;
		float width = area.width;
		float num = 0f;
		float num2 = 0f;
		int num3 = 0;
		for (int i = 0; i < buffer.Count; i++)
		{
			Thing thing = buffer[i];
			num3 += thing.stackCount;
			num += thing.GetStatValue(StatDefOf.Mass) * (float)thing.stackCount;
			num2 += thing.MarketValue * (float)thing.stackCount;
		}
		RadiusFont.Label(new Rect(area.x, y, width, 22f), "RUI.Inspector.ContentsSummary".Translate(num3, num.ToStringMass(), num2.ToStringMoney()), GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		y += 24f;
		Rect rect = new Rect(area.x, y, width - 92f, 24f);
		CardChrome.Rounded(rect, Palette.Surface2, 8f);
		string text = Widgets.TextField(rect.ContractedBy(4f), query);
		if (text != query)
		{
			query = text;
		}
		if (SmallButton(new Rect(area.x + width - 88f, y, 64f, 24f), SortLabel()))
		{
			sortKey = (sortKey + 1) % 4;
			InspectorCore.Poke();
		}
		if (SmallButton(new Rect(area.x + width - 22f, y, 22f, 24f), sortDesc ? "▼" : "▲"))
		{
			sortDesc = !sortDesc;
			InspectorCore.Poke();
		}
		y += 28f;
		Rect outRect = new Rect(area.x, y, width, area.yMax - y - 2f);
		if (outRect.height < 30f)
		{
			return;
		}
		lastViewH = outRect.height;
		float num4 = Mathf.Floor(outRect.width - 20f);
		Rect viewRect = new Rect(0f, 0f, num4, Mathf.Max(lastContentH, outRect.height));
		int depth = FlatScroll.Depth;
		FlatScroll.Begin(outRect, ref scroll, viewRect);
		try
		{
			float cy = 0f;
			if (buffer.Count == 0)
			{
				RadiusFont.Label(new Rect(0f, cy + 12f, num4, 22f), query.NullOrEmpty() ? "RUI.Inspector.ContentsEmpty".Translate() : "RUI.Inspector.ContentsNoMatch".Translate(), GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleCenter, wrap: false);
				cy += 40f;
			}
			else
			{
				DrawGroups(num4, ref cy, tab_ContentsBase, canRemoveThings);
			}
			lastContentH = Mathf.Max(cy, 10f);
		}
		finally
		{
			FlatScroll.EndOrUnwind(depth);
		}
	}

	private static void DrawGroups(float vw, ref float cy, ITab_ContentsBase tab, bool canRemove)
	{
		for (int i = 0; i < catOrder.Count; i++)
		{
			string text = catOrder[i];
			List<Thing> list = byCat[text];
			bool flag = collapsed.Contains(text);
			Rect rect = new Rect(0f, cy, vw, 22f);
			if (ViewCull.Visible(cy, 22f, scroll.y, lastViewH))
			{
				if (Mouse.IsOver(rect))
				{
					CardChrome.Hover(rect, 8f);
				}
				IconSet.Get(flag ? "Action/Next" : "Action/ChevronDown").Draw(new Rect(2f, rect.y + 5f, 12f, 12f), Palette.TextDim);
				RadiusFont.Label(new Rect(18f, rect.y, vw - 70f, 22f), text, GameFont.Tiny, heading: true, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
				RadiusFont.Label(new Rect(vw - 50f, rect.y, 48f, 22f), list.Count.ToString(), GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleRight, wrap: false);
			}
			if (Widgets.ButtonInvisible(rect))
			{
				if (!collapsed.Remove(text))
				{
					collapsed.Add(text);
				}
				InspectorCore.Poke();
			}
			cy += 22f;
			if (!flag)
			{
				for (int j = 0; j < list.Count; j++)
				{
					DrawRow(list[j], vw, ref cy, tab, canRemove);
				}
			}
		}
	}

	private static void DrawRow(Thing t, float vw, ref float cy, ITab_ContentsBase tab, bool canRemove)
	{
		Rect rect = new Rect(0f, cy, vw, 26f);
		if (!ViewCull.Visible(cy, 26f, scroll.y, lastViewH))
		{
			cy += 26f;
			return;
		}
		bool flag = Mouse.IsOver(rect);
		if (flag)
		{
			CardChrome.Hover(rect, 8f);
		}
		Rect rect2 = new Rect(2f, rect.y + 1f, 24f, 24f);
		try
		{
			Widgets.ThingIcon(rect2, t);
		}
		catch (Exception)
		{
		}
		float num = (canRemove ? 26f : 0f);
		RadiusFont.Label(new Rect(30f, rect.y, vw - 126f - num, 26f), t.LabelCap, GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		RadiusFont.Label(new Rect(vw - 96f - num, rect.y, 34f, 26f), t.stackCount.ToString(), GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleRight, wrap: false);
		RadiusFont.Label(new Rect(vw - 60f - num, rect.y, 56f, 26f), (t.GetStatValue(StatDefOf.Mass) * (float)t.stackCount).ToStringMass(), GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleRight, wrap: false);
		if (canRemove)
		{
			Rect rect3 = new Rect(vw - 24f, rect.y + 2f, 22f, 22f);
			if (flag)
			{
				CardChrome.Hover(rect3, 8f);
			}
			IconSet.Get("Action/CursorDrop").Draw(rect3.ContractedBy(4f), Mouse.IsOver(rect3) ? Palette.Bad : Palette.TextDim);
			TooltipHandler.TipRegion(rect3, "RUI.Inspector.ContentsDrop".Translate());
			if (Widgets.ButtonInvisible(rect3))
			{
				Drop(tab, t, t.stackCount);
				InspectorCore.Poke();
				cy += 26f;
				return;
			}
		}
		if (Widgets.ButtonInvisible(new Rect(0f, rect.y, vw - num, 26f)))
		{
			CameraJumper.TryJumpAndSelect(t);
			InspectorCore.Poke();
		}
		if (flag)
		{
			TargetHighlighter.Highlight(t);
		}
		cy += 26f;
	}

	private static void Drop(ITab_ContentsBase tab, Thing t, int count)
	{
		try
		{
			if (!(OnDropThingMI == null))
			{
				dropArgs[0] = t;
				dropArgs[1] = count;
				OnDropThingMI.Invoke(tab, dropArgs);
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] contents drop failed: " + ex.Message, 5351404);
		}
	}

	private static void Rebuild(IList<Thing> items)
	{
		buffer.Clear();
		catOrder.Clear();
		byCat.Clear();
		string text = query.Trim();
		for (int i = 0; i < items.Count; i++)
		{
			Thing thing = items[i];
			if (thing != null)
			{
				Thing thing2 = thing.GetInnerIfMinified() ?? thing;
				if (text.Length <= 0 || thing2.LabelCap.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					buffer.Add(thing);
				}
			}
		}
		buffer.Sort(Compare);
		for (int j = 0; j < buffer.Count; j++)
		{
			string text2 = CategoryOf(buffer[j]);
			if (!byCat.TryGetValue(text2, out List<Thing> value))
			{
				value = new List<Thing>();
				byCat[text2] = value;
				catOrder.Add(text2);
			}
			value.Add(buffer[j]);
		}
	}

	private static string CategoryOf(Thing t)
	{
		List<ThingCategoryDef> thingCategories = (t.GetInnerIfMinified() ?? t).def.thingCategories;
		if (thingCategories != null && thingCategories.Count > 0)
		{
			return thingCategories[0].LabelCap;
		}
		return "RUI.Inspector.ContentsOther".Translate();
	}

	private static int Compare(Thing a, Thing b)
	{
		int num = sortKey switch
		{
			1 => a.stackCount.CompareTo(b.stackCount), 
			2 => (a.GetStatValue(StatDefOf.Mass) * (float)a.stackCount).CompareTo(b.GetStatValue(StatDefOf.Mass) * (float)b.stackCount), 
			3 => (a.MarketValue * (float)a.stackCount).CompareTo(b.MarketValue * (float)b.stackCount), 
			_ => string.Compare(a.LabelCap, b.LabelCap, StringComparison.OrdinalIgnoreCase), 
		};
		if (!sortDesc || sortKey == 0)
		{
			return num;
		}
		return -num;
	}

	private static string SortLabel()
	{
		return sortKey switch
		{
			1 => "RUI.Inspector.SortCount".Translate(), 
			2 => "RUI.Inspector.SortMass".Translate(), 
			3 => "RUI.Inspector.SortValue".Translate(), 
			_ => "RUI.Inspector.SortName".Translate(), 
		};
	}

	private static bool SmallButton(Rect r, string label)
	{
		bool flag = Mouse.IsOver(r);
		CardChrome.Rounded(r, flag ? Palette.Surface3 : Palette.Surface2, 8f);
		RadiusFont.Label(r, label, GameFont.Tiny, heading: false, flag ? Palette.Ink : Palette.TextDim, TextAnchor.MiddleCenter, wrap: false);
		return Widgets.ButtonInvisible(r);
	}
}
