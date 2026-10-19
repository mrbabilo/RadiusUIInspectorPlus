using System;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public static class ZonePanel
{
	private const float Margin = 6f;

	private static readonly CountedLabel CellsLabel = new CountedLabel("RUI.Inspector.ZoneCells");

	private const float Pad = 12f;

	private static Vector2 scroll;

	private static float lastContentH = 10f;

	private static float lastViewH = 10f;

	internal static float BodyContentH => lastContentH;

	public static void DrawPane(Rect inRect, Zone zone, IInspectPane pane)
	{
		if (InspectorCore.Minimized)
		{
			DrawCompact(inRect, zone);
			return;
		}
		DrawFull(inRect, zone, pane);
		InspectorCore.TickMinimize();
	}

	private static void DrawCompact(Rect inRect, Zone zone)
	{
		Rect rect = inRect.ContractedBy(6f);
		CardChrome.Card(rect);
		if (Mouse.IsOver(rect))
		{
			InspectorCore.Poke();
		}
		Rect rect2 = new Rect(rect.x + 8f, rect.y + 8f, 22f, 22f);
		CardChrome.Rounded(rect2, zone.color, 8f);
		RadiusFont.Label(new Rect(rect2.xMax + 8f, rect.y + 6f, rect.width - 60f, 23f), zone.RenamableLabel, GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		if (Widgets.ButtonInvisible(rect))
		{
			InspectorCore.Poke();
		}
	}

	private static void DrawFull(Rect inRect, Zone zone, IInspectPane pane)
	{
		Rect rect = inRect.ContractedBy(6f);
		if (InspectorCore.HostingTab && TabPlumbing.HostedTabPainted)
		{
			InspectorPanel.CardAroundHostedTab(rect);
		}
		else
		{
			CardChrome.Card(rect);
		}
		if (Mouse.IsOver(rect))
		{
			InspectorCore.PokeSoft();
		}
		float y = rect.y + 6f;
		DrawHeader(zone, rect, pane, ref y);
		InspectorPanel.DrawTabPills(rect, pane, ref y, 0, "");
		if (pane.OpenTabType != null)
		{
			InspectorCore.PokeSoft();
		}
		if ((InspectorCore.NativeTabOpen && InspectorPanel.DrawNativeTab(new Rect(rect.x + 12f, y, rect.width - 24f, rect.yMax - y - 8f), pane)) || InspectorCore.HostingTab)
		{
			return;
		}
		Rect outRect = new Rect(rect.x + 12f, y, rect.width - 24f, rect.yMax - y - 8f);
		lastViewH = outRect.height;
		float num = Mathf.Floor(outRect.width - 20f);
		Rect viewRect = new Rect(0f, 0f, num, Mathf.Max(lastContentH, outRect.height));
		int depth = FlatScroll.Depth;
		FlatScroll.Begin(outRect, ref scroll, viewRect);
		try
		{
			float cy = 0f;
			DrawBody(zone, num, ref cy);
			lastContentH = Mathf.Max(cy, 10f);
		}
		finally
		{
			FlatScroll.EndOrUnwind(depth);
		}
	}

	private static void DrawHeader(Zone zone, Rect card, IInspectPane pane, ref float y)
	{
		float num = card.x + 12f;
		Rect rect = new Rect(num, y - 2f, 48f, 48f);
		CardChrome.Rounded(rect, Palette.Surface2, 12f);
		CardChrome.Rounded(rect.ContractedBy(12f), zone.color, 8f);
		float bx = VanillaChrome.Draw(pane, card.xMax - 12f, y, card.width - 24f) - 28f;
		if (InspectorPanel.HeaderButton(ref bx, y, "Action/Dock", "RUI.Inspector.MinimizeTip".Translate(), active: false))
		{
			if (pane.OpenTabType != null)
			{
				pane.CloseOpenTab();
			}
			InspectorCore.Minimized = true;
		}
		if (InspectorPanel.HeaderButton(ref bx, y, "Action/Pin", "RUI.Inspector.PinTip".Translate(), InspectorCore.Pinned))
		{
			InspectorCore.Pinned = !InspectorCore.Pinned;
		}
		float num2 = num + 56f;
		float width = bx + 24f - num2 - 4f;
		RadiusFont.Label(new Rect(num2, y, width, 23f), zone.RenamableLabel, GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		RadiusFont.Label(new Rect(num2, y + 22f, width, 23f), CellsLabel.For(zone.cells.Count), GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		y += 50f;
	}

	private static void DrawBody(Zone zone, float vw, ref float cy)
	{
		if (zone is Zone_Stockpile zone_Stockpile)
		{
			int num = 0;
			int count = zone_Stockpile.cells.Count;
			Map map = zone_Stockpile.Map;
			if (map != null)
			{
				for (int i = 0; i < zone_Stockpile.cells.Count; i++)
				{
					if (map.thingGrid.ThingAt<Thing>(zone_Stockpile.cells[i]) != null)
					{
						num++;
					}
				}
			}
			if (count > 0)
			{
				float num2 = (float)num / (float)count;
				Rect bar = new Rect(0f, cy, vw, 20f);
				InspectorPanel.SuiteBar(bar, num2, (num2 > 0.85f) ? Palette.Warn : Palette.FillNeutral, -1f, -1f);
				InspectorPanel.BarInnerLabels(bar, "RUI.Inspector.ZoneFill".Translate(), num + " / " + count, num2);
				cy += 24f;
			}
		}
		string text;
		try
		{
			text = zone.GetInspectString();
		}
		catch (Exception)
		{
			text = string.Empty;
		}
		if (text.NullOrEmpty())
		{
			return;
		}
		cy += 3f;
		CardChrome.Fill(new Rect(0f, cy, vw, 1f), Palette.Border);
		cy += 6f;
		string[] array = text.Split(new char[1] { '\n' });
		foreach (string text2 in array)
		{
			if (!text2.NullOrEmpty())
			{
				float num3 = Mathf.Max(20f, Mathf.Ceil(RadiusFont.Height(text2, vw - 2f)));
				if (ViewCull.Visible(cy, num3, scroll.y, lastViewH))
				{
					RadiusFont.Label(new Rect(2f, cy, vw - 2f, num3), text2, GameFont.Small, heading: false, Palette.TextDim);
				}
				cy += num3 + 1f;
			}
		}
	}
}
