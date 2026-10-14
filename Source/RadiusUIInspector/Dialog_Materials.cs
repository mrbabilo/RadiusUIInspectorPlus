using System;
using System.Collections.Generic;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RadiusUIInspector;

// The material filters of one ThingFilter, for the screens that draw the vanilla filter tree: bills, apparel
// policies, the storage tab when the panel does not own it. Opened from the bar MaterialFilters adds above
// that tree. Rows paint like the tree's: press on one and drag over the others.
public class Dialog_Materials : Window
{
	private const float RowH = 26f;

	private const string SearchControl = "RUI_MaterialsSearch";

	private readonly ThingFilter filter;

	private readonly List<SpecialThingFilterDef> materials;

	private readonly List<SpecialThingFilterDef> shown = new List<SpecialThingFilterDef>();

	private string query = "";

	private string? shownQuery;

	private Vector2 scroll;

	private bool painting;

	private bool paintAllow;

	public override Vector2 InitialSize => new Vector2(340f, 520f);

	public Dialog_Materials(ThingFilter filter, List<SpecialThingFilterDef> materials)
		: base(new RadiusWindowDrawing())
	{
		this.filter = filter;
		this.materials = materials;
		doCloseX = true;
		closeOnClickedOutside = true;
		absorbInputAroundWindow = false;
		draggable = true;
		forcePause = false;
		layer = WindowLayer.Dialog;
	}

	public override void DoWindowContents(Rect inRect)
	{
		if (shownQuery != query)
		{
			shownQuery = query;
			shown.Clear();
			for (int i = 0; i < materials.Count; i++)
			{
				if (query.Length == 0 || materials[i].label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					shown.Add(materials[i]);
				}
			}
		}
		int allowed = 0;
		for (int i = 0; i < shown.Count; i++)
		{
			if (filter.Allows(shown[i]))
			{
				allowed++;
			}
		}
		RadiusFont.Label(new Rect(0f, 0f, inRect.width - 70f, 24f), "RUI.Inspector.StorageMaterials".Translate(), GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		RadiusFont.Label(new Rect(inRect.width - 100f, 0f, 70f, 24f), allowed + "/" + shown.Count, GameFont.Tiny, heading: false, (allowed < shown.Count) ? Palette.Warn : Palette.TextDim, TextAnchor.MiddleRight, wrap: false);
		query = FlatField.Draw(new Rect(0f, 30f, inRect.width, 28f), query, SearchControl, "RUI.Inspector.StorageSearchHint".Translate());
		// Allow or refuse what the search shows: every material while it is empty.
		float half = (inRect.width - 4f) * 0.5f;
		if (StoragePanel.SmallButton(new Rect(0f, 64f, half, 24f), "RUI.Inspector.AllowAll".Translate()))
		{
			SetShown(allow: true);
		}
		if (StoragePanel.SmallButton(new Rect(half + 4f, 64f, half, 24f), "RUI.Inspector.ClearAll".Translate()))
		{
			SetShown(allow: false);
		}
		if (painting && !Input.GetMouseButton(0))
		{
			painting = false;
		}
		Rect outRect = new Rect(0f, 94f, inRect.width, inRect.height - 94f);
		Rect viewRect = new Rect(0f, 0f, outRect.width - 12f, Mathf.Max(shown.Count * RowH, outRect.height));
		int depth = FlatScroll.Depth;
		FlatScroll.Begin(outRect, ref scroll, viewRect);
		try
		{
			for (int j = 0; j < shown.Count; j++)
			{
				float y = j * RowH;
				if (y + RowH < scroll.y || y > scroll.y + outRect.height)
				{
					continue;
				}
				SpecialThingFilterDef sf = shown[j];
				bool on = filter.Allows(sf);
				Rect row = new Rect(0f, y, viewRect.width, RowH - 2f);
				bool hover = Mouse.IsOver(row);
				if (hover)
				{
					CardChrome.Hover(row, 8f);
					TooltipHandler.TipRegion(row, sf.description);
				}
				StoragePanel.DrawTriState(new Rect(6f, y + 5f, 14f, 14f), on ? 1 : 0, 1);
				float labelX = 28f;
				ThingDef? stuff = MaterialFilters.StuffOf(sf);
				if (stuff != null)
				{
					StoragePanel.ItemIcon(new Rect(labelX, y + 1f, 22f, 22f), stuff, on);
					labelX += 28f;
				}
				RadiusFont.Label(new Rect(labelX, y, row.xMax - labelX - 4f, RowH - 2f), sf.LabelCap, GameFont.Small, heading: false, on ? Palette.Ink : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
				if (!hover)
				{
					continue;
				}
				Event current = Event.current;
				if (current.type == EventType.MouseDown && current.button == 0)
				{
					painting = true;
					paintAllow = !on;
					current.Use();
					Set(sf, paintAllow);
				}
				else if (painting && on != paintAllow && current.type != EventType.Layout)
				{
					Set(sf, paintAllow);
				}
			}
		}
		finally
		{
			FlatScroll.EndOrUnwind(depth);
		}
		Text.Font = GameFont.Small;
		Text.Anchor = TextAnchor.UpperLeft;
		GUI.color = Color.white;
	}

	private void Set(SpecialThingFilterDef sf, bool allow)
	{
		filter.SetAllow(sf, allow);
		(allow ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
	}

	private void SetShown(bool allow)
	{
		for (int i = 0; i < shown.Count; i++)
		{
			filter.SetAllow(shown[i], allow);
		}
	}
}
