using System.Collections.Generic;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public static class ThingPanel
{
	private static readonly ThingSnapshot Snap = new ThingSnapshot();

	private static Vector2 scroll;

	private static float lastContentH = 200f;

	private static float lastViewH = 160f;

	private static int lastThingId = -1;

	private const float Margin = 6f;

	private const float Pad = 12f;

	private const float BarH = 18f;

	private static readonly string HitPointsLabel = "RUI.Inspector.HitPoints".Translate();

	private static readonly string FuelLabel = "RUI.Inspector.Fuel".Translate();

	private static readonly string GrowthLabel = "RUI.Inspector.Growth".Translate();

	private static readonly string FreshnessLabel = "RUI.Inspector.Freshness".Translate();

	private static readonly string BatteryLabel = "RUI.Inspector.Battery".Translate();

	private static readonly string GridLabel = "RUI.Inspector.PowerGrid".Translate();

	private static readonly string PipeNetLabel = "RUI.Inspector.PipeNet".Translate();

	private static readonly string ForbiddenLabel = "RUI.Inspector.Forbidden".Translate();

	private static readonly string BrokenDownLabel = "RUI.Inspector.BrokenDown".Translate();

	private static readonly string InfoTip = "RUI.Inspector.InfoTip".Translate();

	private static readonly string PinTip = "RUI.Inspector.PinTip".Translate();

	private static readonly string UnpinTip = "RUI.Inspector.UnpinTip".Translate();

	private static readonly string MinimizeTip = "RUI.Inspector.MinimizeTip".Translate();

	private static readonly string ExpandTip = "RUI.Inspector.ExpandTip".Translate();

	private static readonly InspectorPanel.ParsedLayout Lane = new InspectorPanel.ParsedLayout();

	internal static float BodyContentH => lastContentH;

	public static void DrawPane(Rect inRect, Thing thing, IInspectPane pane)
	{
		try
		{
			Snap.Refresh(thing);
			if (thing.thingIDNumber != lastThingId)
			{
				lastThingId = thing.thingIDNumber;
				scroll = Vector2.zero;
			}
			InspectorCore.TickMinimize();
			if (InspectorCore.Minimized)
			{
				DrawCompact(inRect, thing);
			}
			else
			{
				DrawFull(inRect, thing, pane);
			}
		}
		finally
		{
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			Text.WordWrap = true;
			GUI.color = Color.white;
		}
	}

	private static void DrawCompact(Rect inRect, Thing thing)
	{
		Rect rect = inRect.ContractedBy(6f);
		bool flag = Mouse.IsOver(rect);
		CardChrome.Card(rect, flag);
		Rect rect2 = new Rect(rect.x + 5f, rect.y + (rect.height - 38f) * 0.5f - 1f, 38f, 38f);
		CardChrome.Rounded(rect2, Palette.Surface2, 8f);
		InspectorPanel.DrawThingIconOrInitial(rect2.ContractedBy(3f), thing, Snap.Initial);
		RadiusFont.Label(new Rect(rect2.xMax + 8f, rect.y, rect.width - 90f, rect.height), Snap.Label, GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		IconSet.Get("Action/Popout").Draw(new Rect(rect.xMax - 24f, rect.y + (rect.height - 14f) * 0.5f, 14f, 14f), flag ? RadiusTheme.Accent : Palette.TextDim);
		TooltipHandler.TipRegion(rect, ExpandTip);
		if (Widgets.ButtonInvisible(rect))
		{
			InspectorCore.Poke();
		}
	}

	private static void DrawFull(Rect inRect, Thing thing, IInspectPane pane)
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
		DrawHeader(thing, rect, pane, ref y);
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
			DrawBody(num, ref cy);
			lastContentH = Mathf.Max(cy, 10f);
		}
		finally
		{
			FlatScroll.EndOrUnwind(depth);
		}
	}

	private static void DrawHeader(Thing thing, Rect card, IInspectPane pane, ref float y)
	{
		float num = card.x + 12f;
		Rect rect = new Rect(num, y - 2f, 48f, 48f);
		CardChrome.Rounded(rect, Palette.Surface2, 12f);
		InspectorPanel.DrawThingIconOrInitial(rect.ContractedBy(4f), thing, Snap.Initial);
		float bx = VanillaChrome.Draw(pane, card.xMax - 12f, y, card.width - 24f) - 28f;
		if (InspectorPanel.HeaderButton(ref bx, y, "Action/Dock", MinimizeTip, active: false))
		{
			if (pane.OpenTabType != null)
			{
				pane.CloseOpenTab();
			}
			InspectorCore.Minimized = true;
		}
		if (InspectorPanel.HeaderButton(ref bx, y, "Action/Pin", InspectorCore.Pinned ? UnpinTip : PinTip, InspectorCore.Pinned))
		{
			InspectorCore.Pinned = !InspectorCore.Pinned;
		}
		float num2 = num + 56f;
		float width = bx + 24f - num2 - 4f;
		RadiusFont.Label(new Rect(num2, y, width, 23f), Snap.Label, GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		RadiusFont.Label(new Rect(num2, y + 22f, width, 23f), Snap.SubLine, GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		y += 50f;
	}

	private static void DrawBody(float vw, ref float cy)
	{
		InspectorPanel.ParsedLayout lane = Lane;
		InspectorPanel.EnsureParsed(lane, InspectorMod.Settings.EffectiveThingLayout());
		ComputeVisibility(lane);
		float num = cy;
		for (int i = 0; i < lane.Rows.Count; i++)
		{
			List<InspectorPanel.CellSpec> list = lane.Rows[i];
			int count = list.Count;
			int num2 = 0;
			for (int j = 0; j < count; j++)
			{
				if (lane.Vis[lane.RowStart[i] + j])
				{
					num2++;
				}
			}
			if (num2 == 0)
			{
				continue;
			}
			for (int k = 0; k < count; k++)
			{
				if (lane.Vis[lane.RowStart[i] + k] && list[k].Kind == InspectorPanel.CellKind.Header)
				{
					if (cy > num)
					{
						cy += 10f;
					}
					break;
				}
			}
			float num3 = ((num2 > 1) ? Mathf.Floor((vw - 10f * (float)(num2 - 1)) / (float)num2) : Mathf.Floor(vw));
			float num4 = 0f;
			float num5 = 0f;
			for (int l = 0; l < count; l++)
			{
				if (lane.Vis[lane.RowStart[i] + l])
				{
					float num6 = DrawThingCell(list[l], num5, num3, cy);
					if (num6 > num4)
					{
						num4 = num6;
					}
					num5 += num3 + 10f;
				}
			}
			if (num4 > 0f)
			{
				cy += Mathf.Ceil(num4) + 4f;
			}
		}
	}

	private static void ComputeVisibility(InspectorPanel.ParsedLayout pl)
	{
		InspectorPanel.PrepareVisibility(pl);
		for (int i = 0; i < pl.Rows.Count; i++)
		{
			List<InspectorPanel.CellSpec> list = pl.Rows[i];
			for (int j = 0; j < list.Count; j++)
			{
				InspectorPanel.CellSpec spec = list[j];
				pl.Vis[pl.RowStart[i] + j] = spec.Kind != InspectorPanel.CellKind.Header && spec.Kind != InspectorPanel.CellKind.Spacer && CellHasContent(in spec);
			}
		}
		InspectorPanel.HeaderSpacerPasses(pl);
	}

	private static bool CellHasContent(in InspectorPanel.CellSpec spec)
	{
		switch (spec.Kind)
		{
		case InspectorPanel.CellKind.HP:
			return Snap.HasHP;
		case InspectorPanel.CellKind.Fuel:
			return Snap.HasFuel;
		case InspectorPanel.CellKind.Growth:
			return Snap.HasGrowth;
		case InspectorPanel.CellKind.Freshness:
			return Snap.HasFresh;
		case InspectorPanel.CellKind.Battery:
			return Snap.HasBattery;
		case InspectorPanel.CellKind.PowerGrid:
			return Snap.HasGrid;
		case InspectorPanel.CellKind.PipeNet:
			return Snap.HasPipe;
		case InspectorPanel.CellKind.Chips:
			if (Snap.QualityStr.NullOrEmpty() && Snap.StackStr.NullOrEmpty() && Snap.ValueStr.NullOrEmpty() && !Snap.HasPower && !Snap.Forbidden)
			{
				return Snap.BrokenDown;
			}
			return true;
		case InspectorPanel.CellKind.Inspect:
			return Snap.InspectLines.Length != 0;
		default:
			return false;
		}
	}

	private static float DrawThingCell(in InspectorPanel.CellSpec spec, float x, float w, float y)
	{
		switch (spec.Kind)
		{
		case InspectorPanel.CellKind.Header:
			return InspectorPanel.DrawEyebrow(spec.Display, x, w, y);
		case InspectorPanel.CellKind.Spacer:
			return 8f;
		case InspectorPanel.CellKind.HP:
		{
			Color color = ((Snap.HPPct > 0.6f) ? Palette.Good : ((Snap.HPPct > 0.3f) ? Palette.Warn : Palette.Bad));
			return Bar(x, w, y, Snap.HPPct, color, HitPointsLabel, Snap.HPStr);
		}
		case InspectorPanel.CellKind.Fuel:
			return Bar(x, w, y, Snap.FuelPct, Palette.FillNeutral, FuelLabel, Snap.FuelStr);
		case InspectorPanel.CellKind.Growth:
			return Bar(x, w, y, Snap.GrowthPct, Palette.Good, GrowthLabel, Snap.GrowthStr);
		case InspectorPanel.CellKind.Freshness:
		{
			Color color2 = ((Snap.FreshPct > 0.5f) ? Palette.Good : ((Snap.FreshPct > 0.2f) ? Palette.Warn : Palette.Bad));
			return Bar(x, w, y, Snap.FreshPct, color2, FreshnessLabel, Snap.FreshStr);
		}
		case InspectorPanel.CellKind.Battery:
			return Bar(x, w, y, Snap.BatteryPct, Palette.FillNeutral, BatteryLabel, Snap.BatteryStr);
		case InspectorPanel.CellKind.PowerGrid:
			return Bar(x, w, y, Snap.GridPct, Palette.FillNeutral, GridLabel, Snap.GridStr);
		case InspectorPanel.CellKind.PipeNet:
			return Bar(x, w, y, Snap.PipePct, Snap.PipeColor, Snap.PipeLabel.NullOrEmpty() ? PipeNetLabel : Snap.PipeLabel, Snap.PipeStr);
		case InspectorPanel.CellKind.Chips:
			return DrawChipsCell(x, w, y);
		case InspectorPanel.CellKind.Inspect:
			return DrawInspectCell(x, w, y);
		default:
			return 0f;
		}
	}

	private static float Bar(float x, float w, float y, float pct, Color color, string label, string value)
	{
		Rect bar = new Rect(x, y, w, 18f);
		InspectorPanel.SuiteBar(bar, pct, color, -1f, -1f);
		InspectorPanel.BarInnerLabels(bar, label, value, pct);
		return 18f;
	}

	private static float DrawChipsCell(float x0, float w, float y)
	{
		float x1 = 0f;
		float cy = y;
		bool flag = false;
		flag |= Chip(x0, ref x1, ref cy, w, Snap.QualityStr, Palette.Surface2, Palette.Ink);
		flag |= Chip(x0, ref x1, ref cy, w, Snap.StackStr, Palette.Surface2, Palette.Ink);
		flag |= Chip(x0, ref x1, ref cy, w, Snap.ValueStr, Palette.Surface2, Palette.TextDim);
		if (Snap.HasPower)
		{
			flag |= Chip(x0, ref x1, ref cy, w, Snap.PowerStr, Snap.PowerOn ? Palette.Surface2 : Palette.Surface3, Snap.PowerOn ? Palette.Good : Palette.TextDim);
		}
		if (Snap.Forbidden)
		{
			flag |= Chip(x0, ref x1, ref cy, w, ForbiddenLabel, Palette.Warn, Palette.InkOnAccent);
		}
		if (Snap.BrokenDown)
		{
			flag |= Chip(x0, ref x1, ref cy, w, BrokenDownLabel, Palette.Bad, Palette.InkOnAccent);
		}
		if (!flag)
		{
			return 0f;
		}
		return cy - y + 20f;
	}

	private static bool Chip(float x0, ref float x, ref float cy, float vw, string text, Color bg, Color ink)
	{
		if (text.NullOrEmpty())
		{
			return false;
		}
		float num = Mathf.Min(RadiusFont.Size(text).x + 18f, vw);
		if (x > 0f && x + num > vw)
		{
			x = 0f;
			cy += 24f;
		}
		Rect rect = new Rect(x0 + x, cy, num, 20f);
		CardChrome.Pill(rect, bg);
		RadiusFont.Label(new Rect(rect.x + 9f, rect.y - 2f, rect.width - 18f, rect.height + 4f), text, GameFont.Small, heading: false, ink, TextAnchor.MiddleLeft, wrap: false);
		x += num + 6f;
		return true;
	}

	private static float DrawInspectCell(float x, float w, float y)
	{
		float num = y;
		for (int i = 0; i < Snap.InspectLines.Length; i++)
		{
			string text = Snap.InspectLines[i];
			float num2 = Mathf.Max(20f, Mathf.Ceil(RadiusFont.Height(text, w - 2f)));
			if (ViewCull.Visible(num, num2, scroll.y, lastViewH))
			{
				RadiusFont.Label(new Rect(x + 2f, num, w - 2f, num2), text, GameFont.Small, heading: false, Palette.TextDim);
			}
			num += num2 + 1f;
		}
		return num - y;
	}
}
