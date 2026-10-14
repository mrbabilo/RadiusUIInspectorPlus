using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public class InspectorMod : Mod
{
	public static InspectorMod? Instance;

	public static InspectorSettings Settings;

	private Vector2 settingsScroll;

	private float settingsContentH = 520f;

	// Useless Filter's settings file, looked up once per game; empty when there is none.
	private static string? uselessFile;

	public InspectorMod(ModContentPack content)
		: base(content)
	{
		Instance = this;
		Settings = GetSettings<InspectorSettings>();
	}

	public override string SettingsCategory()
	{
		return "RUI.Inspector.Title".Translate();
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		CardChrome.Rounded(inRect, Palette.Surface0, 12f);
		Rect outRect = inRect.ContractedBy(16f);
		Rect viewRect = new Rect(0f, 0f, outRect.width - 20f, Mathf.Max(settingsContentH, outRect.height));
		int depth = FlatScroll.Depth;
		FlatScroll.Begin(outRect, ref settingsScroll, viewRect);
		try
		{
			float y = 0f;
			float width = viewRect.width;
			Heading(ref y, width, "RUI.Inspector.SectionGeneral".Translate());
			CheckRow(ref y, width, "RUI.Inspector.Enabled".Translate(), ref Settings.enabled, "RUI.Inspector.EnabledTip".Translate());
			CheckRow(ref y, width, "RUI.Inspector.AutoMinimize".Translate(), ref Settings.autoMinimize, "RUI.Inspector.AutoMinimizeTip".Translate());
			if (Settings.autoMinimize)
			{
				RadiusFont.Label(new Rect(0f, y, width, 23f), "RUI.Inspector.MinimizeDelay".Translate(Mathf.RoundToInt(Settings.minimizeDelay)), GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
				y += 23f;
				Settings.minimizeDelay = Widgets.HorizontalSlider(new Rect(0f, y, Mathf.Min(width, 320f), 22f), Settings.minimizeDelay, 3f, 60f, middleAlignment: true);
				y += 26f;
			}
			Rect rect = new Rect(0f, y, width, 23f);
			RadiusFont.Label(rect, "RUI.Inspector.PaneWidth".Translate(Settings.paneWidth), GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			TooltipHandler.TipRegion(rect, "RUI.Inspector.PaneWidthTip".Translate());
			y += 23f;
			Settings.paneWidth = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(0f, y, Mathf.Min(width, 320f), 22f), Settings.paneWidth, 420f, 720f, middleAlignment: true));
			y += 26f;
			Rect rect2 = new Rect(0f, y, width, 23f);
			RadiusFont.Label(rect2, "RUI.Inspector.PaneHeight".Translate(Settings.paneHeight), GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			TooltipHandler.TipRegion(rect2, "RUI.Inspector.PaneHeightTip".Translate());
			y += 23f;
			Settings.paneHeight = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(0f, y, Mathf.Min(width, 320f), 22f), Settings.paneHeight, 165f, 800f, middleAlignment: true));
			y += 26f;
			Rect rectStorage = new Rect(0f, y, width, 23f);
			RadiusFont.Label(rectStorage, "RUI.Inspector.StorageHeight".Translate(Settings.storagePanelHeight), GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			TooltipHandler.TipRegion(rectStorage, "RUI.Inspector.StorageHeightTip".Translate());
			y += 23f;
			Settings.storagePanelHeight = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(0f, y, Mathf.Min(width, 320f), 22f), Settings.storagePanelHeight, 250f, 900f, middleAlignment: true));
			y += 26f;
			CheckRow(ref y, width, "RUI.Inspector.ShowVanillaTabs".Translate(), ref Settings.showVanillaTabs, "RUI.Inspector.ShowVanillaTabsTip".Translate());
			RadiusFont.Label(new Rect(0f, y + 6f, width, 44f), "RUI.Inspector.LayoutHint".Translate(), GameFont.Small, heading: false, Palette.TextDim);
			y += 54f;
			Rect rect3 = new Rect(0f, y, 200f, 30f);
			bool flag = Mouse.IsOver(rect3);
			CardChrome.Rounded(rect3, flag ? Palette.Surface3 : Palette.Surface2, 8f);
			RadiusFont.Label(rect3, "RUI.Inspector.Reset".Translate(), GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleCenter, wrap: false);
			if (Widgets.ButtonInvisible(rect3))
			{
				Settings.ResetToDefaults();
			}
			y += 38f;
			// Useless Filter's own settings take over while it runs; ours apply to the filters taken from it.
			if (!ModCompat.IsActive("Mlie.UselessFilter"))
			{
				DrawUselessRules(ref y, width);
			}
			settingsContentH = Mathf.Max(y, outRect.height);
		}
		finally
		{
			FlatScroll.EndOrUnwind(depth);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			Text.WordWrap = true;
			GUI.color = Color.white;
		}
	}

	private static void DrawUselessRules(ref float y, float width)
	{
		Heading(ref y, width, "RUI.Inspector.SectionUseless".Translate());
		RadiusFont.Label(new Rect(0f, y, width, 44f), "RUI.Inspector.UselessHint".Translate(), GameFont.Small, heading: false, Palette.TextDim);
		y += 48f;
		float w = Mathf.Min(width, 340f);
		Widgets.FloatRange(new Rect(0f, y, w, 30f), 5309620, ref Settings.uselessHitPoints, 0f, 1f, "HitPoints", ToStringStyle.PercentZero);
		y += 34f;
		Widgets.QualityRange(new Rect(0f, y, w, 30f), 5309621, ref Settings.uselessQuality);
		y += 34f;
		CheckRow(ref y, width, "RUI.Inspector.UselessTainted".Translate(), ref Settings.uselessTainted, null);
		CheckRow(ref y, width, "RUI.Inspector.UselessClean".Translate(), ref Settings.uselessClean, null);
		CheckRow(ref y, width, "RUI.Inspector.UselessBiocoded".Translate(), ref Settings.uselessBiocoded, "RUI.Inspector.UselessBiocodedTip".Translate());
		CheckRow(ref y, width, "RUI.Inspector.UselessNonBiocoded".Translate(), ref Settings.uselessNonBiocoded, null);
		y += 4f;
		Rect rect = new Rect(0f, y, 200f, 30f);
		CardChrome.Rounded(rect, Mouse.IsOver(rect) ? Palette.Surface3 : Palette.Surface2, 8f);
		RadiusFont.Label(rect, "RUI.Inspector.UselessReset".Translate(), GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleCenter, wrap: false);
		if (Widgets.ButtonInvisible(rect))
		{
			Settings.ResetUselessRules();
		}
		// Shown while Useless Filter's own settings file is still there.
		if (uselessFile == null)
		{
			uselessFile = UselessSettingsImport.FindFile() ?? "";
		}
		if (uselessFile.Length > 0)
		{
			Rect rect2 = new Rect(rect.xMax + 8f, y, 260f, 30f);
			CardChrome.Rounded(rect2, Mouse.IsOver(rect2) ? Palette.Surface3 : Palette.Surface2, 8f);
			RadiusFont.Label(rect2, "RUI.Inspector.UselessImport".Translate(), GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleCenter, wrap: false);
			TooltipHandler.TipRegion(rect2, "RUI.Inspector.UselessImportTip".Translate(uselessFile));
			if (Widgets.ButtonInvisible(rect2))
			{
				Messages.Message((UselessSettingsImport.TryImport() ? "RUI.Inspector.UselessImported" : "RUI.Inspector.UselessImportFailed").Translate(), MessageTypeDefOf.NeutralEvent, historical: false);
			}
		}
		y += 38f;
	}

	private static void Heading(ref float y, float w, string label)
	{
		y += 8f;
		RadiusFont.Label(new Rect(0f, y, w, 24f), label, GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		y += 26f;
		CardChrome.Fill(new Rect(0f, y, w, 1f), Palette.Border);
		y += 6f;
	}

	private static void CheckRow(ref float y, float w, string label, ref bool value, string? tip)
	{
		Rect rect = new Rect(0f, y, Mathf.Min(w, 340f), 25f);
		if (Mouse.IsOver(rect))
		{
			CardChrome.Hover(rect, 8f);
		}
		if (!tip.NullOrEmpty())
		{
			TooltipHandler.TipRegion(rect, tip);
		}
		GUI.color = Palette.Ink;
		Widgets.CheckboxLabeled(rect, label, ref value);
		GUI.color = Color.white;
		y += rect.height + 2f;
	}
}
