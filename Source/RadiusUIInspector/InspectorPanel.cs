using System;
using System.Collections;
using System.Collections.Generic;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RadiusUIInspector;

public static class InspectorPanel
{
	internal enum CellKind
	{
		Mood,
		Need,
		Skill,
		SkillsGrid,
		Health,
		HealthBar,
		Equip,
		Armor,
		Header,
		Spacer,
		Readout,
		Area,
		Timetable,
		Policies,
		Medical,
		Inspect,
		MechEnergy,
		Production,
		Portrait,
		NameBlock,
		Activity,
		WorkMode,
		Resistance,
		Training,
		Master,
		Faction,
		GenderAge,
		Traits,
		Carrying,
		Queued,
		Mental,
		Stat,
		Capacity,
		Record,
		HP,
		Fuel,
		Growth,
		Chips,
		Freshness,
		Battery,
		PowerGrid,
		PipeNet
	}

	internal struct CellSpec
	{
		public CellKind Kind;

		public string Arg;

		public string Display;

		public NeedDef? NeedDef;

		public SkillDef? SkillDef;

		public StatDef? StatDef;

		public PawnCapacityDef? CapDef;

		public RecordDef? RecordDef;
	}

	internal sealed class ParsedLayout
	{
		public readonly List<List<CellSpec>> Rows = new List<List<CellSpec>>();

		public readonly List<int> RowStart = new List<int>(24);

		public bool[] Vis = new bool[32];

		public int Stamp = -1;
	}

	internal struct TabEntry
	{
		public string Key;

		public string Label;
	}

	private struct EdgeTab
	{
		public InspectTabBase? Tab;

		public string Key;

		public string Label;

		public bool Active;

		public int Badge;
	}

	private static readonly PawnSnapshot Snap = new PawnSnapshot();

	private static readonly MultiSnapshot Multi = new MultiSnapshot();

	private static Vector2 scroll;

	private static float lastViewH = 300f;

	private static Vector2 multiScroll;

	private static float lastContentH = 340f;

	private static int lastPawnId = -1;

	private static int lastMultiHash;

	private const float Margin = 6f;

	private const float Pad = 12f;

	private const float BarH = 15f;

	private const float CellRowH = 22f;

	private const float RPad = 10f;

	private const float RailPad = 10f;

	private const float EyebrowH = 20f;

	internal const float ScrollReserve = 20f;

	private const float ReadoutH = 78f;

	private static readonly Vector2 PortraitSizeHeader = new Vector2(48f, 78.26087f);

	private static readonly Vector2 PortraitSizeStrip = new Vector2(38f, 61.95652f);

	private const float PortraitCropShift = 0.07f;

	private static readonly Rect PortraitCrop = new Rect(0f, 19f / 60f, 1f, 46f / 75f);

	private static readonly Vector3 PortraitCameraOffset = new Vector3(0f, 0f, 0.3f);

	private const float PortraitZoom = 1.42f;

	private static readonly Rect FullCrop = new Rect(0f, 0f, 1f, 1f);

	private const float AnimalZoom = 1f;

	private static readonly ParsedLayout[] BodyLs = new ParsedLayout[4]
	{
		new ParsedLayout(),
		new ParsedLayout(),
		new ParsedLayout(),
		new ParsedLayout()
	};

	private static readonly ParsedLayout[] RailLs = new ParsedLayout[4]
	{
		new ParsedLayout(),
		new ParsedLayout(),
		new ParsedLayout(),
		new ParsedLayout()
	};

	private static bool cullEnabled = true;

	private static readonly List<TabEntry> knownTabs = new List<TabEntry>(12);

	private static readonly string TitleOverview = "RUI.Inspector.Overview".Translate();

	private static readonly string TitleNeedsWord = "RUI.Inspector.Needs".Translate();

	private static readonly string MoodWord = "RUI.Inspector.Mood".Translate();

	private static readonly string HealthyLabel = "RUI.Inspector.Healthy".Translate();

	private static readonly string UntendedChip = "RUI.Inspector.Untended".Translate();

	private static readonly string SettingsTip = "RUI.Inspector.SettingsTip".Translate();

	private static readonly string InfoTip = "RUI.Inspector.InfoTip".Translate();

	private static readonly string PinTip = "RUI.Inspector.PinTip".Translate();

	private static readonly string UnpinTip = "RUI.Inspector.UnpinTip".Translate();

	private static readonly string MinimizeTip = "RUI.Inspector.MinimizeTip".Translate();

	private static readonly string ExpandTip = "RUI.Inspector.ExpandTip".Translate();

	private static readonly string DropLabel = "RUI.Inspector.Drop".Translate();

	private static readonly string FocusTip = "RUI.Inspector.FocusTip".Translate();

	private static readonly string FocusTipThing = "RUI.Inspector.FocusTipThing".Translate();

	internal static readonly string SecOptions = "RUI.Inspector.SectionOptions".Translate();

	internal static readonly string SecPortrait = "RUI.Inspector.SectionPortrait".Translate();

	internal static readonly string SecTabs = "RUI.Inspector.SectionTabs".Translate();

	internal static readonly string SecRail = "RUI.Inspector.SectionRail".Translate();

	internal static readonly string PortraitS = "RUI.Inspector.PortraitSmall".Translate();

	internal static readonly string PortraitM = "RUI.Inspector.PortraitMedium".Translate();

	internal static readonly string PortraitL = "RUI.Inspector.PortraitLarge".Translate();

	internal static readonly string LblReset = "RUI.Inspector.Reset".Translate();

	internal static readonly string AllSettingsLbl = "RUI.Inspector.AllSettings".Translate();

	internal static readonly string LblMoodTicks = "RUI.Inspector.MoodTicks".Translate();

	internal static readonly string LblRows = "RUI.Inspector.Rows".Translate();

	internal static readonly string LblBands = "RUI.Inspector.HealthBands".Translate();

	internal static readonly string LblHideZero = "RUI.Inspector.SkillsHideZero".Translate();

	private static readonly string WHeader = "RUI.Inspector.WHeader".Translate();

	private static readonly string WSpacer = "RUI.Inspector.WSpacer".Translate();

	private static readonly string WMood = "RUI.Inspector.WMood".Translate();

	private static readonly string WHealth = "RUI.Inspector.WHealth".Translate();

	private static readonly string WSkillsGrid = "RUI.Inspector.WSkillsGrid".Translate();

	private static readonly string WEquip = "RUI.Inspector.WEquip".Translate();

	private static readonly string WArmor = "RUI.Inspector.WArmor".Translate();

	private static readonly string WReadout = "RUI.Inspector.WReadout".Translate();

	private static readonly string JumpNeeds = "RUI.Inspector.JumpNeeds".Translate();

	private static readonly string JumpSkills = "RUI.Inspector.JumpSkills".Translate();

	private static readonly string WArea = "RUI.Inspector.WArea".Translate();

	private static readonly string WTimetable = "RUI.Inspector.WTimetable".Translate();

	private static readonly string WPolicies = "RUI.Inspector.WPolicies".Translate();

	private static readonly string WMedical = "RUI.Inspector.WMedical".Translate();

	private static readonly string WInspect = "RUI.Inspector.WInspect".Translate();

	private static readonly string WMechEnergy = "RUI.Inspector.WMechEnergy".Translate();

	private static readonly string WHealthBar = "RUI.Inspector.WHealthBar".Translate();

	private static readonly string WProduction = "RUI.Inspector.WProduction".Translate();

	private static readonly string WPortrait = "RUI.Inspector.WPortrait".Translate();

	private static readonly string WName = "RUI.Inspector.WName".Translate();

	private static readonly string WActivity = "RUI.Inspector.WActivity".Translate();

	private static readonly string JumpHealth = "RUI.Inspector.JumpHealth".Translate();

	private static readonly string WWorkMode = "RUI.Inspector.WWorkMode".Translate();

	private static readonly string WResistance = "RUI.Inspector.WResistance".Translate();

	private static readonly string WTraining = "RUI.Inspector.WTraining".Translate();

	private static readonly string WMaster = "RUI.Inspector.WMaster".Translate();

	private static readonly string WHP = "RUI.Inspector.HitPoints".Translate();

	private static readonly string WFuel = "RUI.Inspector.Fuel".Translate();

	private static readonly string WGrowth = "RUI.Inspector.Growth".Translate();

	private static readonly string WChips = "RUI.Inspector.WChips".Translate();

	private static readonly string WFreshness = "RUI.Inspector.WFreshness".Translate();

	private static readonly string WBattery = "RUI.Inspector.WBattery".Translate();

	private static readonly string WPowerGrid = "RUI.Inspector.WPowerGrid".Translate();

	private static readonly string WPipeNet = "RUI.Inspector.WPipeNet".Translate();

	private static readonly string WFaction = "RUI.Inspector.WFaction".Translate();

	private static readonly string WGenderAge = "RUI.Inspector.WGenderAge".Translate();

	private static readonly string WTraits = "RUI.Inspector.WTraits".Translate();

	private static readonly string WCarrying = "RUI.Inspector.WCarrying".Translate();

	private static readonly string WQueued = "RUI.Inspector.WQueued".Translate();

	private static readonly string WMental = "RUI.Inspector.WMental".Translate();

	private static readonly string StatWord = "RUI.Inspector.StatWord".Translate();

	private static readonly string CapacityWord = "RUI.Inspector.CapacityWord".Translate();

	private static readonly string RecordWord = "RUI.Inspector.RecordWord".Translate();

	private static readonly string StatPickLbl = "RUI.Inspector.StatPick".Translate();

	private static readonly string CapacityPickLbl = "RUI.Inspector.CapacityPick".Translate();

	private static readonly string RecordPickLbl = "RUI.Inspector.RecordPick".Translate();

	private static readonly Dictionary<string, string> truncCache = new Dictionary<string, string>();

	private static float lastRailH = 300f;

	private static readonly List<EdgeTab> stripTabs = new List<EdgeTab>(12);

	private static readonly List<EdgeTab> stripPool = new List<EdgeTab>(12);

	private static readonly List<float> stripW = new List<float>(12);

	private static readonly List<float> stripX = new List<float>(12);

	private const float TabOverlap = 8f;

	private const float PillH = 19f;

	private const float PillRowPitch = 23f;

	private static readonly HashSet<string> PawnStatCategories = new HashSet<string> { "Basics", "BasicsImportant", "BasicsPawn", "BasicsPawnImportant", "PawnCombat", "PawnSocial", "PawnMisc", "PawnWork", "Mechanitor" };

	internal static float BodyContentH => lastContentH;

	internal static IReadOnlyList<TabEntry> KnownTabs => knownTabs;

	internal static int BadgeUntended => Snap.Untended;

	internal static string BadgeUntendedStr => Snap.UntendedCountStr;

	internal static float RailContentH => lastRailH;

	public static void DrawPane(Rect inRect, Pawn? pawn, IInspectPane pane)
	{
		try
		{
			if (InspectorCore.Mode == PaneMode.Multi)
			{
				DrawMulti(inRect);
			}
			else if (pawn != null)
			{
				Snap.Refresh(pawn);
				if (pawn.thingIDNumber != lastPawnId)
				{
					lastPawnId = pawn.thingIDNumber;
					scroll = Vector2.zero;
				}
				InspectorCore.TickMinimize();
				if (InspectorCore.Minimized)
				{
					DrawCompact(inRect, pawn);
				}
				else
				{
					DrawFull(inRect, pawn, pane);
				}
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

	internal static void DrawThingIconOrInitial(Rect r, Thing thing, string initial)
	{
		if (!FrameGate.Drawing)
		{
			return;
		}
		try
		{
			GUI.color = Color.white;
			Widgets.ThingIcon(r, thing);
			GUI.color = Color.white;
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] ThingIcon draw failed: " + ex.Message, 5351399);
			RadiusFont.Label(r, initial, GameFont.Small, heading: true, RadiusTheme.Accent, TextAnchor.MiddleCenter, wrap: false);
		}
	}

	private static void DrawPortrait(Rect r, Pawn pawn, string initial, Vector2 renderSize)
	{
		if (!FrameGate.Drawing)
		{
			return;
		}
		try
		{
			bool num = pawn.RaceProps == null || pawn.RaceProps.Humanlike;
			Vector3 vector = PortraitCameraOffset;
			float num2 = 1.42f;
			Rect texCoords = PortraitCrop;
			if (!num)
			{
				texCoords = FullCrop;
				vector = Vector3.zero;
				float b = Mathf.Clamp(pawn.BodySize, 0.2f, 4f);
				num2 = 1f / Mathf.Sqrt(Mathf.Max(1f, b));
			}
			InspectorSettings settings = InspectorMod.Settings;
			if (settings != null)
			{
				num2 *= settings.portraitZoom;
				vector += new Vector3(settings.portraitPanX, 0f, settings.portraitPanY);
			}
			Rot4 south = Rot4.South;
			Vector3 cameraOffset = vector;
			float cameraZoom = num2;
			PawnHealthState? healthStateOverride = PawnHealthState.Mobile;
			RenderTexture image = PortraitsCache.Get(pawn, renderSize, south, cameraOffset, cameraZoom, supersample: true, compensateForUIScale: true, renderHeadgear: true, renderClothes: true, null, null, stylingStation: false, healthStateOverride);
			GUI.color = Color.white;
			GUI.DrawTextureWithTexCoords(r, image, texCoords);
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Portrait render failed: " + ex.Message, 5351400);
			CardChrome.Rounded(r, Palette.Surface2, 8f);
			RadiusFont.Label(r, initial, GameFont.Small, heading: true, RadiusTheme.Accent, TextAnchor.MiddleCenter, wrap: false);
		}
	}

	private static void DrawMulti(Rect inRect)
	{
		Selector selector = Find.Selector;
		Multi.Refresh(selector.SelectedObjects);
		if (Multi.SelHash != lastMultiHash)
		{
			lastMultiHash = Multi.SelHash;
			multiScroll = Vector2.zero;
		}
		Rect rect = inRect.ContractedBy(6f);
		CardChrome.Card(rect);
		Rect outRect = rect.ContractedBy(6f);
		if (!Multi.SummaryLine.NullOrEmpty())
		{
			Rect rect2 = new Rect(outRect.x + 4f, outRect.y, outRect.width - 8f, 18f);
			RadiusFont.Label(rect2, Multi.SummaryLine.Truncate(rect2.width, truncCache), GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			TooltipHandler.TipRegion(rect2, Multi.SummaryLine);
			outRect.yMin += 20f;
		}
		float num = 50f;
		float num2 = (float)Multi.Entries.Count * num - 4f;
		float width = Mathf.Floor(outRect.width - ((num2 > outRect.height) ? 20f : 0f));
		Rect viewRect = new Rect(0f, 0f, width, Mathf.Max(num2, outRect.height));
		int depth = FlatScroll.Depth;
		FlatScroll.Begin(outRect, ref multiScroll, viewRect);
		try
		{
			float num3 = 0f;
			for (int i = 0; i < Multi.Entries.Count; i++)
			{
				MultiEntry multiEntry = Multi.Entries[i];
				Rect rect3 = new Rect(0f, num3, width, 46f);
				bool flag = Mouse.IsOver(rect3);
				if (!ViewCull.Visible(num3, 46f, multiScroll.y, outRect.height))
				{
					if (Widgets.ButtonInvisible(rect3))
					{
						Thing thing = multiEntry.Thing;
						selector.ClearSelection();
						selector.Select(thing);
						InspectorCore.Poke();
					}
					num3 += num;
					continue;
				}
				CardChrome.Rounded(rect3, flag ? Palette.Surface2 : Palette.Surface, 8f);
				Rect rect4 = new Rect(rect3.x + 5f, rect3.y + 4f, 38f, 38f);
				if (multiEntry.Pawn != null)
				{
					DrawPortrait(rect4, multiEntry.Pawn, multiEntry.Initial, PortraitSizeStrip);
				}
				else
				{
					CardChrome.Rounded(rect4, Palette.Surface2, 8f);
					DrawThingIconOrInitial(rect4.ContractedBy(3f), multiEntry.Thing, multiEntry.Initial);
				}
				float num4 = rect4.xMax + 8f;
				float num5 = ((multiEntry.Pawn != null) ? 140f : 240f);
				RadiusFont.Label(new Rect(num4, rect3.y, num5, rect3.height), multiEntry.Name, GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
				num4 += num5 + 4f;
				if (multiEntry.Pawn != null && multiEntry.HasMood && multiEntry.Pawn.needs?.mood != null)
				{
					float curLevelPercentage = multiEntry.Pawn.needs.mood.CurLevelPercentage;
					RadiusFont.Label(new Rect(num4, rect3.y, 52f, rect3.height), multiEntry.MoodStr, GameFont.Small, heading: false, MoodColor(multiEntry.Pawn, curLevelPercentage), TextAnchor.MiddleLeft, wrap: false);
					num4 += 56f;
				}
				if (multiEntry.Untended > 0)
				{
					Rect rect5 = new Rect(num4, rect3.y + (rect3.height - 18f) * 0.5f, 26f, 18f);
					CardChrome.Pill(rect5, Palette.Warn);
					RadiusFont.Label(rect5, multiEntry.UntendedStr, GameFont.Small, heading: false, Palette.InkOnAccent, TextAnchor.MiddleCenter, wrap: false);
				}
				IconSet.Get("Action/Popout").Draw(new Rect(rect3.xMax - 22f, rect3.y + (rect3.height - 12f) * 0.5f, 12f, 12f), flag ? RadiusTheme.Accent : Palette.TextDim);
				TooltipHandler.TipRegion(rect3, (multiEntry.Pawn != null) ? FocusTip : FocusTipThing);
				if (Widgets.ButtonInvisible(rect3))
				{
					Thing thing2 = multiEntry.Thing;
					selector.ClearSelection();
					selector.Select(thing2);
					InspectorCore.Poke();
				}
				num3 += num;
			}
		}
		finally
		{
			FlatScroll.EndOrUnwind(depth);
		}
	}

	private static void DrawCompact(Rect inRect, Pawn pawn)
	{
		Rect rect = inRect.ContractedBy(6f);
		bool flag = Mouse.IsOver(rect);
		CardChrome.Card(rect, flag);
		Rect r = new Rect(rect.x + 5f, rect.y + (rect.height - 38f) * 0.5f - 1f, 38f, 38f);
		DrawPortrait(r, pawn, Snap.Initial, PortraitSizeStrip);
		float num = r.xMax + 8f;
		RadiusFont.Label(new Rect(num, rect.y, 130f, rect.height), Snap.NameShort, GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		num += 134f;
		if (Snap.HasMood && pawn.needs?.mood != null)
		{
			float curLevelPercentage = pawn.needs.mood.CurLevelPercentage;
			RadiusFont.Label(new Rect(num, rect.y, 52f, rect.height), Snap.MoodPctStr, GameFont.Small, heading: false, MoodColor(pawn, curLevelPercentage), TextAnchor.MiddleLeft, wrap: false);
			num += 56f;
		}
		if (Snap.Untended > 0)
		{
			Rect rect2 = new Rect(num, rect.y + (rect.height - 18f) * 0.5f, 26f, 18f);
			CardChrome.Pill(rect2, Palette.Warn);
			RadiusFont.Label(rect2, Snap.UntendedCountStr, GameFont.Small, heading: false, Palette.InkOnAccent, TextAnchor.MiddleCenter, wrap: false);
		}
		IconSet.Get("Action/Popout").Draw(new Rect(rect.xMax - 24f, rect.y + (rect.height - 14f) * 0.5f, 14f, 14f), flag ? RadiusTheme.Accent : Palette.TextDim);
		TooltipHandler.TipRegion(rect, ExpandTip);
		if (Widgets.ButtonInvisible(rect))
		{
			InspectorCore.Poke();
		}
	}

	internal static bool DrawNativeTab(Rect body, IInspectPane pane)
	{
		switch (InspectorCore.NativeKind)
		{
		case NativeTabKind.Storage:
		{
			IStoreSettingsParent storeSettingsParent = StoragePanel.Resolve();
			if (storeSettingsParent == null)
			{
				return false;
			}
			StoragePanel.Draw(body, storeSettingsParent);
			return true;
		}
		case NativeTabKind.Contents:
			ContentsPanel.Draw(body, pane);
			return true;
		default:
			return false;
		}
	}

	internal static void CardAroundHostedTab(Rect card)
	{
		Rect rect = InspectorCore.HostedCardHole();
		float num = 12f;
		Rect rect2 = new Rect(card.x, card.y, card.width, Mathf.Max(0f, rect.y - card.y));
		if (rect2.height > 0f)
		{
			CardChrome.RoundedCorners(rect2, Palette.Surface, num, num, 0f, 0f);
		}
		Rect rect3 = new Rect(card.x, rect.y, Mathf.Max(0f, rect.x - card.x), rect.height);
		if (rect3.width > 0f)
		{
			CardChrome.Fill(rect3, Palette.Surface0);
		}
		Rect rect4 = new Rect(rect.xMax, rect.y, Mathf.Max(0f, card.xMax - rect.xMax), rect.height);
		if (rect4.width > 0f)
		{
			CardChrome.Fill(rect4, Palette.Surface0);
		}
		Rect rect5 = new Rect(card.x, rect.yMax, card.width, Mathf.Max(0f, card.yMax - rect.yMax));
		if (rect5.height > 0f)
		{
			CardChrome.RoundedCorners(rect5, Palette.Surface0, 0f, 0f, num, num);
		}
		CardChrome.Outline(card, Palette.Border, 1f, num);
	}

	private static void DrawFull(Rect inRect, Pawn pawn, IInspectPane pane)
	{
		float num = 26f;
		Rect rect = new Rect(inRect.x + 6f, inRect.y + num, inRect.width - 12f, inRect.height - num - 6f);
		if (InspectorCore.HostingTab && TabPlumbing.HostedTabPainted)
		{
			CardAroundHostedTab(rect);
		}
		else
		{
			CardChrome.Card(rect);
		}
		if (Mouse.IsOver(rect))
		{
			InspectorCore.PokeSoft();
		}
		DrawEdgeTabs(inRect, rect, pane);
		bool flag = InspectorCore.HostingTab || InspectorCore.NativeTabOpen;
		float railWidth = InspectorCore.RailWidth;
		if (!flag)
		{
			Rect rect2 = new Rect(rect.x, rect.y, railWidth, rect.height);
			CardChrome.RoundedCorners(rect2, Palette.Surface0, 12f, 0f, 0f, 12f);
			CardChrome.Fill(new Rect(rect.x + railWidth, rect.y, 1f, rect.height), Palette.Border);
			DrawRail(pawn, pane, rect2);
		}
		float num2 = (flag ? (rect.x + 12f) : (rect.x + railWidth + 10f));
		float num3 = rect.xMax - (flag ? 12f : 10f) - num2;
		float y = rect.y + 8f;
		float y2 = y;
		float num4 = DrawHeaderButtons(pawn, pane, num2, num3, ref y);
		y += 4f;
		if (flag)
		{
			float num5 = num4 - 8f - num2;
			if (num5 > 70f)
			{
				Rect rect3 = new Rect(num2, y2, 24f, 24f);
				CardChrome.Rounded(rect3.ExpandedBy(1f), Palette.Surface2, 6f);
				DrawPortrait(rect3, pawn, Snap.Initial, new Vector2(24f, 39.130436f));
				float num6 = rect3.xMax + 8f;
				float num7 = Mathf.Min(RadiusFont.Size(Snap.NameShort, GameFont.Small, heading: true).x + 4f, num5 - 32f);
				RadiusFont.Label(new Rect(num6, y2, num7, 24f), Snap.NameShort, GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
				float num8 = num6 + num7 + 8f;
				float num9 = num2 + num5 - num8;
				if (num9 > 40f)
				{
					bool bleedingOut = Snap.BleedingOut;
					RadiusFont.Label(new Rect(num8, y2, num9, 24f), (bleedingOut ? Snap.BleedLabel : Snap.SubLine).Truncate(num9, truncCache), GameFont.Small, bleedingOut, bleedingOut ? Palette.Bad : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
				}
			}
		}
		if (pane.OpenTabType != null)
		{
			InspectorCore.PokeSoft();
		}
		if ((!InspectorCore.NativeTabOpen || !DrawNativeTab(new Rect(num2, y, num3, rect.yMax - y - 8f), pane)) && !InspectorCore.HostingTab)
		{
			Rect outRect = new Rect(num2, y, num3, rect.yMax - y - 8f);
			lastViewH = outRect.height;
			float num10 = Mathf.Floor(outRect.width - 20f);
			Rect viewRect = new Rect(0f, 0f, num10, Mathf.Max(lastContentH, outRect.height));
			int depth = FlatScroll.Depth;
			FlatScroll.Begin(outRect, ref scroll, viewRect);
			try
			{
				float y3 = 0f;
				PawnProfileKind pawnProfileKind = InspectorCore.ProfileOf(pawn);
				DrawLayoutRows(pawn, pane, num10, ref y3, BodyLs[(int)pawnProfileKind], InspectorMod.Settings.EffectiveLayoutFor(pawnProfileKind));
				lastContentH = Mathf.Max(y3, 10f);
			}
			finally
			{
				FlatScroll.EndOrUnwind(depth);
			}
			if (Snap.BleedingOut)
			{
				DrawDeathBand(rect);
			}
		}
	}

	private static void DrawDeathBand(Rect card)
	{
		Rect rect = new Rect(card.x, card.yMax - 24f, card.width, 24f);
		CardChrome.RoundedCorners(rect, Palette.Bad, 0f, 0f, 12f, 12f);
		IconSet.Get("Medical/Bleed").Draw(new Rect(rect.x + 10f, rect.y + 4f, 16f, 16f), Palette.InkOnAccent);
		RadiusFont.Label(new Rect(rect.x + 32f, rect.y, rect.width - 40f, 24f), Snap.BleedLabel, GameFont.Small, heading: true, Palette.InkOnAccent, TextAnchor.MiddleLeft, wrap: false);
	}

	private static float DrawHeaderButtons(Pawn pawn, IInspectPane pane, float rx, float rw, ref float y)
	{
		float bx = VanillaChrome.Draw(pane, rx + rw, y, rw) - 28f;
		if (HeaderButton(ref bx, y, "Action/Settings", SettingsTip, active: false))
		{
			if (pane.OpenTabType != null)
			{
				pane.CloseOpenTab();
			}
			Find.WindowStack.Add(new Dialog_LayoutEditor());
		}
		if (HeaderButton(ref bx, y, "Action/Dock", MinimizeTip, active: false))
		{
			if (pane.OpenTabType != null)
			{
				pane.CloseOpenTab();
			}
			InspectorCore.Minimized = true;
		}
		if (HeaderButton(ref bx, y, "Action/Pin", InspectorCore.Pinned ? UnpinTip : PinTip, InspectorCore.Pinned))
		{
			InspectorCore.Pinned = !InspectorCore.Pinned;
		}
		if (ModsConfig.IdeologyActive && pawn.Ideo != null && HeaderArtButton(ref bx, y, pawn.Ideo.Icon, pawn.Ideo.Color, pawn.Ideo.name))
		{
			IdeoUIUtility.OpenIdeoInfo(pawn.Ideo);
		}
		if (pawn.Faction != null && HeaderArtButton(ref bx, y, pawn.Faction.def.FactionIcon, pawn.Faction.Color, pawn.Faction.Name))
		{
			MainButtonDef namedSilentFail = DefDatabase<MainButtonDef>.GetNamedSilentFail("Factions");
			if (namedSilentFail != null)
			{
				Find.MainTabsRoot.SetCurrentTab(namedSilentFail);
			}
		}
		y += 28f;
		return bx + 28f;
	}

	internal static bool HeaderButton(ref float bx, float y, string icon, string tip, bool active)
	{
		Rect rect = new Rect(bx, y, 24f, 24f);
		bool flag = Mouse.IsOver(rect);
		if (active)
		{
			CardChrome.Rounded(rect, RadiusTheme.Accent, 8f);
		}
		else if (flag)
		{
			CardChrome.Hover(rect, 8f);
		}
		IconNorm.Draw(icon, rect.ContractedBy(4f), active ? Palette.InkOnAccent : (flag ? RadiusTheme.Accent : Palette.TextDim));
		TooltipHandler.TipRegion(rect, tip);
		bool num = Widgets.ButtonInvisible(rect);
		if (num)
		{
			InspectorCore.Poke();
		}
		bx -= 28f;
		return num;
	}

	internal static bool HeaderArtButton(ref float bx, float y, Texture art, Color tint, string tip)
	{
		Rect rect = new Rect(bx, y, 24f, 24f);
		if (Mouse.IsOver(rect))
		{
			CardChrome.Hover(rect, 8f);
		}
		IconNorm.DrawTexture(art as Texture2D, rect.ContractedBy(4f), tint);
		TooltipHandler.TipRegion(rect, tip);
		bool num = Widgets.ButtonInvisible(rect);
		if (num)
		{
			InspectorCore.Poke();
		}
		bx -= 28f;
		return num;
	}

	private static void DrawRail(Pawn pawn, IInspectPane pane, Rect rail)
	{
		InspectorSettings settings = InspectorMod.Settings;
		float x = rail.x + 10f;
		float vw = rail.width - 20f;
		float y = rail.y + 10f;
		cullEnabled = false;
		try
		{
			PawnProfileKind pawnProfileKind = InspectorCore.ProfileOf(pawn);
			DrawLayoutRows(pawn, pane, vw, ref y, RailLs[(int)pawnProfileKind], settings.EffectiveRailFor(pawnProfileKind), x);
		}
		finally
		{
			cullEnabled = true;
		}
		lastRailH = Mathf.Max(y - rail.y, 60f);
	}

	private static float DrawPortraitCell(Pawn pawn, float x, float w, float y)
	{
		InspectorSettings settings = InspectorMod.Settings;
		float a = ((settings.portraitSize == 0) ? 48f : ((settings.portraitSize == 1) ? 84f : w));
		a = Mathf.Min(a, w);
		Rect rect = new Rect(x, y, a, a);
		Rect rect2 = rect.ExpandedBy(2f);
		CardChrome.Rounded(rect2, Palette.Surface2, 10f);
		DrawPortrait(rect, pawn, Snap.Initial, new Vector2(a, a * 75f / 46f));
		CardChrome.Outline(rect2, Palette.Border, 1f, 10f);
		return a + 2f;
	}

	private static float DrawNameCell(float x, float w, float y)
	{
		RadiusFont.Label(new Rect(x, y, w, 20f), Snap.NameShort, GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		TooltipHandler.TipRegion(new Rect(x, y, w, 20f), Snap.NameShort);
		return 20f;
	}

	private static float DrawLineCell(string text, Color ink, float x, float w, float y, Pawn? pawn = null, int tipId = 0, Func<Pawn, string?>? rich = null)
	{
		if (text.NullOrEmpty())
		{
			return 0f;
		}
		Rect rect = new Rect(x, y, w, 20f);
		RadiusFont.Label(rect, text.Truncate(w, truncCache), GameFont.Small, heading: false, ink, TextAnchor.MiddleLeft, wrap: false);
		if (rich == null)
		{
			TooltipHandler.TipRegion(rect, text);
		}
		else if (Mouse.IsOver(rect))
		{
			// The line is truncated: its full text leads the tooltip, then the detail.
			RichTips.Region(rect, pawn, tipId, delegate(Pawn p)
			{
				string text2 = rich(p);
				return text2.NullOrEmpty() ? text : (text + "\n\n" + text2);
			});
		}
		return 20f;
	}

	private static int FindValueLine(List<PawnSnapshot.ValueLine> lines, Def? def)
	{
		if (def == null)
		{
			return -1;
		}
		for (int i = 0; i < lines.Count; i++)
		{
			if (lines[i].Def == def)
			{
				return i;
			}
		}
		return -1;
	}

	private static float DrawValueRow(in PawnSnapshot.ValueLine line, float x, float w, float y)
	{
		Rect rect = new Rect(x, y, w, 20f);
		float num = Mathf.Min(RadiusFont.Size(line.Value).x, w * 0.5f);
		RadiusFont.Label(new Rect(x, y, w - num - 6f, 20f), line.Label.Truncate(w - num - 8f, truncCache), GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		RadiusFont.Label(rect, line.Value, GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleRight, wrap: false);
		TooltipHandler.TipRegion(rect, line.Tip);
		return 20f;
	}

	private static float DrawCapacityCell(Pawn pawn, in CellSpec spec, float x, float w, float y)
	{
		List<PawnSnapshot.CapLine> capacities = Snap.Capacities;
		for (int i = 0; i < capacities.Count; i++)
		{
			if (capacities[i].Def == spec.CapDef)
			{
				PawnSnapshot.CapLine capLine = capacities[i];
				Color col = ((capLine.Value >= 0.95f) ? Palette.Good : ((capLine.Value >= 0.5f) ? Palette.Warn : Palette.Bad));
				Rect bar = new Rect(x, y, w, 15f);
				SuiteBar(bar, Mathf.Clamp01(capLine.Value), col, -1f, -1f);
				BarInnerLabels(bar, capLine.Label, capLine.Pct, Mathf.Clamp01(capLine.Value));
				PawnCapacityDef capDef = capLine.Def;
				if (capDef != null && Mouse.IsOver(bar))
				{
					RichTips.Region(bar, pawn, RichTips.CapacityId(capDef), (Pawn p) => RichTips.Capacity(p, capDef));
				}
				return 15f;
			}
		}
		return 0f;
	}

	private static float DrawActivityCell(Pawn pawn, float x, float w, float y)
	{
		IconSet.Get("Action/Play").Draw(new Rect(x, y + 4f, 12f, 12f), Palette.TextDim);
		Rect rect = new Rect(x + 16f, y, w - 16f, 20f);
		RadiusFont.Label(rect, Snap.Activity.Truncate(rect.width, truncCache), GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		if (Mouse.IsOver(rect))
		{
			string activity = Snap.Activity;
			RichTips.Region(rect, pawn, RichTips.ActivityId, delegate(Pawn p)
			{
				string text = RichTips.Activity(p);
				return text.NullOrEmpty() ? activity : (activity + "\n\n" + text);
			});
		}
		return 20f;
	}

	private static void DrawEdgeTabs(Rect inRect, Rect card, IInspectPane pane)
	{
		float stripH = 26f;
		InspectorSettings settings = InspectorMod.Settings;
		stripPool.Clear();
		IEnumerable<InspectTabBase> curTabs = pane.CurTabs;
		if (curTabs != null)
		{
			foreach (InspectTabBase item in curTabs)
			{
				if (item != null && item.IsVisible && !item.Hidden)
				{
					stripPool.Add(new EdgeTab
					{
						Tab = item,
						Key = TabKey(item),
						Label = item.labelKey.Translate(),
						Active = (item.GetType() == pane.OpenTabType),
						Badge = ((item is ITab_Pawn_Health) ? Snap.Untended : 0)
					});
				}
			}
		}
		stripTabs.Clear();
		knownTabs.Clear();
		stripTabs.Add(new EdgeTab
		{
			Tab = null,
			Key = "",
			Label = TitleOverview,
			Active = (pane.OpenTabType == null),
			Badge = 0
		});
		for (int i = 0; i < settings.tabOrder.Count; i++)
		{
			for (int j = 0; j < stripPool.Count; j++)
			{
				if (!(stripPool[j].Key != settings.tabOrder[i]))
				{
					TakeStripTab(j, settings);
					break;
				}
			}
		}
		while (stripPool.Count > 0)
		{
			TakeStripTab(0, settings);
		}
		stripW.Clear();
		stripX.Clear();
		float num = 0f;
		for (int k = 0; k < stripTabs.Count; k++)
		{
			float num2 = RadiusFont.Size(stripTabs[k].Label, GameFont.Small, heading: true).x + 24f;
			if (stripTabs[k].Badge > 0)
			{
				num2 += 18f;
			}
			stripW.Add(num2);
			num += num2;
		}
		float num3 = card.width - 16f + 8f * (float)(stripTabs.Count - 1);
		float num4 = ((num > num3) ? (num3 / num) : 1f);
		float num5 = card.x + 8f;
		for (int l = 0; l < stripTabs.Count; l++)
		{
			stripW[l] = Mathf.Max(44f, Mathf.Floor(stripW[l] * num4));
			stripX.Add(num5);
			num5 += stripW[l] - 8f;
		}
		float y = inRect.y;
		int num6 = 0;
		for (int m = 0; m < stripTabs.Count; m++)
		{
			if (stripTabs[m].Active)
			{
				num6 = m;
			}
		}
		for (int num7 = stripTabs.Count - 1; num7 >= 0; num7--)
		{
			if (num7 != num6)
			{
				DrawEdgeTab(num7, y, stripH);
			}
		}
		DrawEdgeTab(num6, y, stripH);
		HandleEdgeTab(num6, y, stripH, pane);
		for (int n = 0; n < stripTabs.Count; n++)
		{
			if (n != num6)
			{
				HandleEdgeTab(n, y, stripH, pane);
			}
		}
	}

	private static void TakeStripTab(int i, InspectorSettings s)
	{
		EdgeTab item = stripPool[i];
		stripPool.RemoveAt(i);
		knownTabs.Add(new TabEntry
		{
			Key = item.Key,
			Label = item.Label
		});
		if (item.Active || !s.hiddenTabs.Contains(item.Key))
		{
			stripTabs.Add(item);
		}
	}

	private static void DrawEdgeTab(int i, float top, float stripH)
	{
		EdgeTab edgeTab = stripTabs[i];
		float num = stripW[i];
		float num2 = stripX[i];
		Rect rect = (edgeTab.Active ? new Rect(num2, top, num, stripH + 2f) : new Rect(num2, top + 5f, num, stripH - 5f));
		bool flag = !edgeTab.Active && Mouse.IsOver(new Rect(num2, top, num, stripH));
		CardChrome.RoundedCorners(rect, edgeTab.Active ? Palette.Surface : (flag ? Palette.Surface2 : Palette.Surface0), 8f, 8f, 0f, 0f);
		float num3 = ((edgeTab.Badge > 0) ? 20f : 0f);
		string text = edgeTab.Label.Truncate(num - num3 - 12f, truncCache);
		float x = RadiusFont.Size(text, GameFont.Small, edgeTab.Active).x;
		float num4 = num2 + Mathf.Max(6f, (num - x - num3) * 0.5f);
		if (edgeTab.Active)
		{
			float num5 = RadiusFont.LineH(GameFont.Small);
			RadiusFont.LabelBold(new Rect(num4, top + (stripH - num5) * 0.5f + 1f, x + 2f, num5), text, GameFont.Small, Color.white);
		}
		else
		{
			RadiusFont.Label(new Rect(num4, rect.y, x + 2f, rect.height), text, GameFont.Small, heading: false, flag ? Palette.Ink : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		}
		if (edgeTab.Badge > 0)
		{
			float num6 = (edgeTab.Active ? (top + stripH * 0.5f) : (rect.y + rect.height * 0.5f));
			Rect rect2 = new Rect(num4 + x + 4f, num6 - 7f, 16f, 14f);
			CardChrome.Pill(rect2, Palette.Warn);
			RadiusFont.Label(rect2, Snap.UntendedCountStr, GameFont.Small, heading: false, Palette.InkOnAccent, TextAnchor.MiddleCenter, wrap: false);
		}
	}

	private static void HandleEdgeTab(int i, float top, float stripH, IInspectPane pane)
	{
		EdgeTab edgeTab = stripTabs[i];
		if (!Widgets.ButtonInvisible(new Rect(stripX[i], top, stripW[i], stripH + 2f)))
		{
			return;
		}
		if (edgeTab.Tab == null)
		{
			if (pane.OpenTabType != null)
			{
				pane.CloseOpenTab();
			}
		}
		else
		{
			TabPlumbing.Toggle(edgeTab.Tab, pane);
		}
		InspectorCore.Poke();
	}

	private static float PillWidth(string label, int badge)
	{
		float num = RadiusFont.Size(label, GameFont.Small, heading: true).x + 16f;
		if (badge > 0)
		{
			num += 18f;
		}
		return num;
	}

	internal static float MeasurePillBlock(IInspectPane pane, float availWidth, int badgeCount)
	{
		int rows = 1;
		float x = 0f;
		AdvancePill(PillWidth(TitleOverview, 0), availWidth, ref x, ref rows);
		IEnumerable<InspectTabBase> curTabs = pane.CurTabs;
		if (curTabs != null)
		{
			if (curTabs is IList list)
			{
				for (int i = 0; i < list.Count; i++)
				{
					MeasureOne((InspectTabBase)list[i]);
				}
			}
			else
			{
				foreach (InspectTabBase item in curTabs)
				{
					MeasureOne(item);
				}
			}
		}
		return (float)rows * 23f + 4f;
		void MeasureOne(InspectTabBase? tab)
		{
			if (tab != null && tab.IsVisible && !tab.Hidden && (!(tab.GetType() != pane.OpenTabType) || !InspectorMod.Settings.hiddenTabs.Contains(TabKey(tab))))
			{
				int badge = ((tab is ITab_Pawn_Health) ? badgeCount : 0);
				AdvancePill(PillWidth(tab.labelKey.Translate(), badge), availWidth, ref x, ref rows);
			}
		}
	}

	private static void AdvancePill(float w, float availWidth, ref float x, ref int rows)
	{
		if (x > 0f && x + w > availWidth)
		{
			x = 0f;
			rows++;
		}
		x += w + 4f;
	}

	internal static void DrawTabPills(Rect card, IInspectPane pane, ref float y, int badgeCount, string badgeStr)
	{
		DrawTabPillsAt(card.x + 12f, card.width - 24f, pane, ref y, badgeCount, badgeStr);
	}

	internal static void DrawTabPillsAt(float left, float availWidth, IInspectPane pane, ref float y, int badgeCount, string badgeStr)
	{
		knownTabs.Clear();
		float x = 0f;
		int rows = 1;
		bool active = pane.OpenTabType == null;
		if (PillButton(left, y, ref x, ref rows, availWidth, TitleOverview, active, 0, "") && pane.OpenTabType != null)
		{
			pane.CloseOpenTab();
		}
		IEnumerable<InspectTabBase> curTabs = pane.CurTabs;
		if (curTabs != null)
		{
			if (curTabs is IList list)
			{
				for (int i = 0; i < list.Count; i++)
				{
					TabPill((InspectTabBase)list[i], pane, left, y, ref x, ref rows, availWidth, badgeCount, badgeStr);
				}
			}
			else
			{
				foreach (InspectTabBase item in curTabs)
				{
					TabPill(item, pane, left, y, ref x, ref rows, availWidth, badgeCount, badgeStr);
				}
			}
		}
		y += (float)rows * 23f + 4f;
	}

	internal static string TabKey(InspectTabBase t)
	{
		if (!t.labelKey.NullOrEmpty())
		{
			return t.labelKey;
		}
		return t.GetType().FullName;
	}

	private static void TabPill(InspectTabBase? tab, IInspectPane pane, float left, float y, ref float x, ref int rows, float availWidth, int badgeCount, string badgeStr)
	{
		if (tab == null || !tab.IsVisible || tab.Hidden)
		{
			return;
		}
		string label = tab.labelKey.Translate();
		bool flag = tab.GetType() == pane.OpenTabType;
		string text = TabKey(tab);
		knownTabs.Add(new TabEntry
		{
			Key = text,
			Label = label
		});
		if (flag || !InspectorMod.Settings.hiddenTabs.Contains(text))
		{
			int badge = ((tab is ITab_Pawn_Health) ? badgeCount : 0);
			if (PillButton(left, y, ref x, ref rows, availWidth, label, flag, badge, badgeStr))
			{
				TabPlumbing.Toggle(tab, pane);
			}
		}
	}

	private static bool PillButton(float left, float top, ref float x, ref int rows, float availWidth, string label, bool active, int badge, string badgeStr)
	{
		float num = PillWidth(label, badge);
		if (x > 0f && x + num > availWidth)
		{
			x = 0f;
			rows++;
		}
		float width = Mathf.Min(num, availWidth);
		Rect rect = new Rect(left + x, top + (float)(rows - 1) * 23f, width, 19f);
		bool flag = Mouse.IsOver(rect);
		CardChrome.Pill(rect, active ? RadiusTheme.Accent : (flag ? Palette.Surface3 : Palette.Surface2));
		RadiusFont.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height), label, GameFont.Small, active, active ? Palette.InkOnAccent : (flag ? Palette.Ink : Palette.TextDim), TextAnchor.MiddleLeft, wrap: false);
		if (badge > 0)
		{
			Rect rect2 = new Rect(rect.xMax - 22f, rect.y + 2f, 18f, 15f);
			CardChrome.Pill(rect2, active ? Palette.InkOnAccent : Palette.Warn);
			RadiusFont.Label(rect2, badgeStr, GameFont.Small, heading: false, active ? RadiusTheme.Accent : Palette.InkOnAccent, TextAnchor.MiddleCenter, wrap: false);
		}
		bool num2 = Widgets.ButtonInvisible(rect);
		if (num2)
		{
			InspectorCore.Poke();
		}
		x += num + 4f;
		return num2;
	}

	internal static float MeasurePillBlockForPawn(IInspectPane pane, float availWidth)
	{
		return MeasurePillBlock(pane, availWidth, Snap.Untended);
	}

	internal static void EnsureParsed(ParsedLayout pl, List<string> rows)
	{
		InspectorSettings settings = InspectorMod.Settings;
		if (pl.Stamp == settings.LayoutStamp)
		{
			return;
		}
		pl.Stamp = settings.LayoutStamp;
		pl.Rows.Clear();
		for (int i = 0; i < rows.Count; i++)
		{
			List<CellSpec> list = new List<CellSpec>(3);
			string[] array = rows[i].Split(new char[1] { '|' });
			for (int j = 0; j < array.Length; j++)
			{
				CellSpec item = ParseCell(array[j]);
				list.Add(item);
			}
			pl.Rows.Add(list);
		}
	}

	private static CellSpec ParseCell(string raw)
	{
		if (raw.StartsWith("need:"))
		{
			string text = raw.Substring(5);
			NeedDef namedSilentFail = DefDatabase<NeedDef>.GetNamedSilentFail(text);
			return new CellSpec
			{
				Kind = CellKind.Need,
				Arg = text,
				NeedDef = namedSilentFail,
				Display = ((namedSilentFail != null) ? ((string)namedSilentFail.LabelCap) : text)
			};
		}
		if (raw.StartsWith("skill:"))
		{
			string text2 = raw.Substring(6);
			SkillDef namedSilentFail2 = DefDatabase<SkillDef>.GetNamedSilentFail(text2);
			return new CellSpec
			{
				Kind = CellKind.Skill,
				Arg = text2,
				SkillDef = namedSilentFail2,
				Display = ((namedSilentFail2 != null) ? ((string)namedSilentFail2.LabelCap) : text2)
			};
		}
		if (raw.StartsWith("header:"))
		{
			string text3 = raw.Substring(7);
			return new CellSpec
			{
				Kind = CellKind.Header,
				Arg = text3,
				Display = text3
			};
		}
		if (raw.StartsWith("stat:"))
		{
			string text4 = raw.Substring(5);
			StatDef namedSilentFail3 = DefDatabase<StatDef>.GetNamedSilentFail(text4);
			LayoutDefRegistry.Register(namedSilentFail3);
			return new CellSpec
			{
				Kind = CellKind.Stat,
				Arg = text4,
				StatDef = namedSilentFail3,
				Display = StatWord + ": " + ((namedSilentFail3 != null) ? ((string)namedSilentFail3.LabelCap) : text4)
			};
		}
		if (raw.StartsWith("capacity:"))
		{
			string text5 = raw.Substring(9);
			PawnCapacityDef namedSilentFail4 = DefDatabase<PawnCapacityDef>.GetNamedSilentFail(text5);
			return new CellSpec
			{
				Kind = CellKind.Capacity,
				Arg = text5,
				CapDef = namedSilentFail4,
				Display = CapacityWord + ": " + ((namedSilentFail4 != null) ? ((string)namedSilentFail4.LabelCap) : text5)
			};
		}
		if (raw.StartsWith("record:"))
		{
			string text6 = raw.Substring(7);
			RecordDef namedSilentFail5 = DefDatabase<RecordDef>.GetNamedSilentFail(text6);
			LayoutDefRegistry.Register(namedSilentFail5);
			return new CellSpec
			{
				Kind = CellKind.Record,
				Arg = text6,
				RecordDef = namedSilentFail5,
				Display = RecordWord + ": " + ((namedSilentFail5 != null) ? ((string)namedSilentFail5.LabelCap) : text6)
			};
		}
		return raw switch
		{
			"mood" => new CellSpec
			{
				Kind = CellKind.Mood,
				Arg = "",
				Display = WMood
			}, 
			"skills" => new CellSpec
			{
				Kind = CellKind.SkillsGrid,
				Arg = "",
				Display = WSkillsGrid
			}, 
			"health" => new CellSpec
			{
				Kind = CellKind.Health,
				Arg = "",
				Display = WHealth
			}, 
			"healthbar" => new CellSpec
			{
				Kind = CellKind.HealthBar,
				Arg = "",
				Display = WHealthBar
			}, 
			"equip" => new CellSpec
			{
				Kind = CellKind.Equip,
				Arg = "",
				Display = WEquip
			}, 
			"armor" => new CellSpec
			{
				Kind = CellKind.Armor,
				Arg = "",
				Display = WArmor
			}, 
			"readout" => new CellSpec
			{
				Kind = CellKind.Readout,
				Arg = "",
				Display = WReadout
			}, 
			"area" => new CellSpec
			{
				Kind = CellKind.Area,
				Arg = "",
				Display = WArea
			}, 
			"timetable" => new CellSpec
			{
				Kind = CellKind.Timetable,
				Arg = "",
				Display = WTimetable
			}, 
			"policies" => new CellSpec
			{
				Kind = CellKind.Policies,
				Arg = "",
				Display = WPolicies
			}, 
			"medical" => new CellSpec
			{
				Kind = CellKind.Medical,
				Arg = "",
				Display = WMedical
			}, 
			"inspect" => new CellSpec
			{
				Kind = CellKind.Inspect,
				Arg = "",
				Display = WInspect
			}, 
			"mechenergy" => new CellSpec
			{
				Kind = CellKind.MechEnergy,
				Arg = "",
				Display = WMechEnergy
			}, 
			"production" => new CellSpec
			{
				Kind = CellKind.Production,
				Arg = "",
				Display = WProduction
			}, 
			"workmode" => new CellSpec
			{
				Kind = CellKind.WorkMode,
				Arg = "",
				Display = WWorkMode
			}, 
			"resistance" => new CellSpec
			{
				Kind = CellKind.Resistance,
				Arg = "",
				Display = WResistance
			}, 
			"training" => new CellSpec
			{
				Kind = CellKind.Training,
				Arg = "",
				Display = WTraining
			}, 
			"master" => new CellSpec
			{
				Kind = CellKind.Master,
				Arg = "",
				Display = WMaster
			}, 
			"portrait" => new CellSpec
			{
				Kind = CellKind.Portrait,
				Arg = "",
				Display = WPortrait
			}, 
			"name" => new CellSpec
			{
				Kind = CellKind.NameBlock,
				Arg = "",
				Display = WName
			}, 
			"activity" => new CellSpec
			{
				Kind = CellKind.Activity,
				Arg = "",
				Display = WActivity
			}, 
			"faction" => new CellSpec
			{
				Kind = CellKind.Faction,
				Arg = "",
				Display = WFaction
			}, 
			"genderage" => new CellSpec
			{
				Kind = CellKind.GenderAge,
				Arg = "",
				Display = WGenderAge
			}, 
			"traits" => new CellSpec
			{
				Kind = CellKind.Traits,
				Arg = "",
				Display = WTraits
			}, 
			"carrying" => new CellSpec
			{
				Kind = CellKind.Carrying,
				Arg = "",
				Display = WCarrying
			}, 
			"queued" => new CellSpec
			{
				Kind = CellKind.Queued,
				Arg = "",
				Display = WQueued
			}, 
			"mental" => new CellSpec
			{
				Kind = CellKind.Mental,
				Arg = "",
				Display = WMental
			}, 
			"hp" => new CellSpec
			{
				Kind = CellKind.HP,
				Arg = "",
				Display = WHP
			}, 
			"fuel" => new CellSpec
			{
				Kind = CellKind.Fuel,
				Arg = "",
				Display = WFuel
			}, 
			"growth" => new CellSpec
			{
				Kind = CellKind.Growth,
				Arg = "",
				Display = WGrowth
			}, 
			"chips" => new CellSpec
			{
				Kind = CellKind.Chips,
				Arg = "",
				Display = WChips
			}, 
			"freshness" => new CellSpec
			{
				Kind = CellKind.Freshness,
				Arg = "",
				Display = WFreshness
			}, 
			"battery" => new CellSpec
			{
				Kind = CellKind.Battery,
				Arg = "",
				Display = WBattery
			}, 
			"powergrid" => new CellSpec
			{
				Kind = CellKind.PowerGrid,
				Arg = "",
				Display = WPowerGrid
			}, 
			"pipenet" => new CellSpec
			{
				Kind = CellKind.PipeNet,
				Arg = "",
				Display = WPipeNet
			}, 
			_ => new CellSpec
			{
				Kind = CellKind.Spacer,
				Arg = "",
				Display = WSpacer
			}, 
		};
	}

	private static bool CellHasContent(Pawn pawn, in CellSpec spec)
	{
		switch (spec.Kind)
		{
		case CellKind.Mood:
			return Snap.HasMood;
		case CellKind.Need:
		{
			for (int l = 0; l < Snap.Needs.Count; l++)
			{
				if (Snap.Needs[l].Need.def == spec.NeedDef)
				{
					return true;
				}
			}
			return false;
		}
		case CellKind.Skill:
		{
			for (int k = 0; k < Snap.Skills.Count; k++)
			{
				if (Snap.Skills[k].Def == spec.SkillDef)
				{
					return true;
				}
			}
			return false;
		}
		case CellKind.SkillsGrid:
		{
			InspectorSettings settings = InspectorMod.Settings;
			for (int j = 0; j < Snap.Skills.Count; j++)
			{
				PawnSnapshot.SkillLine skillLine = Snap.Skills[j];
				if (!settings.skillsHideZero || (!skillLine.Disabled && !(skillLine.FillPct <= 0f)))
				{
					return true;
				}
			}
			return false;
		}
		case CellKind.HealthBar:
			return pawn.health != null;
		case CellKind.Equip:
			return pawn.equipment != null;
		case CellKind.Armor:
			return !Snap.ArmorLine.NullOrEmpty();
		case CellKind.Area:
			return ControlsPanel.SupportsArea(pawn);
		case CellKind.Timetable:
			return ControlsPanel.SupportsTimetable(pawn);
		case CellKind.Policies:
			return ControlsPanel.SupportsPolicies(pawn);
		case CellKind.Medical:
			return ControlsPanel.SupportsMedical(pawn);
		case CellKind.MechEnergy:
			return pawn.needs?.energy != null;
		case CellKind.Production:
			return Snap.Production.Count > 0;
		case CellKind.WorkMode:
			return ControlsPanel.SupportsWorkMode(pawn);
		case CellKind.Resistance:
			return ControlsPanel.SupportsResistance(pawn);
		case CellKind.Training:
			return ControlsPanel.SupportsTraining(pawn);
		case CellKind.Master:
			return ControlsPanel.SupportsMaster(pawn);
		case CellKind.Faction:
			return !Snap.SubLine.NullOrEmpty();
		case CellKind.GenderAge:
			return !Snap.GenderAge.NullOrEmpty();
		case CellKind.Traits:
			return !Snap.TraitsLine.NullOrEmpty();
		case CellKind.Carrying:
			return !Snap.Carrying.NullOrEmpty();
		case CellKind.Queued:
			return !Snap.Queued.NullOrEmpty();
		case CellKind.Mental:
			return !Snap.MentalLine.NullOrEmpty();
		case CellKind.Stat:
			return FindValueLine(Snap.StatLines, spec.StatDef) >= 0;
		case CellKind.Record:
			return FindValueLine(Snap.RecordLines, spec.RecordDef) >= 0;
		case CellKind.Capacity:
		{
			for (int i = 0; i < Snap.Capacities.Count; i++)
			{
				if (Snap.Capacities[i].Def == spec.CapDef)
				{
					return true;
				}
			}
			return false;
		}
		case CellKind.HP:
		case CellKind.Fuel:
		case CellKind.Growth:
		case CellKind.Chips:
		case CellKind.Freshness:
		case CellKind.Battery:
		case CellKind.PowerGrid:
		case CellKind.PipeNet:
			return false;
		default:
			return true;
		}
	}

	private static void ComputeCellVisibility(Pawn pawn, ParsedLayout pl)
	{
		PrepareVisibility(pl);
		for (int i = 0; i < pl.Rows.Count; i++)
		{
			List<CellSpec> list = pl.Rows[i];
			for (int j = 0; j < list.Count; j++)
			{
				CellSpec spec = list[j];
				pl.Vis[pl.RowStart[i] + j] = spec.Kind != CellKind.Header && spec.Kind != CellKind.Spacer && CellHasContent(pawn, in spec);
			}
		}
		HeaderSpacerPasses(pl);
	}

	internal static void PrepareVisibility(ParsedLayout pl)
	{
		pl.RowStart.Clear();
		int num = 0;
		for (int i = 0; i < pl.Rows.Count; i++)
		{
			pl.RowStart.Add(num);
			num += pl.Rows[i].Count;
		}
		if (pl.Vis.Length < num)
		{
			pl.Vis = new bool[Mathf.NextPowerOfTwo(num)];
		}
	}

	internal static void HeaderSpacerPasses(ParsedLayout pl)
	{
		bool flag = false;
		bool flag2 = false;
		for (int num = pl.Rows.Count - 1; num >= 0; num--)
		{
			List<CellSpec> list = pl.Rows[num];
			for (int num2 = list.Count - 1; num2 >= 0; num2--)
			{
				int num3 = pl.RowStart[num] + num2;
				switch (list[num2].Kind)
				{
				case CellKind.Header:
					pl.Vis[num3] = flag;
					flag = false;
					break;
				case CellKind.Spacer:
					pl.Vis[num3] = flag2;
					break;
				default:
					if (pl.Vis[num3])
					{
						flag = true;
						flag2 = true;
					}
					break;
				}
			}
		}
		bool flag3 = false;
		for (int i = 0; i < pl.Rows.Count; i++)
		{
			List<CellSpec> list2 = pl.Rows[i];
			for (int j = 0; j < list2.Count; j++)
			{
				int num4 = pl.RowStart[i] + j;
				switch (list2[j].Kind)
				{
				case CellKind.Spacer:
					pl.Vis[num4] = pl.Vis[num4] && flag3;
					continue;
				case CellKind.Header:
					continue;
				}
				if (pl.Vis[num4])
				{
					flag3 = true;
				}
			}
		}
	}

	internal static float DrawEyebrow(string text, float x, float w, float y)
	{
		float x2 = RadiusFont.Size(text).x;
		RadiusFont.Label(new Rect(x, y, w, 20f), text, GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		float num = x + Mathf.Min(x2, w) + 10f;
		if (num < x + w - 4f)
		{
			CardChrome.Fill(new Rect(num, y + 10f, x + w - num, 1f), Palette.Surface3);
		}
		return 20f;
	}

	private static void DrawLayoutRows(Pawn pawn, IInspectPane pane, float vw, ref float y, ParsedLayout pl, List<string> rows, float x0 = 0f)
	{
		EnsureParsed(pl, rows);
		ComputeCellVisibility(pawn, pl);
		float num = y;
		for (int i = 0; i < pl.Rows.Count; i++)
		{
			List<CellSpec> list = pl.Rows[i];
			int count = list.Count;
			int num2 = 0;
			for (int j = 0; j < count; j++)
			{
				if (pl.Vis[pl.RowStart[i] + j])
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
				if (pl.Vis[pl.RowStart[i] + k] && list[k].Kind == CellKind.Header)
				{
					if (y > num)
					{
						y += 10f;
					}
					break;
				}
			}
			float num3 = ((num2 > 1) ? Mathf.Floor((vw - 10f * (float)(num2 - 1)) / (float)num2) : Mathf.Floor(vw));
			float num4 = 0f;
			float num5 = x0;
			for (int l = 0; l < count; l++)
			{
				if (pl.Vis[pl.RowStart[i] + l])
				{
					float num6 = DrawCell(pawn, pane, list[l], num5, num3, y);
					if (num6 > num4)
					{
						num4 = num6;
					}
					num5 += num3 + 10f;
				}
			}
			if (num4 > 0f)
			{
				y += Mathf.Ceil(num4) + 4f;
			}
		}
	}

	private static float DrawCell(Pawn pawn, IInspectPane pane, CellSpec spec, float x, float w, float y)
	{
		switch (spec.Kind)
		{
		case CellKind.Readout:
			VanillaChrome.DrawReadout(pane, new Rect(x, y, w, 78f));
			return 78f;
		case CellKind.Area:
			return ControlsPanel.DrawArea(pawn, x, w, y);
		case CellKind.Timetable:
			return ControlsPanel.DrawTimetable(pawn, x, w, y);
		case CellKind.Policies:
			return ControlsPanel.DrawPolicies(pawn, x, w, y);
		case CellKind.Medical:
			return ControlsPanel.DrawMedical(pawn, x, w, y);
		case CellKind.MechEnergy:
			return ControlsPanel.DrawMechEnergy(pawn, x, w, y);
		case CellKind.WorkMode:
			return ControlsPanel.DrawWorkMode(pawn, x, w, y);
		case CellKind.Resistance:
			return ControlsPanel.DrawResistance(pawn, x, w, y);
		case CellKind.Training:
		{
			float num5 = ControlsPanel.DrawTraining(pawn, x, w, y);
			TabJump(pane, new Rect(x, y, w, num5), typeof(ITab_Pawn_Training), "TabTraining", WTraining, pawn, RichTips.AnimalId, RichTips.Animal);
			return num5;
		}
		case CellKind.Master:
			return ControlsPanel.DrawMaster(pawn, x, w, y);
		case CellKind.Production:
			return DrawProductionCell(x, w, y);
		case CellKind.Portrait:
			return DrawPortraitCell(pawn, x, w, y);
		case CellKind.NameBlock:
			return DrawNameCell(x, w, y);
		case CellKind.Activity:
			return DrawActivityCell(pawn, x, w, y);
		case CellKind.Faction:
			return DrawLineCell(Snap.SubLine, Palette.TextDim, x, w, y);
		case CellKind.GenderAge:
			return DrawLineCell(Snap.GenderAge, Palette.TextDim, x, w, y, pawn, RichTips.BioId, RichTips.Bio);
		case CellKind.Traits:
			return DrawLineCell(Snap.TraitsLine, Palette.Ink, x, w, y, pawn, RichTips.TraitsId, RichTips.Traits);
		case CellKind.Carrying:
			return DrawLineCell(Snap.Carrying, Palette.TextDim, x, w, y);
		case CellKind.Queued:
			return DrawLineCell(Snap.Queued, Palette.TextDim, x, w, y);
		case CellKind.Mental:
			return DrawLineCell(Snap.MentalLine, Snap.MentalBad ? Palette.Warn : RadiusTheme.Accent, x, w, y, pawn, RichTips.MoodId, RichTips.Mood);
		case CellKind.Stat:
		{
			int num7 = FindValueLine(Snap.StatLines, spec.StatDef);
			if (num7 >= 0)
			{
				return DrawValueRow(Snap.StatLines[num7], x, w, y);
			}
			return 0f;
		}
		case CellKind.Record:
		{
			int num6 = FindValueLine(Snap.RecordLines, spec.RecordDef);
			if (num6 >= 0)
			{
				return DrawValueRow(Snap.RecordLines[num6], x, w, y);
			}
			return 0f;
		}
		case CellKind.Capacity:
			return DrawCapacityCell(pawn, in spec, x, w, y);
		case CellKind.Inspect:
			return ControlsPanel.DrawInspect(pawn, x, w, y, cullEnabled ? scroll.y : 0f, cullEnabled ? lastViewH : 99999f);
		case CellKind.Header:
			return DrawEyebrow(spec.Arg, x, w, y);
		case CellKind.Spacer:
			return 8f;
		case CellKind.Mood:
		{
			float num4 = DrawMoodCell(pawn, x, w, y);
			TabJump(pane, new Rect(x, y, w, num4), typeof(ITab_Pawn_Needs), "TabNeeds", JumpNeeds, pawn, RichTips.MoodId, RichTips.Mood);
			return num4;
		}
		case CellKind.Need:
		{
			float num3 = DrawNeedCell(pawn, spec, x, w, y);
			NeedDef needDef = spec.NeedDef;
			Rect rect = new Rect(x, y, w, num3);
			TabJump(pane, rect, typeof(ITab_Pawn_Needs), "TabNeeds", JumpNeeds, pawn, (needDef != null) ? RichTips.NeedId(needDef) : 0, (needDef != null && Mouse.IsOver(rect)) ? ((Func<Pawn, string?>)((Pawn p) => RichTips.Need(p, needDef))) : null);
			return num3;
		}
		case CellKind.Skill:
		{
			float num2 = DrawSkillCell(pawn, spec, x, w, y, out string passion);
			SkillDef skillDef = spec.SkillDef;
			Rect rect2 = new Rect(x, y, w, num2);
			TabJump(pane, rect2, typeof(ITab_Pawn_Character), "TabCharacter", JumpSkills, pawn, (skillDef != null) ? RichTips.SkillId(skillDef) : 0, (skillDef != null && Mouse.IsOver(rect2)) ? ((Func<Pawn, string?>)((Pawn p) => RichTips.Skill(p, skillDef, passion))) : null);
			return num2;
		}
		case CellKind.SkillsGrid:
			return DrawSkillsGrid(pawn, x, w, y);
		case CellKind.Health:
			return DrawHealthCell(pawn, x, w, y);
		case CellKind.HealthBar:
		{
			float num = DrawHealthBarCell(pawn, x, w, y);
			TabJump(pane, new Rect(x, y, w, num), typeof(ITab_Pawn_Health), "TabHealth", JumpHealth, pawn, RichTips.HealthId, HealthTip);
			return num;
		}
		case CellKind.Equip:
			return DrawEquipCell(pawn, x, w, y);
		case CellKind.Armor:
			if (Snap.ArmorLine.NullOrEmpty())
			{
				return 0f;
			}
			RadiusFont.Label(new Rect(x + 2f, y, w - 2f, 23f), Snap.ArmorLine, GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			return 23f;
		default:
			return 0f;
		}
	}

	private static Color MoodColor(Pawn pawn, float pct)
	{
		MentalBreaker obj = pawn.mindState?.mentalBreaker;
		float num = obj?.BreakThresholdMinor ?? 0.35f;
		float num2 = obj?.BreakThresholdMajor ?? 0.2f;
		if (!(pct < num2))
		{
			if (!(pct < num))
			{
				return Palette.Good;
			}
			return Palette.Warn;
		}
		return Palette.Bad;
	}

	private static float DrawMoodCell(Pawn pawn, float x, float w, float y)
	{
		Need need = pawn.needs?.mood;
		if (need == null)
		{
			return 0f;
		}
		float curLevelPercentage = need.CurLevelPercentage;
		Color col = MoodColor(pawn, curLevelPercentage);
		Rect bar = new Rect(x, y, w, 15f);
		MentalBreaker mentalBreaker = pawn.mindState?.mentalBreaker;
		SuiteBar(bar, curLevelPercentage, col, (!InspectorMod.Settings.moodTicks) ? (-1f) : (mentalBreaker?.BreakThresholdMinor ?? (-1f)), (!InspectorMod.Settings.moodTicks) ? (-1f) : (mentalBreaker?.BreakThresholdMajor ?? (-1f)));
		BarInnerLabels(bar, MoodWord, Snap.MoodPctStr, curLevelPercentage);
		return 15f;
	}

	private static float DrawProductionCell(float x, float w, float y)
	{
		List<PawnSnapshot.ProductionLine> production = Snap.Production;
		if (production.Count == 0)
		{
			return 0f;
		}
		float num = y;
		for (int i = 0; i < production.Count; i++)
		{
			PawnSnapshot.ProductionLine productionLine = production[i];
			Rect rect = new Rect(x, num, w, 15f);
			SuiteBar(rect, productionLine.Fill, productionLine.Ready ? Palette.Good : Palette.FillNeutral, -1f, -1f);
			BarInnerLabels(rect, productionLine.Label, productionLine.Pct, productionLine.Fill);
			if (!productionLine.Tip.NullOrEmpty())
			{
				TooltipHandler.TipRegion(rect, productionLine.Tip);
			}
			num += 19f;
		}
		return num - y - 4f;
	}

	// rich: detailed tooltip (RichTips), shown above the jump hint; without a pane only the detail remains.
	private static void TabJump(IInspectPane pane, Rect r, Type vanillaType, string labelKey, string tip, Pawn? pawn = null, int tipId = 0, Func<Pawn, string?>? rich = null)
	{
		if (pane == null)
		{
			RichTips.Region(r, pawn, tipId, rich);
		}
		if (pane != null)
		{
			if (Mouse.IsOver(r))
			{
				CardChrome.Hover(r, 8f);
				RichTips.Region(r, pawn, tipId, rich, tip);
			}
			if (Widgets.ButtonInvisible(r))
			{
				OpenTabFor(pane, vanillaType, labelKey);
			}
		}
	}

	internal static void OpenTabFor(IInspectPane pane, Type vanillaType, string labelKey)
	{
		IEnumerable<InspectTabBase> curTabs = pane.CurTabs;
		if (curTabs == null)
		{
			return;
		}
		InspectTabBase inspectTabBase = null;
		foreach (InspectTabBase item in curTabs)
		{
			if (item != null && item.IsVisible && !item.Hidden)
			{
				if (vanillaType.IsInstanceOfType(item))
				{
					TabPlumbing.Toggle(item, pane);
					return;
				}
				if (inspectTabBase == null && item.labelKey == labelKey)
				{
					inspectTabBase = item;
				}
			}
		}
		if (inspectTabBase != null)
		{
			TabPlumbing.Toggle(inspectTabBase, pane);
		}
	}

	private static float DrawNeedCell(Pawn pawn, CellSpec spec, float x, float w, float y)
	{
		for (int i = 0; i < Snap.Needs.Count; i++)
		{
			PawnSnapshot.NeedLine needLine = Snap.Needs[i];
			if (needLine.Need.def == spec.NeedDef)
			{
				float curLevelPercentage = needLine.Need.CurLevelPercentage;
				Color col = ((curLevelPercentage < 0.25f) ? Palette.Bad : ((curLevelPercentage < 0.5f) ? Palette.Warn : Palette.FillNeutral));
				Rect bar = new Rect(x, y, w, 15f);
				SuiteBar(bar, curLevelPercentage, col, -1f, -1f);
				BarInnerLabels(bar, needLine.Label, needLine.Pct, curLevelPercentage);
				return 15f;
			}
		}
		return 0f;
	}

	private static float DrawSkillCell(Pawn pawn, CellSpec spec, float x, float w, float y, out string passion)
	{
		passion = "";
		for (int i = 0; i < Snap.Skills.Count; i++)
		{
			PawnSnapshot.SkillLine sk = Snap.Skills[i];
			if (sk.Def == spec.SkillDef)
			{
				DrawSkillRow(null, sk, x, w, y);
				passion = sk.PassionLabel;
				return 22f;
			}
		}
		return 0f;
	}

	// pawn: draw the row's detailed tooltip (skills grid); null when the caller owns the tooltip.
	private static void DrawSkillRow(Pawn? pawn, PawnSnapshot.SkillLine sk, float x, float w, float y)
	{
		Rect rect0 = new Rect(x, y, w, 22f);
		if (pawn != null && sk.Def != null && Mouse.IsOver(rect0))
		{
			SkillDef def = sk.Def;
			string passion = sk.PassionLabel;
			RichTips.Region(rect0, pawn, RichTips.SkillId(def), (Pawn p) => RichTips.Skill(p, def, passion));
		}
		float num = w * 0.44f;
		Color value = (sk.Disabled ? Palette.TextFaint : Palette.TextDim);
		RadiusFont.Label(new Rect(x + 2f, y, num - 2f, 22f), sk.Label, GameFont.Small, heading: false, value, TextAnchor.MiddleLeft, wrap: false);
		float num2 = x + num;
		if (!sk.Disabled && sk.Passion > 0)
		{
			Texture2D image = sk.PassionTex ?? ((sk.Passion >= 2) ? SkillUI.PassionMajorIcon : SkillUI.PassionMinorIcon);
			Rect rect = new Rect(num2, y + 3f, 16f, 16f);
			GUI.color = Color.white;
			GUI.DrawTexture(rect, image);
		}
		num2 += 18f;
		RadiusFont.Label(new Rect(num2, y, 24f, 22f), sk.Level, GameFont.Small, !sk.Disabled, sk.Disabled ? Palette.TextFaint : Palette.Ink, TextAnchor.MiddleRight, wrap: false);
		num2 += 28f;
		if (!sk.Disabled)
		{
			SuiteBar(new Rect(num2, y + 7f, Mathf.Max(20f, x + w - num2 - 2f), 8f), sk.FillPct, Palette.Good, -1f, -1f);
		}
	}

	private static float DrawSkillsGrid(Pawn pawn, float x, float w, float y)
	{
		InspectorSettings settings = InspectorMod.Settings;
		int num = ((!(w > 340f)) ? 1 : 2);
		float num2 = ((num > 1) ? Mathf.Floor((w - 10f) / 2f) : w);
		int num3 = 0;
		float num4 = y;
		int num5 = 0;
		for (int i = 0; i < Snap.Skills.Count; i++)
		{
			PawnSnapshot.SkillLine sk = Snap.Skills[i];
			if (!settings.skillsHideZero || (!sk.Disabled && !(sk.FillPct <= 0f)))
			{
				if (!cullEnabled || ViewCull.Visible(num4, 22f, scroll.y, lastViewH))
				{
					DrawSkillRow(pawn, sk, x + (float)num3 * (num2 + 10f), num2, num4);
				}
				num5++;
				num3++;
				if (num3 == num)
				{
					num3 = 0;
					num4 += 22f;
				}
			}
		}
		if (num3 != 0)
		{
			num4 += 22f;
		}
		if (num5 != 0)
		{
			return num4 - y;
		}
		return 0f;
	}

	private static float DrawHealthBarCell(Pawn pawn, float x, float w, float y)
	{
		if (pawn.health == null)
		{
			return 0f;
		}
		float healthPct = Snap.HealthPct;
		Color col = (Snap.HealthCritical ? Palette.Bad : ((healthPct < 0.5f) ? Palette.Warn : ((healthPct < 0.999f) ? Palette.FillNeutral : Palette.Good)));
		Rect rect = new Rect(x, y, w, 15f);
		SuiteBar(rect, healthPct, col, -1f, -1f);
		BarInnerLabels(rect, Snap.HealthCondition, Snap.HealthPctStr, healthPct);
		return 15f;
	}

	// Health tooltip, led by the bleeding line the bar used to show on its own.
	private static string? HealthTip(Pawn pawn)
	{
		string text = RichTips.Health(pawn);
		if (!Snap.Bleeding || Snap.BleedLabel.NullOrEmpty())
		{
			return text;
		}
		return Snap.BleedLabel.Colorize(Palette.Bad) + (text.NullOrEmpty() ? "" : ("\n\n" + text));
	}

	private static float DrawHealthCell(Pawn pawn, float x, float w, float y)
	{
		InspectorSettings settings = InspectorMod.Settings;
		float num = y;
		if (settings.healthBands)
		{
			if (Snap.Bleeding)
			{
				Band(x, w, ref y, Palette.Bad, Snap.BleedLabel, "Medical/Bleed");
			}
			if (Snap.Untended > 0)
			{
				Band(x, w, ref y, Palette.Warn, Snap.UntendedLabel, "Alert/Warn");
			}
		}
		int num2 = Mathf.Min(Snap.Conditions.Count, settings.healthRows);
		if (num2 == 0 && y == num)
		{
			RadiusFont.Label(new Rect(x + 2f, y, w - 2f, 23f), HealthyLabel, GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			y += 23f;
		}
		for (int i = 0; i < num2; i++)
		{
			PawnSnapshot.ConditionLine conditionLine = Snap.Conditions[i];
			IconSet.Get(conditionLine.IconPath).Draw(new Rect(x + 2f, y + 3f, 16f, 16f), conditionLine.Color);
			float num3 = (conditionLine.Untended ? 80f : 4f);
			float num4 = Mathf.Min(110f, w * 0.3f);
			RadiusFont.Label(new Rect(x + 24f, y, w - 24f - num4 - num3, 23f), conditionLine.Label, GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
			RadiusFont.Label(new Rect(x + w - num4 - num3, y, num4, 23f), conditionLine.Part, GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			if (conditionLine.Untended)
			{
				RadiusFont.Label(new Rect(x + w - 78f, y, 78f, 23f), UntendedChip, GameFont.Small, heading: false, Palette.Warn, TextAnchor.MiddleRight, wrap: false);
			}
			y += 23f;
		}
		RichTips.Region(new Rect(x, num, w, y - num), pawn, RichTips.HealthId, HealthTip);
		return y - num;
	}

	private static float DrawEquipCell(Pawn pawn, float x, float w, float y)
	{
		Rect rect = new Rect(x, y, w, 23f);
		bool num = Snap.WeaponThing != null;
		if (num && Mouse.IsOver(rect))
		{
			CardChrome.Hover(rect, 8f);
		}
		Rect rect2 = new Rect(x + 2f, y + 2f, 18f, 18f);
		if (Snap.WeaponThing != null)
		{
			GUI.color = Color.white;
			Widgets.ThingIcon(rect2, Snap.WeaponThing);
			GUI.color = Color.white;
		}
		else
		{
			IconSet.Get("Slot/Weapon").Draw(rect2.ContractedBy(2f), Palette.TextDim);
		}
		RadiusFont.Label(new Rect(x + 24f, y, w - 24f, 23f), Snap.Weapon, GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		if (num && Widgets.ButtonInvisible(rect))
		{
			OpenGearMenu(pawn, Snap.WeaponThing);
			InspectorCore.Poke();
		}
		return 23f;
	}

	private static void OpenGearMenu(Pawn pawn, Thing thing)
	{
		List<FloatMenuOption> list = new List<FloatMenuOption>
		{
			new FloatMenuOption(InfoTip, delegate
			{
				Find.WindowStack.Add(new Dialog_InfoCard(thing));
			})
		};
		if (pawn.IsColonistPlayerControlled && pawn.Spawned)
		{
			Apparel ap = thing as Apparel;
			if (ap != null && pawn.apparel != null && !pawn.apparel.IsLocked(ap))
			{
				list.Add(new FloatMenuOption(DropLabel, delegate
				{
					try
					{
						pawn.apparel.TryDrop(ap);
					}
					catch (Exception ex)
					{
						Log.Warning("[Radius UI - Inspector] Drop failed: " + ex.Message);
					}
				}));
			}
			else
			{
				ThingWithComps eq = thing as ThingWithComps;
				if (eq != null && pawn.equipment?.Primary == eq)
				{
					list.Add(new FloatMenuOption(DropLabel, delegate
					{
						try
						{
							pawn.equipment.TryDropEquipment(eq, out var _, pawn.Position);
						}
						catch (Exception ex)
						{
							Log.Warning("[Radius UI - Inspector] Drop failed: " + ex.Message);
						}
					}));
				}
			}
		}
		Find.WindowStack.Add(new FloatMenu(list));
	}

	internal static void SuiteBar(Rect bar, float pct, Color col, float thresh1, float thresh2)
	{
		CardChrome.Pill(bar, Palette.Surface3);
		float num = Mathf.Round(bar.width * Mathf.Clamp01(pct));
		if (num >= 2f)
		{
			CardChrome.Pill(new Rect(bar.x, bar.y, num, bar.height), col);
		}
		BarTick(bar, thresh1, Mathf.Clamp01(pct));
		BarTick(bar, thresh2, Mathf.Clamp01(pct));
	}

	private static void BarTick(Rect bar, float t, float pct)
	{
		if (!(t <= 0f) && !(t >= 1f))
		{
			Color color = ((t < pct) ? new Color(0f, 0f, 0f, 0.45f) : new Color(1f, 1f, 1f, 0.22f));
			CardChrome.Fill(new Rect(bar.x + bar.width * t, bar.y + bar.height * 0.55f, 1f, bar.height * 0.45f), color);
		}
	}

	internal static void BarInnerLabels(Rect bar, string label, string pct, float fillPct)
	{
		float num = Mathf.Max(44f, Mathf.Ceil(RadiusFont.Size(pct).x) + 10f);
		float num2 = bar.x + bar.width * Mathf.Clamp01(fillPct);
		Rect rect = new Rect(bar.x + 8f, bar.y - 1f, bar.width - num - 20f, bar.height + 2f);
		float num3 = Mathf.Min(RadiusFont.Size(label).x, rect.width);
		RadiusFont.Label(color: (num2 > rect.x + num3 * 0.5f) ? Palette.InkOnAccent : Palette.Ink, rect: rect, text: label, font: GameFont.Small, heading: false, anchor: TextAnchor.MiddleLeft, wrap: false);
		Rect rect2 = new Rect(bar.xMax - num - 6f, bar.y - 1f, num, bar.height + 2f);
		float num4 = Mathf.Min(RadiusFont.Size(pct).x, rect2.width);
		Color value = ((num2 > rect2.xMax - num4 * 0.5f) ? Palette.InkOnAccent : Palette.Ink);
		RadiusFont.Label(rect2, pct, GameFont.Small, heading: false, value, TextAnchor.MiddleRight, wrap: false);
	}

	private static void Band(float x, float w, ref float y, Color color, string label, string iconPath)
	{
		CardChrome.Rounded(new Rect(x, y, w, 24f), color, 8f);
		IconSet.Get(iconPath).Draw(new Rect(x + 8f, y + 4f, 16f, 16f), Palette.InkOnAccent);
		RadiusFont.Label(new Rect(x + 30f, y, w - 34f, 24f), label, GameFont.Small, heading: true, Palette.InkOnAccent, TextAnchor.MiddleLeft, wrap: false);
		y += 28f;
	}

	internal static List<FloatMenuOption> BuildCellOptions(Action<string> add, bool things = false)
	{
		if (things)
		{
			return new List<FloatMenuOption>
			{
				new FloatMenuOption(WHeader, delegate
				{
					add("header:" + WHeader);
				}),
				new FloatMenuOption(WHP, delegate
				{
					add("hp");
				}),
				new FloatMenuOption(WFreshness, delegate
				{
					add("freshness");
				}),
				new FloatMenuOption(WFuel, delegate
				{
					add("fuel");
				}),
				new FloatMenuOption(WGrowth, delegate
				{
					add("growth");
				}),
				new FloatMenuOption(WBattery, delegate
				{
					add("battery");
				}),
				new FloatMenuOption(WPowerGrid, delegate
				{
					add("powergrid");
				}),
				new FloatMenuOption(WPipeNet, delegate
				{
					add("pipenet");
				}),
				new FloatMenuOption(WChips, delegate
				{
					add("chips");
				}),
				new FloatMenuOption(WInspect, delegate
				{
					add("inspect");
				}),
				new FloatMenuOption(WSpacer, delegate
				{
					add("spacer");
				})
			};
		}
		List<FloatMenuOption> list = new List<FloatMenuOption>
		{
			new FloatMenuOption(WHeader, delegate
			{
				add("header:" + WHeader);
			}),
			new FloatMenuOption(WMood, delegate
			{
				add("mood");
			}),
			new FloatMenuOption(WHealth, delegate
			{
				add("health");
			}),
			new FloatMenuOption(WHealthBar, delegate
			{
				add("healthbar");
			}),
			new FloatMenuOption(WSkillsGrid, delegate
			{
				add("skills");
			}),
			new FloatMenuOption(WEquip, delegate
			{
				add("equip");
			}),
			new FloatMenuOption(WArmor, delegate
			{
				add("armor");
			}),
			new FloatMenuOption(WReadout, delegate
			{
				add("readout");
			}),
			new FloatMenuOption(WSpacer, delegate
			{
				add("spacer");
			}),
			new FloatMenuOption(WArea, delegate
			{
				add("area");
			}),
			new FloatMenuOption(WTimetable, delegate
			{
				add("timetable");
			}),
			new FloatMenuOption(WPolicies, delegate
			{
				add("policies");
			}),
			new FloatMenuOption(WMedical, delegate
			{
				add("medical");
			}),
			new FloatMenuOption(WInspect, delegate
			{
				add("inspect");
			}),
			new FloatMenuOption(WMechEnergy, delegate
			{
				add("mechenergy");
			}),
			new FloatMenuOption(WProduction, delegate
			{
				add("production");
			}),
			new FloatMenuOption(WWorkMode, delegate
			{
				add("workmode");
			}),
			new FloatMenuOption(WResistance, delegate
			{
				add("resistance");
			}),
			new FloatMenuOption(WTraining, delegate
			{
				add("training");
			}),
			new FloatMenuOption(WMaster, delegate
			{
				add("master");
			}),
			new FloatMenuOption(WPortrait, delegate
			{
				add("portrait");
			}),
			new FloatMenuOption(WName, delegate
			{
				add("name");
			}),
			new FloatMenuOption(WFaction, delegate
			{
				add("faction");
			}),
			new FloatMenuOption(WActivity, delegate
			{
				add("activity");
			}),
			new FloatMenuOption(WGenderAge, delegate
			{
				add("genderage");
			}),
			new FloatMenuOption(WTraits, delegate
			{
				add("traits");
			}),
			new FloatMenuOption(WCarrying, delegate
			{
				add("carrying");
			}),
			new FloatMenuOption(WQueued, delegate
			{
				add("queued");
			}),
			new FloatMenuOption(WMental, delegate
			{
				add("mental");
			}),
			new FloatMenuOption(StatPickLbl, delegate
			{
				Find.WindowStack.Add(new FloatMenu(BuildStatOptions(add)));
			}),
			new FloatMenuOption(CapacityPickLbl, delegate
			{
				Find.WindowStack.Add(new FloatMenu(BuildCapacityOptions(add)));
			}),
			new FloatMenuOption(RecordPickLbl, delegate
			{
				Find.WindowStack.Add(new FloatMenu(BuildRecordOptions(add)));
			})
		};
		List<NeedDef> allDefsListForReading = DefDatabase<NeedDef>.AllDefsListForReading;
		List<FloatMenuOption> list2 = new List<FloatMenuOption>(allDefsListForReading.Count);
		for (int num = 0; num < allDefsListForReading.Count; num++)
		{
			NeedDef needDef = allDefsListForReading[num];
			if (!(needDef.needClass != null) || !typeof(Need_Mood).IsAssignableFrom(needDef.needClass))
			{
				string def = needDef.defName;
				list2.Add(new FloatMenuOption(TitleNeedsWord + ": " + needDef.LabelCap, delegate
				{
					add("need:" + def);
				}));
			}
		}
		list2.SortBy((FloatMenuOption o) => o.Label);
		list.AddRange(list2);
		List<SkillDef> allDefsListForReading2 = DefDatabase<SkillDef>.AllDefsListForReading;
		for (int num2 = 0; num2 < allDefsListForReading2.Count; num2++)
		{
			SkillDef sd = allDefsListForReading2[num2];
			list.Add(new FloatMenuOption(string.Concat("RUI.Inspector.Skills".Translate(), ": ") + sd.LabelCap, delegate
			{
				add("skill:" + sd.defName);
			}));
		}
		return list;
	}

	private static List<FloatMenuOption> BuildStatOptions(Action<string> add)
	{
		List<StatDef> allDefsListForReading = DefDatabase<StatDef>.AllDefsListForReading;
		List<FloatMenuOption> list = new List<FloatMenuOption>(96);
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			StatDef statDef = allDefsListForReading[i];
			if (statDef.category != null && PawnStatCategories.Contains(statDef.category.defName))
			{
				string def = statDef.defName;
				list.Add(new FloatMenuOption(statDef.LabelCap, delegate
				{
					add("stat:" + def);
				}));
			}
		}
		list.SortBy((FloatMenuOption o) => o.Label);
		return list;
	}

	private static List<FloatMenuOption> BuildCapacityOptions(Action<string> add)
	{
		List<PawnCapacityDef> allDefsListForReading = DefDatabase<PawnCapacityDef>.AllDefsListForReading;
		List<FloatMenuOption> list = new List<FloatMenuOption>(allDefsListForReading.Count);
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			PawnCapacityDef pawnCapacityDef = allDefsListForReading[i];
			string def = pawnCapacityDef.defName;
			list.Add(new FloatMenuOption(pawnCapacityDef.LabelCap, delegate
			{
				add("capacity:" + def);
			}));
		}
		list.SortBy((FloatMenuOption o) => o.Label);
		return list;
	}

	private static List<FloatMenuOption> BuildRecordOptions(Action<string> add)
	{
		List<RecordDef> allDefsListForReading = DefDatabase<RecordDef>.AllDefsListForReading;
		List<FloatMenuOption> list = new List<FloatMenuOption>(allDefsListForReading.Count);
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			RecordDef recordDef = allDefsListForReading[i];
			string def = recordDef.defName;
			list.Add(new FloatMenuOption(recordDef.LabelCap, delegate
			{
				add("record:" + def);
			}));
		}
		list.SortBy((FloatMenuOption o) => o.Label);
		return list;
	}

	internal static string CellDisplay(string raw)
	{
		return ParseCell(raw).Display;
	}

	internal static string CellIcon(string raw)
	{
		if (raw.StartsWith("header:"))
		{
			return "Action/Rename";
		}
		if (raw.StartsWith("need:"))
		{
			return "Stat/Wave";
		}
		if (raw.StartsWith("skill:"))
		{
			return "Stat/BarGraph";
		}
		if (raw.StartsWith("stat:"))
		{
			return "Action/TabStats";
		}
		if (raw.StartsWith("capacity:"))
		{
			return "Medical/Bionic";
		}
		if (raw.StartsWith("record:"))
		{
			return "Common/Book";
		}
		return raw switch
		{
			"mood" => "Stat/Mood", 
			"healthbar" => "Stat/Heart", 
			"health" => "Medical/Injury", 
			"medical" => "Medical/Bandage", 
			"skills" => "Stat/BarGraph", 
			"equip" => "Slot/Weapon", 
			"armor" => "Stat/Shield", 
			"area" => "Action/DefPos", 
			"timetable" => "Reminder/Clock", 
			"policies" => "Action/Policies", 
			"inspect" => "Alert/Info", 
			"mechenergy" => "Medical/Pain", 
			"production" => "Stat/Droplet", 
			"workmode" => "Action/CommandCenter", 
			"resistance" => "Status/Break", 
			"training" => "Action/Check", 
			"master" => "Action/Draft", 
			"portrait" => "Action/Bio", 
			"name" => "Chrome/SnipTitle", 
			"faction" => "Reminder/Flag", 
			"genderage" => "Common/Cal", 
			"traits" => "Stat/Quality", 
			"carrying" => "Slot/Pack", 
			"queued" => "Reminder/Check", 
			"mental" => "Alert/Warn", 
			"activity" => "Action/Play", 
			"readout" => "Action/SwapView", 
			"hp" => "Stat/Heart", 
			"fuel" => "Stat/Flame", 
			"growth" => "Common/Leaf", 
			"chips" => "Stat/Tetris", 
			"freshness" => "Stat/Food", 
			"battery" => "Weather/Thunder", 
			"powergrid" => "Stat/Glow", 
			"pipenet" => "Stat/Droplet", 
			_ => "Action/Grid", 
		};
	}

	internal static void SheetHeading(float sw, ref float cy, string label)
	{
		cy += 4f;
		RadiusFont.Label(new Rect(0f, cy, sw, 22f), label, GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		cy += 24f;
		CardChrome.Fill(new Rect(0f, cy, sw, 1f), Palette.Border);
		cy += 6f;
	}

	internal static void Stepper(float sw, ref float cy, string label, int value, int min, int max, Action<int> set)
	{
		Rect rect = new Rect(0f, cy, sw, 24f);
		if (Mouse.IsOver(rect))
		{
			CardChrome.Hover(rect, 8f);
		}
		RadiusFont.Label(new Rect(4f, cy, sw - 96f, 24f), label, GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		Rect rect2 = new Rect(sw - 88f, cy + 2f, 20f, 20f);
		Rect rect3 = new Rect(sw - 66f, cy, 24f, 24f);
		Rect rect4 = new Rect(sw - 40f, cy + 2f, 20f, 20f);
		bool flag = Mouse.IsOver(rect2);
		bool flag2 = Mouse.IsOver(rect4);
		CardChrome.Rounded(rect2, flag ? Palette.Surface3 : Palette.Surface2, 6f);
		CardChrome.Rounded(rect4, flag2 ? Palette.Surface3 : Palette.Surface2, 6f);
		RadiusFont.Label(rect2, "-", GameFont.Small, heading: true, (value > min) ? Palette.Ink : Palette.TextFaint, TextAnchor.MiddleCenter, wrap: false);
		RadiusFont.Label(rect4, "+", GameFont.Small, heading: true, (value < max) ? Palette.Ink : Palette.TextFaint, TextAnchor.MiddleCenter, wrap: false);
		RadiusFont.Label(rect3, value.ToString(), GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleCenter, wrap: false);
		if (Widgets.ButtonInvisible(rect2) && value > min)
		{
			set(value - 1);
		}
		if (Widgets.ButtonInvisible(rect4) && value < max)
		{
			set(value + 1);
		}
		cy += 26f;
	}

	internal static void SwitchRow(float sw, float indent, ref float cy, string label, bool value, Action<bool> set)
	{
		Rect rect = new Rect(indent, cy, sw - indent, 26f);
		if (Mouse.IsOver(rect))
		{
			CardChrome.Hover(rect, 8f);
		}
		RadiusFont.Label(new Rect(rect.x + 4f, cy, rect.width - 48f, 26f), label, GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		Rect rect2 = new Rect(sw - 40f, cy + 3f, 36f, 20f);
		CardChrome.Pill(rect2, value ? RadiusTheme.Accent : Palette.Surface3);
		float num = rect2.height - 4f;
		CardChrome.Pill(new Rect(value ? (rect2.xMax - num - 2f) : (rect2.x + 2f), rect2.y + 2f, num, num), Palette.Ink);
		if (Widgets.ButtonInvisible(rect))
		{
			set(!value);
			InspectorCore.Poke();
		}
		cy += 28f;
	}
}
