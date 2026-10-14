using System;
using System.Collections.Generic;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RadiusUIInspector;

public class Dialog_LayoutEditor : Window
{
	private struct SlotHit
	{
		public Rect rect;

		public int row;

		public int col;

		public bool filled;
	}

	private const float RowH = 44f;

	private const float RowGap = 5f;

	private const float GripW = 22f;

	private const float ToolsW = 74f;

	private const float SlotGap = 8f;

	private const float DragThreshold = 4f;

	private readonly List<List<string>> model = new List<List<string>>();

	private readonly List<SlotHit> slotHits = new List<SlotHit>();

	private readonly List<Rect> rowHits = new List<Rect>();

	private readonly List<Rect> gripHits = new List<Rect>();

	private bool rowArmed;

	private int rowArmIndex;

	private bool rowDragging;

	private int rowDragFrom;

	private int rowDropAt = -1;

	private Vector2 scroll;

	private Vector2 previewScroll;

	private Vector2 setScroll;

	private float setContentH = 520f;

	private int page;

	private int lane;

	private PawnProfileKind kind;

	private bool things;

	private int loadedStamp = -1;

	private static readonly string LaneBody = "RUI.Inspector.LaneBody".Translate();

	private static readonly string LaneRail = "RUI.Inspector.SectionRail".Translate();

	private static readonly string ResetLayoutLbl = "RUI.Inspector.ResetLayout".Translate();

	private static readonly string CopyFromLbl = "RUI.Inspector.CopyFrom".Translate();

	private static readonly string UserPresetsLbl = "RUI.Inspector.UserPresets".Translate();

	private static readonly string UserPresetsTip = "RUI.Inspector.UserPresetsTip".Translate();

	private static readonly string ResetProfileTip = "RUI.Inspector.ResetProfileTip".Translate();

	private static readonly string[] ProfileLabels = new string[4]
	{
		"RUI.Inspector.ProfileColonist".Translate(),
		"RUI.Inspector.ProfileAnimal".Translate(),
		"RUI.Inspector.ProfileMech".Translate(),
		"RUI.Inspector.ProfilePrisoner".Translate()
	};

	private static readonly string ThingsLabel = "RUI.Inspector.ProfileThings".Translate();

	private readonly struct ToolButton
	{
		public readonly string Label;

		public readonly string? Tip;

		public readonly bool Danger;

		public readonly Action OnClick;

		public ToolButton(string label, string? tip, bool danger, Action onClick)
		{
			Label = label;
			Tip = tip;
			Danger = danger;
			OnClick = onClick;
		}
	}

	private readonly List<ToolButton> toolButtons = new List<ToolButton>();

	private bool armed;

	private Vector2 armPos;

	private int armRow;

	private int armCol;

	private bool dragging;

	private string dragId = "";

	private int dragFromRow;

	private int dragFromCol;

	private int MaxCells
	{
		get
		{
			if (lane != 1)
			{
				return 3;
			}
			return 2;
		}
	}

	public override Vector2 InitialSize => new Vector2(660f, 600f);

	protected override float Margin => 14f;

	public Dialog_LayoutEditor()
		: base(new RadiusWindowDrawing())
	{
		forcePause = false;
		absorbInputAroundWindow = false;
		closeOnClickedOutside = false;
		closeOnAccept = false;
		preventCameraMotion = false;
		draggable = true;
		resizeable = true;
		doCloseX = true;
		doWindowBackground = true;
		drawShadow = false;
		onlyOneOfTypeAllowed = true;
		if (Current.ProgramState == ProgramState.Playing)
		{
			Thing thing = Find.Selector?.SingleSelectedThing;
			if (thing is Pawn pawn)
			{
				kind = InspectorCore.ProfileOf(pawn);
			}
			else if (thing != null)
			{
				things = true;
			}
		}
	}

	private List<string> TargetRows(InspectorSettings s)
	{
		if (things)
		{
			return s.EffectiveThingLayout();
		}
		if (lane != 1)
		{
			return s.EffectiveLayoutFor(kind);
		}
		return s.EffectiveRailFor(kind);
	}

	private void Load(InspectorSettings s)
	{
		model.Clear();
		List<string> list = TargetRows(s);
		for (int i = 0; i < list.Count; i++)
		{
			model.Add(new List<string>(list[i].Split(new char[1] { '|' })));
		}
		loadedStamp = s.LayoutStamp;
	}

	private void Save(InspectorSettings s)
	{
		List<string> list = TargetRows(s);
		list.Clear();
		for (int i = 0; i < model.Count; i++)
		{
			if (model[i].Count > 0)
			{
				list.Add(string.Join("|", model[i].ToArray()));
			}
		}
		s.LayoutStamp++;
		loadedStamp = s.LayoutStamp;
		InspectorCore.Poke();
	}

	private void Prune()
	{
		for (int num = model.Count - 1; num >= 0; num--)
		{
			if (model[num].Count == 0)
			{
				model.RemoveAt(num);
			}
		}
	}

	public override void DoWindowContents(Rect inRect)
	{
		InspectorSettings settings = InspectorMod.Settings;
		if (settings == null)
		{
			return;
		}
		if (loadedStamp != settings.LayoutStamp)
		{
			Load(settings);
		}
		float y = inRect.y;
		RadiusFont.Label(new Rect(inRect.x, y, inRect.width - 40f, 26f), "RUI.Inspector.LayoutEditorTitle".Translate(), GameFont.Medium, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		y += 32f;
		y = DrawToolbar(settings, inRect, y);
		CardChrome.Fill(new Rect(inRect.x, y, inRect.width, 1f), Palette.Border);
		y += 8f;
		Rect area = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - 26f);
		if (page == 1)
		{
			DrawPreview(area);
		}
		else if (page == 2)
		{
			DrawSettingsPage(settings, area);
		}
		else
		{
			DrawBoard(settings, area);
		}
		if (rowDragging && rowDropAt >= 0 && rowDropAt <= rowHits.Count)
		{
			float num = ((rowDropAt < rowHits.Count) ? rowHits[rowDropAt].y : ((rowHits.Count > 0) ? rowHits[rowHits.Count - 1].yMax : 0f));
			num += area.y - scroll.y;
			if (num > area.y && num < area.yMax)
			{
				CardChrome.Rounded(new Rect(area.x + 2f, num - 2f, area.width - 4f, 3f), RadiusTheme.Accent, 2f);
			}
		}
		RadiusFont.Label(new Rect(inRect.x, inRect.yMax - 22f, inRect.width, 20f), dragging ? "RUI.Inspector.LayoutDragHint".Translate() : "RUI.Inspector.LayoutEditorHint".Translate(), GameFont.Tiny, heading: false, dragging ? RadiusTheme.Accent : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		if (page == 0)
		{
			HandleInput(settings, area);
		}
		if (dragging)
		{
			DrawGhost();
		}
	}

	public override void PostClose()
	{
		base.PostClose();
		InspectorMod.Instance?.WriteSettings();
	}

	private void DrawSettingsPage(InspectorSettings s, Rect area)
	{
		float num = Mathf.Floor(area.width - 10f - 6f);
		Rect viewRect = new Rect(0f, 0f, num, Mathf.Max(setContentH, area.height));
		int depth = FlatScroll.Depth;
		FlatScroll.Begin(area, ref setScroll, viewRect);
		try
		{
			float cy = 0f;
			InspectorPanel.SheetHeading(num, ref cy, InspectorPanel.SecPortrait);
			Rect rect = new Rect(0f, cy, Mathf.Min(num, 280f), 26f);
			CardChrome.Rounded(rect, Palette.Surface3, 8f);
			float num2 = (rect.width - 4f) / 3f;
			for (int i = 0; i < 3; i++)
			{
				bool flag = s.portraitSize == i;
				Rect rect2 = new Rect(rect.x + 2f + (float)i * num2, rect.y + 2f, num2, 22f);
				if (flag)
				{
					CardChrome.Rounded(rect2, RadiusTheme.Accent, 8f);
				}
				RadiusFont.Label(rect2, i switch
				{
					1 => InspectorPanel.PortraitM, 
					0 => InspectorPanel.PortraitS, 
					_ => InspectorPanel.PortraitL, 
				}, GameFont.Small, flag, flag ? Palette.InkOnAccent : Palette.TextDim, TextAnchor.MiddleCenter, wrap: false);
				if (Widgets.ButtonInvisible(rect2))
				{
					s.portraitSize = i;
					InspectorCore.Poke();
				}
			}
			cy += 32f;
			SettingsSlider(num, ref cy, "RUI.Inspector.PortraitZoom".Translate(s.portraitZoom.ToStringPercent()), ref s.portraitZoom, 0.6f, 1.8f);
			SettingsSlider(num, ref cy, "RUI.Inspector.PortraitPanX".Translate(s.portraitPanX.ToString("0.00")), ref s.portraitPanX, -0.6f, 0.6f);
			SettingsSlider(num, ref cy, "RUI.Inspector.PortraitPanY".Translate(s.portraitPanY.ToString("0.00")), ref s.portraitPanY, -0.6f, 0.6f);
			InspectorPanel.SheetHeading(num, ref cy, InspectorPanel.SecTabs);
			IReadOnlyList<InspectorPanel.TabEntry> knownTabs = InspectorPanel.KnownTabs;
			for (int j = 0; j < knownTabs.Count; j++)
			{
				InspectorPanel.TabEntry tabEntry = knownTabs[j];
				string key = tabEntry.Key;
				Rect rect3 = new Rect(0f, cy, num, 26f);
				if (Mouse.IsOver(rect3))
				{
					CardChrome.Hover(rect3, 8f);
				}
				RadiusFont.Label(new Rect(4f, cy, num - 132f, 26f), tabEntry.Label, GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
				if (MiniGlyph(new Rect(num - 126f, cy + 3f, 20f, 20f), "↑") && j > 0)
				{
					MoveTab(s, knownTabs, j, -1);
				}
				if (MiniGlyph(new Rect(num - 102f, cy + 3f, 20f, 20f), "↓") && j < knownTabs.Count - 1)
				{
					MoveTab(s, knownTabs, j, 1);
				}
				bool flag2 = !s.hiddenTabs.Contains(key);
				Rect rect4 = new Rect(num - 40f, cy + 3f, 36f, 20f);
				CardChrome.Pill(rect4, flag2 ? RadiusTheme.Accent : Palette.Surface3);
				float num3 = rect4.height - 4f;
				CardChrome.Pill(new Rect(flag2 ? (rect4.xMax - num3 - 2f) : (rect4.x + 2f), rect4.y + 2f, num3, num3), Palette.Ink);
				if (Widgets.ButtonInvisible(rect4))
				{
					if (flag2)
					{
						if (!s.hiddenTabs.Contains(key))
						{
							s.hiddenTabs.Add(key);
						}
					}
					else
					{
						s.hiddenTabs.Remove(key);
					}
					InspectorCore.Poke();
				}
				cy += 28f;
			}
			InspectorPanel.SheetHeading(num, ref cy, InspectorPanel.SecOptions);
			InspectorPanel.SwitchRow(num, 0f, ref cy, InspectorPanel.LblMoodTicks, s.moodTicks, delegate(bool v)
			{
				s.moodTicks = v;
			});
			InspectorPanel.SwitchRow(num, 0f, ref cy, InspectorPanel.LblBands, s.healthBands, delegate(bool v)
			{
				s.healthBands = v;
			});
			InspectorPanel.Stepper(num, ref cy, InspectorPanel.LblRows, s.healthRows, 1, 8, delegate(int v)
			{
				s.healthRows = v;
			});
			InspectorPanel.SwitchRow(num, 0f, ref cy, InspectorPanel.LblHideZero, s.skillsHideZero, delegate(bool v)
			{
				s.skillsHideZero = v;
			});
			RadiusFont.Label(new Rect(0f, cy, num, 18f), "RUI.Inspector.PaneWidth".Translate(s.paneWidth), GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			cy += 18f;
			int num4 = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(0f, cy, Mathf.Min(num, 280f), 22f), s.paneWidth, 420f, 720f, middleAlignment: true));
			if (num4 != s.paneWidth)
			{
				s.paneWidth = num4;
				InspectorCore.Poke();
			}
			cy += 26f;
			RadiusFont.Label(new Rect(0f, cy, num, 18f), "RUI.Inspector.PaneHeight".Translate(s.paneHeight), GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			cy += 18f;
			s.paneHeight = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(0f, cy, Mathf.Min(num, 280f), 22f), s.paneHeight, 165f, 800f, middleAlignment: true));
			cy += 26f;
			cy += 8f;
			Rect rect5 = new Rect(0f, cy, 180f, 26f);
			bool flag3 = Mouse.IsOver(rect5);
			CardChrome.Rounded(rect5, flag3 ? Palette.Surface3 : Palette.Surface2, 8f);
			RadiusFont.Label(rect5, InspectorPanel.AllSettingsLbl, GameFont.Small, heading: false, flag3 ? RadiusTheme.Accent : Palette.Ink, TextAnchor.MiddleCenter, wrap: false);
			if (Widgets.ButtonInvisible(rect5) && InspectorMod.Instance != null)
			{
				Find.WindowStack.Add(new Dialog_ModSettings(InspectorMod.Instance));
			}
			Rect rect6 = new Rect(190f, cy, 180f, 26f);
			bool flag4 = Mouse.IsOver(rect6);
			CardChrome.Rounded(rect6, flag4 ? Palette.Surface3 : Palette.Surface2, 8f);
			RadiusFont.Label(rect6, InspectorPanel.LblReset, GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleCenter, wrap: false);
			if (Widgets.ButtonInvisible(rect6))
			{
				s.ResetToDefaults();
				Load(s);
			}
			cy += 34f;
			setContentH = Mathf.Max(cy, area.height);
		}
		finally
		{
			FlatScroll.EndOrUnwind(depth);
		}
	}

	private static bool MiniGlyph(Rect r, string glyph)
	{
		bool flag = Mouse.IsOver(r);
		if (flag)
		{
			CardChrome.Hover(r, 6f);
		}
		RadiusFont.Label(r, glyph, GameFont.Small, heading: false, flag ? RadiusTheme.Accent : Palette.TextDim, TextAnchor.MiddleCenter, wrap: false);
		return Widgets.ButtonInvisible(r);
	}

	private static void MoveTab(InspectorSettings s, IReadOnlyList<InspectorPanel.TabEntry> tabs, int i, int dir)
	{
		List<string> list = new List<string>(tabs.Count);
		for (int j = 0; j < tabs.Count; j++)
		{
			list.Add(tabs[j].Key);
		}
		int num = i + dir;
		List<string> list2 = list;
		int index = num;
		string value = list[num];
		string value2 = list[i];
		list2[i] = value;
		list[index] = value2;
		s.tabOrder.Clear();
		s.tabOrder.AddRange(list);
		InspectorCore.Poke();
	}

	private static void SettingsSlider(float sw, ref float cy, string label, ref float value, float min, float max)
	{
		RadiusFont.Label(new Rect(0f, cy, sw, 18f), label, GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		cy += 18f;
		value = Widgets.HorizontalSlider(new Rect(0f, cy, Mathf.Min(sw, 280f), 22f), value, min, max, middleAlignment: true);
		cy += 26f;
	}

	private float DrawToolbar(InspectorSettings s, Rect inRect, float y)
	{
		Rect rect = new Rect(inRect.x, y, 240f, 26f);
		CardChrome.Rounded(rect, Palette.Surface3, 8f);
		for (int i = 0; i < 3; i++)
		{
			bool flag = page == i;
			Rect rect2 = new Rect(rect.x + 2f + (float)i * ((rect.width - 4f) / 3f), rect.y + 2f, (rect.width - 4f) / 3f, 22f);
			if (flag)
			{
				CardChrome.Rounded(rect2, RadiusTheme.Accent, 8f);
			}
			RadiusFont.Label(rect2, i switch
			{
				1 => "RUI.Inspector.LayoutPreview".Translate(), 
				0 => "RUI.Inspector.LayoutBuild".Translate(), 
				_ => "RUI.Inspector.LayoutSettings".Translate(), 
			}, GameFont.Tiny, flag, flag ? Palette.InkOnAccent : Palette.TextDim, TextAnchor.MiddleCenter, wrap: false);
			if (Widgets.ButtonInvisible(rect2))
			{
				page = i;
			}
		}
		if (page != 2)
		{
			Rect rect3 = new Rect(rect.xMax + 8f, y, Mathf.Min(430f, inRect.xMax - rect.xMax - 8f), 26f);
			CardChrome.Rounded(rect3, Palette.Surface3, 8f);
			float num = (rect3.width - 4f) / 5f;
			for (int j = 0; j < 5; j++)
			{
				bool flag2 = j == 4;
				bool flag3 = (things ? flag2 : (!flag2 && kind == (PawnProfileKind)j));
				Rect rect4 = new Rect(rect3.x + 2f + (float)j * num, rect3.y + 2f, num, 22f);
				if (flag3)
				{
					CardChrome.Rounded(rect4, RadiusTheme.Accent, 8f);
				}
				RadiusFont.Label(rect4, flag2 ? ThingsLabel : ProfileLabels[j], GameFont.Tiny, flag3, flag3 ? Palette.InkOnAccent : Palette.TextDim, TextAnchor.MiddleCenter, wrap: false);
				if (Widgets.ButtonInvisible(rect4) && !flag3)
				{
					things = flag2;
					if (!flag2)
					{
						kind = (PawnProfileKind)j;
					}
					lane = 0;
					Load(s);
					SoundDefOf.Click.PlayOneShotOnCamera();
				}
			}
		}
		if (page == 2)
		{
			return y + 32f;
		}
		y += 32f;
		float num2 = inRect.x;
		if (!things)
		{
			// Segments sized to their labels: a fixed width cut "Panneau de gauche".
			float a = Mathf.Max(RadiusFont.Size(LaneBody, GameFont.Tiny, heading: true).x, RadiusFont.Size(LaneRail, GameFont.Tiny, heading: true).x) + 24f;
			Rect rect5 = new Rect(inRect.x, y, Mathf.Max(190f, a * 2f + 4f), 26f);
			CardChrome.Rounded(rect5, Palette.Surface3, 8f);
			float num3 = (rect5.width - 4f) / 2f;
			for (int k = 0; k < 2; k++)
			{
				bool flag4 = lane == k;
				Rect rect6 = new Rect(rect5.x + 2f + (float)k * num3, rect5.y + 2f, num3, 22f);
				if (flag4)
				{
					CardChrome.Rounded(rect6, RadiusTheme.Accent, 8f);
				}
				RadiusFont.Label(rect6, (k == 0) ? LaneBody : LaneRail, GameFont.Tiny, flag4, flag4 ? Palette.InkOnAccent : Palette.TextDim, TextAnchor.MiddleCenter, wrap: false);
				if (Widgets.ButtonInvisible(rect6) && lane != k)
				{
					lane = k;
					Load(s);
				}
			}
			num2 = rect5.xMax;
		}
		toolButtons.Clear();
		toolButtons.Add(new ToolButton(ResetLayoutLbl, ResetProfileTip.Formatted(things ? ThingsLabel : ProfileLabels[(int)kind]), danger: true, delegate
		{
			if (things)
			{
				s.ResetThingLayout();
			}
			else
			{
				s.ResetProfile(kind);
			}
			InspectorCore.Poke();
			Load(s);
			SoundDefOf.Click.PlayOneShotOnCamera();
		}));
		if (!things)
		{
			toolButtons.Add(new ToolButton(CopyFromLbl, null, danger: false, delegate
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				for (int l = 0; l < 4; l++)
				{
					if (l != (int)kind)
					{
						PawnProfileKind from = (PawnProfileKind)l;
						list.Add(new FloatMenuOption(ProfileLabels[l], delegate
						{
							s.CopyLane(from, kind, lane == 1);
							InspectorCore.Poke();
							Load(s);
						}));
					}
				}
				Find.WindowStack.Add(new FloatMenu(list));
			}));
			// The player's own presets and the RimHUD import (Dialog_LayoutPresets).
			toolButtons.Add(new ToolButton(UserPresetsLbl, UserPresetsTip, danger: false, delegate
			{
				Find.WindowStack.Add(new Dialog_LayoutPresets());
			}));
			if (lane == 0)
			{
				InspectorSettings.LayoutPreset[] presets = InspectorSettings.Presets;
				for (int num8 = 0; num8 < presets.Length; num8++)
				{
					InspectorSettings.LayoutPreset layoutPreset = presets[num8];
					toolButtons.Add(new ToolButton(layoutPreset.Label, layoutPreset.Blurb, danger: false, delegate
					{
						s.ApplyPreset(layoutPreset, kind);
						Load(s);
						SoundDefOf.Click.PlayOneShotOnCamera();
					}));
				}
			}
		}
		return DrawToolButtons(inRect, num2 + 12f, y);
	}

	// Buttons sized to their labels, right-aligned; a full row wraps below. The first row shares the line
	// of the lane toggle, so it starts after it.
	private float DrawToolButtons(Rect inRect, float firstRowLeft, float y)
	{
		float num = y;
		float num2 = firstRowLeft;
		int num3 = 0;
		while (num3 < toolButtons.Count)
		{
			float num4 = 0f;
			int i;
			for (i = num3; i < toolButtons.Count; i++)
			{
				float num5 = num4 + ((i > num3) ? 4f : 0f) + ToolButtonWidth(toolButtons[i].Label);
				if (num5 > inRect.xMax - num2 && i > num3)
				{
					break;
				}
				num4 = num5;
			}
			if (num4 > inRect.xMax - num2 && num2 > inRect.x)
			{
				num += 32f;
				num2 = inRect.x;
				continue;
			}
			float num6 = inRect.xMax - num4;
			for (int j = num3; j < i; j++)
			{
				ToolButton toolButton = toolButtons[j];
				Rect rect = new Rect(num6, num, ToolButtonWidth(toolButton.Label), 26f);
				bool flag = Mouse.IsOver(rect);
				CardChrome.Rounded(rect, flag ? Palette.Surface3 : Palette.Surface2, 8f);
				RadiusFont.Label(rect, toolButton.Label, GameFont.Tiny, heading: false, flag ? (toolButton.Danger ? Palette.Bad : RadiusTheme.Accent) : Palette.Ink, TextAnchor.MiddleCenter, wrap: false);
				if (!toolButton.Tip.NullOrEmpty())
				{
					TooltipHandler.TipRegion(rect, toolButton.Tip);
				}
				if (Widgets.ButtonInvisible(rect))
				{
					toolButton.OnClick();
				}
				num6 = rect.xMax + 4f;
			}
			num3 = i;
			if (num3 < toolButtons.Count)
			{
				num += 32f;
				num2 = inRect.x;
			}
		}
		return num + 32f;
	}

	private static float ToolButtonWidth(string label)
	{
		return Mathf.Max(64f, RadiusFont.Size(label, GameFont.Tiny).x + 20f);
	}

	private void DrawBoard(InspectorSettings s, Rect area)
	{
		slotHits.Clear();
		rowHits.Clear();
		gripHits.Clear();
		float a = (float)model.Count * 49f + 40f;
		float width = area.width - 10f - 6f;
		Rect viewRect = new Rect(0f, 0f, width, Mathf.Max(a, area.height));
		int depth = FlatScroll.Depth;
		FlatScroll.Begin(area, ref scroll, viewRect);
		try
		{
			float num = 0f;
			for (int i = 0; i < model.Count; i++)
			{
				Rect rect = new Rect(0f, num, width, 44f);
				rowHits.Add(rect);
				DrawRow(s, i, rect);
				num += 49f;
			}
			Rect rect2 = new Rect(0f, num + 2f, width, 28f);
			bool flag = Mouse.IsOver(rect2);
			CardChrome.Outline(rect2, flag ? RadiusTheme.Accent : Palette.Border, 1f, 8f);
			RadiusFont.Label(rect2, "RUI.Inspector.LayoutAddRow".Translate(), GameFont.Tiny, heading: false, flag ? RadiusTheme.Accent : Palette.TextDim, TextAnchor.MiddleCenter, wrap: false);
			if (Widgets.ButtonInvisible(rect2))
			{
				Find.WindowStack.Add(new FloatMenu(InspectorPanel.BuildCellOptions(delegate(string cell)
				{
					model.Add(new List<string> { cell });
					Save(s);
				}, things)));
			}
		}
		finally
		{
			FlatScroll.EndOrUnwind(depth);
		}
	}

	private void DrawRow(InspectorSettings s, int r, Rect row)
	{
		List<string> list = model[r];
		if (Mouse.IsOver(row))
		{
			CardChrome.Rounded(row, Palette.HoverWash, 8f);
		}
		Rect rect = new Rect(row.x + 2f, row.y + 2f, 18f, 40f);
		gripHits.Add(rect);
		bool flag = Mouse.IsOver(rect) && !dragging;
		if (flag)
		{
			CardChrome.Rounded(rect, Palette.Surface3, 6f);
		}
		RadiusFont.Label(rect, "⋮⋮", GameFont.Small, heading: false, (rowDragging && rowDragFrom == r) ? RadiusTheme.Accent : (flag ? Palette.Ink : Palette.TextDim), TextAnchor.MiddleCenter, wrap: false);
		TooltipHandler.TipRegion(rect, "RUI.Inspector.LayoutGripTip".Translate());
		bool flag2 = list.Count < MaxCells;
		float num = row.x + 22f;
		float num2 = row.width - 22f - 74f;
		float num3 = (flag2 ? Mathf.Min(110f, num2 * 0.25f) : 0f);
		int num4 = list.Count - 1 + (flag2 ? 1 : 0);
		float num5 = (num2 - num3 - 8f * (float)num4) / (float)Mathf.Max(1, list.Count);
		for (int i = 0; i < list.Count; i++)
		{
			Rect rect2 = new Rect(num + (float)i * (num5 + 8f), row.y + 2f, num5, 40f);
			slotHits.Add(new SlotHit
			{
				rect = rect2,
				row = r,
				col = i,
				filled = true
			});
			DrawFilledSlot(s, r, i, list[i], rect2);
		}
		if (flag2)
		{
			Rect rect3 = new Rect(num + (float)list.Count * (num5 + 8f), row.y + 2f, num3, 40f);
			slotHits.Add(new SlotHit
			{
				rect = rect3,
				row = r,
				col = list.Count,
				filled = false
			});
			DrawEmptySlot(s, r, rect3);
		}
		float bx = row.xMax - 24f;
		if (MiniTool(ref bx, row, "✕", "RUI.Inspector.LayoutDelRow".Translate(), danger: true))
		{
			model.RemoveAt(r);
			Save(s);
		}
		else if (MiniTool(ref bx, row, "↓", "RUI.Inspector.LayoutMoveDown".Translate(), danger: false) && r < model.Count - 1)
		{
			List<List<string>> list2 = model;
			int index = r;
			List<List<string>> list3 = model;
			int index2 = r + 1;
			List<string> value = model[r + 1];
			List<string> value2 = model[r];
			list2[index] = value;
			list3[index2] = value2;
			Save(s);
		}
		else if (MiniTool(ref bx, row, "↑", "RUI.Inspector.LayoutMoveUp".Translate(), danger: false) && r > 0)
		{
			List<List<string>> list2 = model;
			int index2 = r;
			List<List<string>> list4 = model;
			int index = r - 1;
			List<string> value2 = model[r - 1];
			List<string> value = model[r];
			list2[index2] = value2;
			list4[index] = value;
			Save(s);
		}
	}

	private static Rect PenRect(Rect sr)
	{
		return new Rect(sr.x + 5f, sr.y + sr.height / 2f - 10f, 20f, 20f);
	}

	private static Rect KillRect(Rect sr)
	{
		return new Rect(sr.xMax - 22f, sr.y + sr.height / 2f - 9f, 18f, 18f);
	}

	private void DrawFilledSlot(InspectorSettings s, int r, int c, string id, Rect sr)
	{
		bool flag = dragging && dragFromRow == r && dragFromCol == c;
		bool flag2 = id.StartsWith("header:");
		bool flag3 = Mouse.IsOver(sr) && !dragging;
		Color color = (flag2 ? (RadiusTheme.Accent * new Color(1f, 1f, 1f, 0.16f)) : (flag3 ? Palette.Surface3 : Palette.Surface2));
		CardChrome.Rounded(sr, color, 8f);
		if (flag)
		{
			CardChrome.Rounded(sr, new Color(0f, 0f, 0f, 0.55f), 8f);
		}
		if (dragging && Mouse.IsOver(sr) && !flag)
		{
			CardChrome.Outline(sr, RadiusTheme.Accent, 2f, 8f);
		}
		Rect rect = PenRect(sr);
		bool flag4 = flag2 && Mouse.IsOver(rect) && !dragging;
		if (flag4)
		{
			CardChrome.Rounded(rect, Palette.Surface3, 6f);
		}
		IconSet.Get(InspectorPanel.CellIcon(id)).Draw(new Rect(sr.x + 8f, sr.y + sr.height / 2f - 8f, 16f, 16f), (!flag2) ? Palette.TextDim : (flag4 ? Palette.Ink : RadiusTheme.Accent));
		if (flag2)
		{
			TooltipHandler.TipRegion(rect, "RUI.Inspector.RenameTip".Translate());
			if (!dragging && Widgets.ButtonInvisible(rect))
			{
				int rr = r;
				int cc = c;
				Find.WindowStack.Add(new Dialog_RenameHeader(InspectorPanel.CellDisplay(id), delegate(string text2)
				{
					if (rr < model.Count && cc < model[rr].Count)
					{
						model[rr][cc] = "header:" + text2;
						Save(s);
					}
				}));
			}
		}
		string text = InspectorPanel.CellDisplay(id);
		RadiusFont.Label(new Rect(sr.x + 30f, sr.y, sr.width - 54f, sr.height), text, GameFont.Small, flag2, flag2 ? RadiusTheme.Accent : Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		Rect rect2 = KillRect(sr);
		if (Mouse.IsOver(sr))
		{
			bool flag5 = Mouse.IsOver(rect2);
			IconSet.Get("Action/Decline").Draw(rect2.ContractedBy(3f), flag5 ? Palette.Bad : Palette.TextDim);
			if (Widgets.ButtonInvisible(rect2))
			{
				model[r].RemoveAt(c);
				Prune();
				Save(s);
			}
		}
	}

	private void DrawEmptySlot(InspectorSettings s, int r, Rect sr)
	{
		bool flag = Mouse.IsOver(sr);
		CardChrome.Outline(sr, (dragging && flag) ? RadiusTheme.Accent : (flag ? RadiusTheme.Accent : Palette.Border), (dragging && flag) ? 2f : 1f, 8f);
		RadiusFont.Label(sr, "RUI.Inspector.LayoutAddCell".Translate(), GameFont.Tiny, heading: false, flag ? RadiusTheme.Accent : Palette.TextDim, TextAnchor.MiddleCenter, wrap: false);
		if (dragging || !Widgets.ButtonInvisible(sr))
		{
			return;
		}
		int row = r;
		Find.WindowStack.Add(new FloatMenu(InspectorPanel.BuildCellOptions(delegate(string cell)
		{
			if (row < model.Count && model[row].Count < MaxCells)
			{
				model[row].Add(cell);
			}
			Save(s);
		}, things)));
	}

	private bool MiniTool(ref float bx, Rect row, string glyph, string tip, bool danger)
	{
		Rect rect = new Rect(bx, row.y + 22f - 11f, 22f, 22f);
		bx -= 24f;
		bool flag = Mouse.IsOver(rect);
		if (flag)
		{
			CardChrome.Rounded(rect, danger ? (Palette.Bad * new Color(1f, 1f, 1f, 0.3f)) : Palette.Surface3, 6f);
		}
		RadiusFont.Label(rect, glyph, GameFont.Tiny, heading: false, (!flag) ? Palette.TextDim : (danger ? Palette.Bad : Palette.Ink), TextAnchor.MiddleCenter, wrap: false);
		TooltipHandler.TipRegion(rect, tip);
		return Widgets.ButtonInvisible(rect);
	}

	private void DrawPreview(Rect area)
	{
		InspectorSettings settings = InspectorMod.Settings;
		float num = Mathf.Min(760f, area.width);
		Rect rect = new Rect(area.x + (area.width - num) / 2f, area.y, num, area.height);
		CardChrome.Card(rect);
		List<string> list = (things ? null : settings.EffectiveRailFor(kind));
		List<string> list2 = (things ? settings.EffectiveThingLayout() : settings.EffectiveLayoutFor(kind));
		float a = (float)Mathf.Max((list != null) ? CountRows(list) : 0, CountRows(list2)) * 26f;
		Rect outRect = new Rect(rect.x, rect.y + 10f, rect.width, rect.height - 20f);
		float num2 = outRect.width - 10f - 6f;
		Rect viewRect = new Rect(0f, 0f, num2, Mathf.Max(a, outRect.height));
		int depth = FlatScroll.Depth;
		FlatScroll.Begin(outRect, ref previewScroll, viewRect);
		try
		{
			if (list == null)
			{
				PreviewColumn(list2, 12f, num2 - 24f);
				return;
			}
			float num3 = Mathf.Floor(num * 0.3f);
			PreviewColumn(list, 12f, num3 - 24f);
			CardChrome.Fill(new Rect(num3, 0f, 1f, Mathf.Max(a, outRect.height)), Palette.Border);
			PreviewColumn(list2, num3 + 12f, num2 - num3 - 24f);
		}
		finally
		{
			FlatScroll.EndOrUnwind(depth);
		}
	}

	private static int CountRows(List<string> rows)
	{
		int num = 0;
		for (int i = 0; i < rows.Count; i++)
		{
			if (!rows[i].NullOrEmpty())
			{
				num++;
			}
		}
		return num;
	}

	private static void PreviewColumn(List<string> rowsList, float x0, float colW)
	{
		float num = 0f;
		for (int i = 0; i < rowsList.Count; i++)
		{
			string text = rowsList[i];
			if (text.NullOrEmpty())
			{
				continue;
			}
			string[] array = text.Split(new char[1] { '|' });
			int num2 = array.Length;
			float num3 = (colW - 10f * (float)(num2 - 1)) / (float)num2;
			for (int j = 0; j < num2; j++)
			{
				Rect rect = new Rect(x0 + (float)j * (num3 + 10f), num, num3, 22f);
				string text2 = array[j];
				if (text2.StartsWith("header:"))
				{
					RadiusFont.Label(rect, InspectorPanel.CellDisplay(text2), GameFont.Tiny, heading: true, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
					continue;
				}
				CardChrome.Rounded(rect, Palette.Surface2, 8f);
				RadiusFont.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height), InspectorPanel.CellDisplay(text2), GameFont.Tiny, heading: false, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
			}
			num += 26f;
		}
	}

	private void DrawGhost()
	{
		Vector2 mousePosition = Event.current.mousePosition;
		string text = InspectorPanel.CellDisplay(dragId);
		float width = RadiusFont.Size(text).x + 24f;
		Rect rect = new Rect(mousePosition.x + 12f, mousePosition.y + 12f, width, 24f);
		CardChrome.Rounded(rect, RadiusTheme.Accent, 8f);
		RadiusFont.Label(rect, text, GameFont.Tiny, heading: true, Palette.InkOnAccent, TextAnchor.MiddleCenter, wrap: false);
	}

	private void HandleInput(InspectorSettings s, Rect area)
	{
		Event current = Event.current;
		Vector2 mousePosition = current.mousePosition;
		EventType type = current.type;
		bool flag = area.Contains(mousePosition);
		if (type == EventType.MouseDown && current.button == 0 && !rowArmed && !rowDragging)
		{
			for (int i = 0; i < gripHits.Count; i++)
			{
				Rect rect = new Rect(gripHits[i].x + area.x, gripHits[i].y + area.y - scroll.y, gripHits[i].width, gripHits[i].height);
				if (rect.Contains(mousePosition))
				{
					rowArmed = true;
					rowArmIndex = i;
					armPos = mousePosition;
					break;
				}
			}
		}
		if (rowArmed && !rowDragging && type == EventType.MouseDrag && (mousePosition - armPos).sqrMagnitude > 16f)
		{
			rowDragging = true;
			rowDragFrom = rowArmIndex;
			rowArmed = false;
		}
		if (rowDragging && (type == EventType.MouseDrag || type == EventType.MouseUp))
		{
			rowDropAt = rowHits.Count;
			for (int j = 0; j < rowHits.Count; j++)
			{
				Rect rect2 = new Rect(rowHits[j].x + area.x, rowHits[j].y + area.y - scroll.y, rowHits[j].width, rowHits[j].height);
				if (mousePosition.y < rect2.y + rect2.height * 0.5f)
				{
					rowDropAt = j;
					break;
				}
			}
		}
		if (type == EventType.MouseUp && current.button == 0 && rowDragging)
		{
			int num = rowDropAt;
			if (num > rowDragFrom)
			{
				num--;
			}
			if (num >= 0 && num < model.Count && num != rowDragFrom)
			{
				List<string> item = model[rowDragFrom];
				model.RemoveAt(rowDragFrom);
				model.Insert(num, item);
				Save(s);
			}
			rowDragging = false;
			rowDropAt = -1;
			rowArmed = false;
			return;
		}
		if (rowArmed && type == EventType.MouseUp)
		{
			rowArmed = false;
		}
		if (type == EventType.MouseDown && current.button == 0 && !rowDragging)
		{
			for (int k = 0; k < slotHits.Count; k++)
			{
				SlotHit slotHit = slotHits[k];
				if (!slotHit.filled)
				{
					continue;
				}
				Rect sr = new Rect(slotHit.rect.x + area.x, slotHit.rect.y + area.y - scroll.y, slotHit.rect.width, slotHit.rect.height);
				if (sr.Contains(mousePosition))
				{
					if (!KillRect(sr).Contains(mousePosition) && (!model[slotHit.row][slotHit.col].StartsWith("header:") || !PenRect(sr).Contains(mousePosition)))
					{
						armed = true;
						armPos = mousePosition;
						armRow = slotHit.row;
						armCol = slotHit.col;
					}
					break;
				}
			}
		}
		if (armed && !dragging && type == EventType.MouseDrag && (mousePosition - armPos).sqrMagnitude > 16f)
		{
			if (armRow < model.Count && armCol < model[armRow].Count)
			{
				dragging = true;
				dragId = model[armRow][armCol];
				dragFromRow = armRow;
				dragFromCol = armCol;
			}
			armed = false;
		}
		if (type == EventType.MouseUp && current.button == 0)
		{
			if (dragging)
			{
				Drop(s, mousePosition, area);
			}
			else if (armed)
			{
				int rr = armRow;
				int cc = armCol;
				Find.WindowStack.Add(new FloatMenu(InspectorPanel.BuildCellOptions(delegate(string cell)
				{
					if (rr < model.Count && cc < model[rr].Count)
					{
						model[rr][cc] = cell;
						Save(s);
					}
				}, things)));
			}
			armed = false;
		}
		if (type == EventType.MouseDrag && (dragging || rowDragging || flag) && current.type != EventType.Used)
		{
			current.Use();
		}
	}

	private void Drop(InspectorSettings s, Vector2 m, Rect area)
	{
		dragging = false;
		if (dragFromRow >= model.Count || dragFromCol >= model[dragFromRow].Count)
		{
			return;
		}
		for (int i = 0; i < slotHits.Count; i++)
		{
			SlotHit slotHit = slotHits[i];
			if (!new Rect(slotHit.rect.x + area.x, slotHit.rect.y + area.y - scroll.y, slotHit.rect.width, slotHit.rect.height).Contains(m))
			{
				continue;
			}
			if (slotHit.row != dragFromRow || slotHit.col != dragFromCol)
			{
				model[dragFromRow].RemoveAt(dragFromCol);
				int num = slotHit.row;
				if (model[dragFromRow].Count == 0 && dragFromRow < num)
				{
					num--;
				}
				Prune();
				if (num < 0 || num >= model.Count)
				{
					model.Add(new List<string> { dragId });
				}
				else if (model[num].Count < MaxCells)
				{
					model[num].Insert(Mathf.Min(slotHit.col, model[num].Count), dragId);
				}
				else
				{
					model[num][Mathf.Min(slotHit.col, model[num].Count - 1)] = dragId;
				}
				Save(s);
			}
			return;
		}
		if (!area.Contains(m))
		{
			return;
		}
		int num2 = model.Count;
		for (int j = 0; j < rowHits.Count; j++)
		{
			Rect rect = new Rect(rowHits[j].x + area.x, rowHits[j].y + area.y - scroll.y, rowHits[j].width, rowHits[j].height);
			if (m.y < rect.y + rect.height / 2f)
			{
				num2 = j;
				break;
			}
		}
		model[dragFromRow].RemoveAt(dragFromCol);
		if (model[dragFromRow].Count == 0 && dragFromRow < num2)
		{
			num2--;
		}
		Prune();
		model.Insert(Mathf.Clamp(num2, 0, model.Count), new List<string> { dragId });
		Save(s);
	}

	public override void PreClose()
	{
		base.PreClose();
		InspectorMod.Instance?.WriteSettings();
	}
}
