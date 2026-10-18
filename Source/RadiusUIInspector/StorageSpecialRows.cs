using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

// The special filters of the storage panel, laid out one row per set of choices instead of one row per filter:
// fresh and rotten, smeltable and non-smeltable, every skill book… Each choice is a chip, green or red by what
// it lets in (SpecialEffect); a refused chip is faded and struck through. Rows with several choices end with an
// "All" chip that allows every one of them. A filter that belongs to no known set stays a row of its own.
public static partial class StoragePanel
{
	private sealed class SpecialRow
	{
		public string? title;

		public string? scope;

		public string key = "";

		// The choices share out every thing of the defs they can match: refusing them all refuses those defs.
		public bool partition;

		public readonly List<SpecialThingFilterDef> choices = new List<SpecialThingFilterDef>();

		// Chip widths and line count, kept until the width, the font or the italic setting changes: measuring
		// every chip on every pass cost more than drawing the visible ones.
		public float[]? chipW;

		public float layoutVw = -1f;

		public int layoutEpoch = -1;

		public bool layoutItalic;

		public float scopeW;

		public int lines;
	}

	// Known sets, in the order of their choices. Apparel and weapons versions are separate sets: the scope
	// (category) shows at the end of the row. A set marked P is a partition: every thing of a def one of its
	// choices can match is matched by one of them (checked against the vanilla workers), so refusing all of
	// them refuses the def itself.
	private const bool P = true;

	private static readonly (string title, string[] defNames, bool partition)[] SpecialSets = new (string, string[], bool)[]
	{
		("State", new string[] { "AllowFresh", "AllowRotten" }, P),
		("Taint", new string[] { "AllowNonDeadmansApparel", "AllowDeadmansApparel" }, P),
		("Usefulness", new string[] { "NonUselessApparelFilter", "UselessApparelFilter" }, P),
		("Usefulness", new string[] { "NonUselessWeaponFilter", "UselessWeaponFilter" }, P),
		("Biocoding", new string[] { "AllowNonBiocodedApparel", "AllowBiocodedApparel" }, P),
		("Biocoding", new string[] { "AllowNonBiocodedWeapons", "AllowBiocodedWeapons" }, P),
		("Smelting", new string[] { "AllowSmeltableApparel", "AllowNonSmeltableApparel" }, P),
		("Smelting", new string[] { "AllowSmeltable", "AllowNonSmeltableWeapons" }, P),
		("Burning", new string[] { "AllowBurnableApparel", "AllowNonBurnableApparel" }, P),
		("Burning", new string[] { "AllowBurnableWeapons", "AllowNonBurnableWeapons" }, P),
		("Age", new string[] { "AllowAdultOnlyApparel", "AllowChildOnlyApparel" }, P),
		("Sex", new string[] { "AllowMaleGender", "AllowFemaleGender" }, false),
		("HumanCorpses", new string[] { "AllowCorpsesColonist", "AllowCorpsesSlave", "AllowCorpsesStranger", "AllowCorpsesUnnatural" }, P),
		("MechCorpses", new string[] { "AllowCorpsesMechFriendly", "AllowCorpsesMechEnemy" }, false),
		("AnimalCorpses", new string[] { "UsefulFilters_AllowCorpsesColonyAnimal", "UsefulFilters_AllowCorpsesNonColonyAnimal" }, P),
		("Diet", new string[] { "AllowVegetarian", "AllowCarnivore", "AllowInsectMeat", "AllowCannibal" }, false),
		("Research", new string[] { "AllowMainResearch", "AllowAnomalyResearch" }, false),
		("Skills", new string[] { "AllowBookShooting", "AllowBookMelee", "AllowBookConstruction", "AllowBookMining", "AllowBookCooking", "AllowBookPlants", "AllowBookAnimals", "AllowBookCrafting", "AllowBookArtistic", "AllowBookMedicine", "AllowBookSocial", "AllowBookIntellectual" }, false),
		("Quality", new string[] { "UsefulFilters_AllowHasQuality", "UsefulFilters_AllowNoQuality" }, P),
		("Quality", new string[] { "UR_AllowQuality", "UR_AllowNoQuality" }, P),
		("Quality", new string[] { "NoQuality_Weapons" }, false),
		("Quality", new string[] { "NoQuality_Apparels" }, false),
		("Deterioration", new string[] { "UsefulFilters_AllowNonDeteriorable", "UsefulFilters_AllowDeteriorable" }, P),
		("Deterioration", new string[] { "UR_AllowNotDeteriorating", "UR_AllowDeteriorating" }, P),
		("Rot", new string[] { "UsefulFilters_AllowNonRottable", "UsefulFilters_AllowRottable" }, P),
		// Better Filter's "perishable" also counts things that dissolve: not the same set as rotting.
		("Perishable", new string[] { "UR_AllowNotPerishable", "UR_AllowPerishable" }, P),
		("Flammability", new string[] { "UsefulFilters_AllowNonFlammable", "UsefulFilters_AllowFlammable" }, P),
		("HitPoints", new string[] { "RUI_AllowHasHitPoints", "RUI_AllowNoHitPoints" }, P),
		("HitPoints", new string[] { "UR_AllowHitPoints", "UR_AllowNoHitPoints" }, P),
		("Stacking", new string[] { "RUI_AllowStackable", "RUI_AllowNonStackable" }, P),
		("Stacking", new string[] { "UR_AllowStackable", "UR_AllowNonStackable" }, P)
	};

	private static Dictionary<string, int>? setOfDefName;

	private static readonly List<SpecialRow> specialRows = new List<SpecialRow>();

	private static readonly Dictionary<SpecialThingFilterDef, string> chipText = new Dictionary<SpecialThingFilterDef, string>();

	private const float ChipH = 20f;

	private const float ChipLineH = 24f;

	// Width of the title column: the longest title of the rows built, within bounds.
	private static float titleW = 104f;

	// Unknown sets from other mods: "AllowNonFoo" or "AllowNotFoo" pairs with "AllowFoo".
	private static readonly Regex Negation = new Regex("(Non|Not)(?=[A-Z])");

	private static void BuildSpecialRows(ThingCategoryDef root)
	{
		if (setOfDefName == null)
		{
			setOfDefName = new Dictionary<string, int>();
			for (int i = 0; i < SpecialSets.Length; i++)
			{
				foreach (string defName in SpecialSets[i].defNames)
				{
					setOfDefName[defName] = i;
				}
			}
		}
		specialRows.Clear();
		Dictionary<string, SpecialRow> byKey = new Dictionary<string, SpecialRow>();
		for (int i = 0; i < specialCache.Count; i++)
		{
			SpecialThingFilterDef sf = specialCache[i];
			string key;
			string? title = null;
			bool partition = false;
			if (setOfDefName.TryGetValue(sf.defName, out var set))
			{
				key = "set:" + set;
				title = ("RUI.Inspector.SpecialSet" + SpecialSets[set].title).Translate();
				partition = SpecialSets[set].partition;
			}
			else
			{
				key = "pair:" + Negation.Replace(sf.defName, "") + "|" + sf.parentCategory?.defName;
			}
			if (!byKey.TryGetValue(key, out var row))
			{
				row = new SpecialRow
				{
					key = key,
					partition = partition,
					title = title,
					scope = (sf.parentCategory != null && sf.parentCategory != root && sf.parentCategory != ThingCategoryDefOf.Root) ? sf.parentCategory.LabelCap.ToString() : null
				};
				byKey[key] = row;
				specialRows.Add(row);
			}
			row.choices.Add(sf);
		}
		// The scope (Apparel, Weapons) tells apart two rows of the same title only; elsewhere the title says it.
		Dictionary<string, int> titleCount = new Dictionary<string, int>();
		foreach (SpecialRow row in specialRows)
		{
			if (row.title != null)
			{
				titleCount[row.title] = (titleCount.TryGetValue(row.title, out var n) ? n : 0) + 1;
			}
		}
		titleW = 80f;
		foreach (SpecialRow row in specialRows)
		{
			if (row.title != null)
			{
				if (titleCount[row.title] < 2)
				{
					row.scope = null;
				}
				titleW = Mathf.Max(titleW, RadiusFont.Width(row.title, GameFont.Small, bold: false) + 14f);
			}
		}
		titleW = Mathf.Min(titleW, 170f);
		// Choices of a known set follow the set's order, whatever order the defs came in.
		foreach (SpecialRow row in specialRows)
		{
			if (row.choices.Count > 1 && setOfDefName.TryGetValue(row.choices[0].defName, out var set))
			{
				string[] order = SpecialSets[set].defNames;
				row.choices.SortBy((SpecialThingFilterDef sf) => Array.IndexOf(order, sf.defName));
			}
		}
	}

	// The game's own label without its leading verb ("allow fresh" reads "fresh"), so chips keep its words.
	private static string ChipText(SpecialThingFilterDef sf)
	{
		if (!chipText.TryGetValue(sf, out var text))
		{
			// The row shows the scope: the plain label, without the category SpecialLabel adds to tell twins apart.
			text = sf.LabelCap;
			foreach (string prefix in "RUI.Inspector.SpecialVerbPrefixes".Translate().ToString().Split('|'))
			{
				if (prefix.Length > 0 && text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					text = text.Substring(prefix.Length);
					break;
				}
			}
			text = text.CapitalizeFirst();
			chipText[sf] = text;
		}
		return text;
	}

	private static bool RowMatches(SpecialRow row, string q)
	{
		if (q.Length == 0 || (row.title != null && row.title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
		{
			return true;
		}
		for (int i = 0; i < row.choices.Count; i++)
		{
			if (SpecialMatches(row.choices[i], q) || ChipText(row.choices[i]).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	private static float ChipW(SpecialThingFilterDef sf)
	{
		return RadiusFont.Width(ChipText(sf), GameFont.Tiny, bold: false) + ((SpecialEffect(sf) != 0) ? 26f : 16f);
	}

	private static string? specialAllLabel;

	private static string SpecialAllLabel => specialAllLabel ??= "RUI.Inspector.SpecialAll".Translate();

	// Flows the chips of a row (a new line when the next one does not fit) once per width and font.
	private static void LayoutRow(SpecialRow row, float vw, float allW, float left)
	{
		if (row.chipW != null && row.layoutVw == vw && row.layoutEpoch == RadiusFont.Epoch && row.layoutItalic == RadiusTheme.Italic)
		{
			return;
		}
		row.layoutVw = vw;
		row.layoutEpoch = RadiusFont.Epoch;
		row.layoutItalic = RadiusTheme.Italic;
		row.scopeW = (row.scope != null) ? (RadiusFont.Width(row.scope, GameFont.Tiny, bold: false) + 8f) : 0f;
		row.chipW = new float[row.choices.Count];
		float right = vw - 4f - (allW + 4f) - row.scopeW;
		int lines = 1;
		float x = left;
		for (int i = 0; i < row.choices.Count; i++)
		{
			float w = row.chipW[i] = ChipW(row.choices[i]);
			if (x > left && x + w > right)
			{
				lines++;
				x = left;
			}
			x += w + 4f;
		}
		row.lines = lines;
	}

	private static void DrawSpecialRows(ThingFilter filter, ThingFilter? parentFilter, float vw, ref float cy, string q)
	{
		string all = SpecialAllLabel;
		// Measuring the label also brings RadiusFont.Epoch up to date before the cached layouts are checked.
		float allW = RadiusFont.Width(all, GameFont.Tiny, bold: false) + 16f;
		for (int r = 0; r < specialRows.Count; r++)
		{
			SpecialRow row = specialRows[r];
			if (!RowMatches(row, q))
			{
				continue;
			}
			bool multi = row.choices.Count > 1;
			float left = Indent + 6f + ((row.title != null) ? titleW : 0f);
			LayoutRow(row, vw, allW, left);
			float scopeW = row.scopeW;
			// The "All" column is kept on single-choice rows too, so the scopes line up.
			float right = vw - 4f - (allW + 4f) - scopeW;
			float[] widths = row.chipW!;
			float rowH = row.lines * ChipLineH;
			Rect rowRect = new Rect(Indent, cy, vw - Indent, rowH);
			bool visible = ViewCull.Visible(cy, rowH, scroll.y, lastViewH);
			if (visible)
			{
				DrawGuides(1, cy, rowH, RadiusTheme.Accent);
				if (Mouse.IsOver(rowRect))
				{
					CardChrome.Hover(rowRect, 8f);
				}
				if (row.title != null)
				{
					RadiusFont.Label(new Rect(Indent + 6f, cy, titleW - 6f, ChipLineH), row.title, GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
				}
				if (row.scope != null)
				{
					RadiusFont.Label(new Rect(right + 4f, cy, scopeW - 4f, ChipLineH), row.scope, GameFont.Tiny, heading: false, Palette.TextFaint, TextAnchor.MiddleLeft, wrap: false);
				}
			}
			float x = left;
			float y = cy;
			int allowed = 0;
			for (int i = 0; i < row.choices.Count; i++)
			{
				SpecialThingFilterDef sf = row.choices[i];
				float w = widths[i];
				if (x > left && x + w > right)
				{
					x = left;
					y += ChipLineH;
				}
				Rect chip = new Rect(x, y + (ChipLineH - ChipH) * 0.5f, w, ChipH);
				bool on = filter.Allows(sf);
				if (on)
				{
					allowed++;
				}
				if (visible)
				{
					DrawSpecialChip(chip, sf, on);
				}
				if (PaintHit(chip, on, on))
				{
					filter.SetAllow(sf, paintAllow);
					SyncSpecialDefs(sf, paintAllow, filter, parentFilter);
					SyncRowDefs(row, filter, parentFilter);
					memoDirty = true;
					InspectorCore.Poke();
				}
				x += w + 4f;
			}
			// "All" works as a box: on when every choice is allowed. Press it, or drag over several, to allow or
			// refuse every choice of the rows passed.
			if (multi)
			{
				Rect allRect = new Rect(vw - 4f - allW, cy + (ChipLineH - ChipH) * 0.5f, allW, ChipH);
				bool full = allowed >= row.choices.Count;
				if (visible)
				{
					bool hot = Mouse.IsOver(allRect);
					Color fill = full ? Palette.WithAlpha(RadiusTheme.Accent, hot ? 0.5f : 0.35f) : (hot ? Palette.Surface3 : Palette.Surface2);
					CardChrome.Rounded(allRect, fill, ChipH * 0.5f);
					CardChrome.Outline(allRect, (full || hot) ? RadiusTheme.Accent : Palette.Border, 1f, ChipH * 0.5f);
					RadiusFont.Label(allRect, all, GameFont.Tiny, heading: false, (full || hot) ? Palette.Ink : Palette.TextDim, TextAnchor.MiddleCenter, wrap: false);
					if (hot)
					{
						TooltipHandler.TipRegion(allRect, (full ? "RUI.Inspector.SpecialNoneTip" : "RUI.Inspector.SpecialAllTip").Translate());
					}
				}
				if (PaintHit(allRect, full, allowed > 0))
				{
					for (int i = 0; i < row.choices.Count; i++)
					{
						if (filter.Allows(row.choices[i]) != paintAllow)
						{
							filter.SetAllow(row.choices[i], paintAllow);
							SyncSpecialDefs(row.choices[i], paintAllow, filter, parentFilter);
						}
					}
					SyncRowDefs(row, filter, parentFilter);
					memoDirty = true;
					InspectorCore.Poke();
				}
			}
			cy += rowH;
		}
	}

	// Defs a partition row unticked because all its choices were refused, per filter and row: allowing a
	// choice again reticks them, and only them.
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ThingFilter, Dictionary<string, List<ThingDef>>> rowOff = new System.Runtime.CompilerServices.ConditionalWeakTable<ThingFilter, Dictionary<string, List<ThingDef>>>();

	// A partition row turns the item rows it covers off once every choice that applies to them is refused, and
	// back on once one is allowed again. A def no choice of the row applies to is left alone.
	private static void SyncRowDefs(SpecialRow row, ThingFilter filter, ThingFilter? parentFilter)
	{
		if (!row.partition || specialRoot == null)
		{
			return;
		}
		try
		{
			Dictionary<string, List<ThingDef>> memo = rowOff.GetOrCreateValue(filter);
			memo.TryGetValue(row.key, out var off);
			HashSet<ThingDef> seen = new HashSet<ThingDef>();
			foreach (ThingDef def in specialRoot.DescendantThingDefs)
			{
				if (!seen.Add(def) || !Visible(def, parentFilter))
				{
					continue;
				}
				bool covered = false;
				bool anyAllowed = false;
				for (int i = 0; i < row.choices.Count; i++)
				{
					SpecialThingFilterDef sf = row.choices[i];
					if ((sf.parentCategory != null && !def.IsWithinCategory(sf.parentCategory)) || sf.Worker == null || !sf.Worker.CanEverMatch(def))
					{
						continue;
					}
					covered = true;
					if (filter.Allows(sf))
					{
						anyAllowed = true;
						break;
					}
				}
				if (!covered)
				{
					continue;
				}
				if (!anyAllowed)
				{
					if (filter.Allows(def))
					{
						filter.SetAllow(def, allow: false);
						(off ??= new List<ThingDef>()).Add(def);
					}
				}
				else if (off != null && off.Remove(def))
				{
					filter.SetAllow(def, allow: true);
				}
			}
			if (off != null && off.Count > 0)
			{
				memo[row.key] = off;
			}
			else
			{
				memo.Remove(row.key);
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] special row sync failed: " + ex.Message, 5351413);
		}
	}

	// Green admits sound things (fresh, clean), red admits spoiled or risky ones (rotten, tainted), accent the
	// rest. Allowed: filled in its tone. Refused: faded, struck through.
	private static void DrawSpecialChip(Rect chip, SpecialThingFilterDef sf, bool on)
	{
		int effect = SpecialEffect(sf);
		Color tone = (effect > 0) ? Palette.Good : ((effect < 0) ? Palette.Bad : RadiusTheme.Accent);
		bool hover = Mouse.IsOver(chip);
		float radius = chip.height * 0.5f;
		CardChrome.Rounded(chip, on ? Palette.WithAlpha(tone, hover ? 0.42f : 0.3f) : (hover ? Palette.Surface3 : Palette.Surface2), radius);
		CardChrome.Outline(chip, on ? tone : Palette.Border, 1f, radius);
		float textX = chip.x + 8f;
		if (effect != 0)
		{
			CardChrome.Rounded(new Rect(textX, chip.center.y - 3f, 6f, 6f), on ? tone : Palette.WithAlpha(tone, 0.35f), 3f);
			textX += 10f;
		}
		Rect textRect = new Rect(textX, chip.y, chip.xMax - textX - 6f, chip.height);
		RadiusFont.Label(textRect, ChipText(sf), GameFont.Tiny, heading: false, on ? Palette.Ink : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		if (!on)
		{
			CardChrome.Fill(new Rect(textX - 2f, chip.center.y, chip.xMax - textX - 4f, 1f), Palette.TextDim);
		}
		if (hover)
		{
			string tip = SpecialLabel(sf);
			if (!sf.description.NullOrEmpty())
			{
				tip = tip + "\n\n" + sf.description;
			}
			TooltipHandler.TipRegion(chip, tip);
		}
	}
}
