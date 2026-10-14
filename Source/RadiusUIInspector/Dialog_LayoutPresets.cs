using System.Collections.Generic;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RadiusUIInspector;

// The player's layout presets (every pawn profile, body and rail), shared between saves, and the import of
// RimHUD layouts into them. Same row pattern as the storage bookmarks.
public class Dialog_LayoutPresets : Window
{
	private const float RowH = 34f;

	private const float ButtonW = 74f;

	private const string NameControl = "RUI_LayoutPresetName";

	private string newName = "";

	private LayoutUserPreset? pendingDelete;

	private List<RimHudPresets.Entry>? rimHud;

	private Vector2 scroll;

	private float contentH = 300f;

	private string status = "";

	private bool statusBad;

	public override Vector2 InitialSize => new Vector2(560f, 560f);

	protected override float Margin => 14f;

	public Dialog_LayoutPresets()
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
	}

	public override void DoWindowContents(Rect inRect)
	{
		InspectorSettings settings = InspectorMod.Settings;
		if (settings == null)
		{
			return;
		}
		if (rimHud == null)
		{
			rimHud = RimHudPresets.Find();
		}
		float y = inRect.y;
		RadiusFont.Label(new Rect(inRect.x, y, inRect.width - 40f, 26f), "RUI.Inspector.UserPresetsTitle".Translate(), GameFont.Medium, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		y += 32f;
		y = DrawSaveRow(settings, inRect.x, y, inRect.width);
		y += 10f;
		float num = (status.Length > 0) ? 26f : 0f;
		Rect rect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - num);
		float width = rect.width - 16f;
		int depth = FlatScroll.Depth;
		FlatScroll.Begin(rect, ref scroll, new Rect(0f, 0f, width, Mathf.Max(contentH, rect.height)));
		try
		{
			float cy = 0f;
			cy += InspectorPanel.DrawEyebrow("RUI.Inspector.UserPresetsMine".Translate(settings.layoutPresets.Count), 0f, width, cy) + 6f;
			if (settings.layoutPresets.Count == 0)
			{
				RadiusFont.Label(new Rect(8f, cy, width - 16f, 22f), "RUI.Inspector.UserPresetsNone".Translate(), GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
				cy += 26f;
			}
			for (int i = 0; i < settings.layoutPresets.Count; i++)
			{
				if (DrawPresetRow(settings, settings.layoutPresets[i], width, cy))
				{
					break;
				}
				cy += RowH + 4f;
			}
			cy += 14f;
			cy += InspectorPanel.DrawEyebrow("RUI.Inspector.RimHudImport".Translate(), 0f, width, cy) + 6f;
			if (rimHud.Count == 0)
			{
				RadiusFont.Label(new Rect(8f, cy, width - 16f, 22f), "RUI.Inspector.RimHudNone".Translate(), GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
				cy += 26f;
			}
			for (int j = 0; j < rimHud.Count; j++)
			{
				DrawRimHudRow(settings, rimHud[j], width, cy);
				cy += RowH + 4f;
			}
			contentH = cy + 4f;
		}
		finally
		{
			FlatScroll.EndOrUnwind(depth);
		}
		if (num > 0f)
		{
			RadiusFont.Label(new Rect(inRect.x, inRect.yMax - 22f, inRect.width, 22f), status, GameFont.Tiny, heading: false, statusBad ? Palette.Warn : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			TooltipHandler.TipRegion(new Rect(inRect.x, inRect.yMax - 22f, inRect.width, 22f), status);
		}
	}

	private float DrawSaveRow(InspectorSettings s, float x, float y, float w)
	{
		float num = 150f;
		Rect rect = new Rect(x, y, w - num - 4f, 28f);
		CardChrome.Rounded(rect, Palette.Surface2, 8f);
		Event current = Event.current;
		bool flag = current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == NameControl;
		GUI.SetNextControlName(NameControl);
		newName = Widgets.TextField(rect.ContractedBy(4f), newName, 40);
		if (newName.Length == 0)
		{
			RadiusFont.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height), "RUI.Inspector.UserPresetNameHint".Translate(), GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		}
		string text = newName.Trim();
		LayoutUserPreset layoutUserPreset = FindPreset(s, text);
		Rect r = new Rect(rect.xMax + 4f, y, num, 28f);
		TooltipHandler.TipRegion(r, "RUI.Inspector.UserPresetSaveTip".Translate());
		if (StoragePanel.SmallButton(r, ((layoutUserPreset != null) ? "RUI.Inspector.UserPresetUpdate" : "RUI.Inspector.UserPresetSave").Translate(), hot: true, "Action/Plus") || flag)
		{
			if (text.Length == 0)
			{
				text = DefaultName(s);
			}
			if (layoutUserPreset != null)
			{
				layoutUserPreset.CaptureFrom(s);
			}
			else
			{
				s.layoutPresets.Add(LayoutUserPreset.Capture(s, text));
			}
			Saved("RUI.Inspector.UserPresetSaved".Translate(text), bad: false);
			newName = "";
			GUI.FocusControl(null);
			if (flag)
			{
				current.Use();
			}
		}
		return y + 28f;
	}

	// Returns true when the list changed under the loop (a delete), so the caller stops iterating.
	private bool DrawPresetRow(InspectorSettings s, LayoutUserPreset p, float w, float y)
	{
		Rect rect = new Rect(0f, y, w, RowH);
		if (Mouse.IsOver(rect))
		{
			CardChrome.Hover(rect, 8f);
		}
		Rect rect2 = new Rect(rect.xMax - 26f, y + 5f, 24f, 24f);
		Rect rect3 = new Rect(rect2.x - 28f, y + 5f, 24f, 24f);
		Rect r = new Rect(rect3.x - 4f - ButtonW, y + 5f, ButtonW, 24f);
		Rect r2 = new Rect(r.x - 4f - ButtonW, y + 5f, ButtonW, 24f);
		DrawName(new Rect(8f, y, r2.x - 12f, RowH), p.name, p.source);
		TooltipHandler.TipRegion(r2, "RUI.Inspector.UserPresetApplyTip".Translate());
		if (StoragePanel.SmallButton(r2, "RUI.Inspector.UserPresetApply".Translate()))
		{
			pendingDelete = null;
			p.ApplyTo(s);
			Saved("RUI.Inspector.UserPresetApplied".Translate(p.name), bad: false);
		}
		TooltipHandler.TipRegion(r, "RUI.Inspector.UserPresetOverwriteTip".Translate());
		if (StoragePanel.SmallButton(r, "RUI.Inspector.BookmarkOverwrite".Translate()))
		{
			pendingDelete = null;
			p.CaptureFrom(s);
			Saved("RUI.Inspector.UserPresetSaved".Translate(p.name), bad: false);
		}
		bool flag = Mouse.IsOver(rect3);
		CardChrome.Rounded(rect3, flag ? Palette.Surface3 : Palette.Surface2, 8f);
		IconSet.Get("Action/Rename").Draw(rect3.ContractedBy(5f), flag ? Palette.Ink : Palette.TextDim);
		TooltipHandler.TipRegion(rect3, "RUI.Inspector.BookmarkRename".Translate());
		if (Widgets.ButtonInvisible(rect3))
		{
			pendingDelete = null;
			Find.WindowStack.Add(new Dialog_RenameHeader(p.name, delegate(string name)
			{
				p.name = name;
				Saved("", bad: false);
			}, "RUI.Inspector.UserPresetRename".Translate(), p.name));
		}
		// Deleting is permanent and shared by every save: the first click arms the button, the second deletes.
		bool flag2 = pendingDelete == p;
		bool flag3 = Mouse.IsOver(rect2);
		CardChrome.Rounded(rect2, flag2 ? Palette.Bad : (flag3 ? Palette.Surface3 : Palette.Surface2), 8f);
		IconSet.Get("Action/DevTrash").Draw(rect2.ContractedBy(5f), (flag2 || flag3) ? Palette.Ink : Palette.TextDim);
		TooltipHandler.TipRegion(rect2, (flag2 ? "RUI.Inspector.UserPresetDeleteConfirm" : "RUI.Inspector.UserPresetDelete").Translate());
		if (Widgets.ButtonInvisible(rect2))
		{
			if (!flag2)
			{
				pendingDelete = p;
				return false;
			}
			pendingDelete = null;
			s.layoutPresets.Remove(p);
			Saved("", bad: false);
			return true;
		}
		return false;
	}

	private void DrawRimHudRow(InspectorSettings s, RimHudPresets.Entry e, float w, float y)
	{
		Rect rect = new Rect(0f, y, w, RowH);
		if (Mouse.IsOver(rect))
		{
			CardChrome.Hover(rect, 8f);
		}
		Rect r = new Rect(rect.xMax - ButtonW - 2f, y + 5f, ButtonW, 24f);
		string text = e.Source;
		if (e.MissingMods)
		{
			text = text + " · " + "RUI.Inspector.RimHudMissingMods".Translate();
		}
		DrawName(new Rect(8f, y, r.x - 12f, RowH), e.Name, text);
		TooltipHandler.TipRegion(rect, e.Path);
		TooltipHandler.TipRegion(r, "RUI.Inspector.RimHudImportTip".Translate());
		if (!StoragePanel.SmallButton(r, "RUI.Inspector.RimHudImportOne".Translate()))
		{
			return;
		}
		pendingDelete = null;
		RimHudPresets.Result result = RimHudPresets.Import(e, s);
		if (result.Preset == null)
		{
			status = "RUI.Inspector.RimHudFailed".Translate(e.Name, result.Error);
			statusBad = true;
			return;
		}
		result.Preset.name = UniqueName(s, result.Preset.name);
		s.layoutPresets.Add(result.Preset);
		Saved((result.Skipped.Count == 0) ? "RUI.Inspector.RimHudImported".Translate(result.Preset.name) : "RUI.Inspector.RimHudImportedSkipped".Translate(result.Preset.name, string.Join(", ", result.Skipped.ToArray())), result.Skipped.Count > 0);
	}

	private static void DrawName(Rect r, string name, string source)
	{
		if (source.NullOrEmpty())
		{
			RadiusFont.Label(r, name, GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
			return;
		}
		RadiusFont.Label(new Rect(r.x, r.y + 1f, r.width, 18f), name, GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		RadiusFont.Label(new Rect(r.x, r.y + 17f, r.width, 16f), source, GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
	}

	private void Saved(string message, bool bad)
	{
		if (message.Length > 0)
		{
			status = message;
			statusBad = bad;
		}
		InspectorMod.Instance?.WriteSettings();
		InspectorCore.Poke();
		SoundDefOf.Click.PlayOneShotOnCamera();
	}

	private static LayoutUserPreset? FindPreset(InspectorSettings s, string name)
	{
		if (name.Length == 0)
		{
			return null;
		}
		for (int i = 0; i < s.layoutPresets.Count; i++)
		{
			if (string.Equals(s.layoutPresets[i].name, name, System.StringComparison.OrdinalIgnoreCase))
			{
				return s.layoutPresets[i];
			}
		}
		return null;
	}

	private static string DefaultName(InspectorSettings s)
	{
		return UniqueName(s, "RUI.Inspector.UserPresetDefaultName".Translate());
	}

	private static string UniqueName(InspectorSettings s, string name)
	{
		if (FindPreset(s, name) == null)
		{
			return name;
		}
		int num = 2;
		while (FindPreset(s, name + " " + num) != null)
		{
			num++;
		}
		return name + " " + num;
	}
}
