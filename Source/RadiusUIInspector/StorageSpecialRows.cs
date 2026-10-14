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

		public readonly List<SpecialThingFilterDef> choices = new List<SpecialThingFilterDef>();
	}

	// Known sets, in the order of their choices. Apparel and weapons versions are separate sets: the scope
	// (category) shows at the end of the row.
	private static readonly (string title, string[] defNames)[] SpecialSets = new (string, string[])[]
	{
		("State", new string[] { "AllowFresh", "AllowRotten" }),
		("Taint", new string[] { "AllowNonDeadmansApparel", "AllowDeadmansApparel" }),
		("Usefulness", new string[] { "NonUselessApparelFilter", "UselessApparelFilter" }),
		("Usefulness", new string[] { "NonUselessWeaponFilter", "UselessWeaponFilter" }),
		("Biocoding", new string[] { "AllowNonBiocodedApparel", "AllowBiocodedApparel" }),
		("Biocoding", new string[] { "AllowNonBiocodedWeapons", "AllowBiocodedWeapons" }),
		("Smelting", new string[] { "AllowSmeltableApparel", "AllowNonSmeltableApparel" }),
		("Smelting", new string[] { "AllowSmeltable", "AllowNonSmeltableWeapons" }),
		("Burning", new string[] { "AllowBurnableApparel", "AllowNonBurnableApparel" }),
		("Burning", new string[] { "AllowBurnableWeapons", "AllowNonBurnableWeapons" }),
		("Age", new string[] { "AllowAdultOnlyApparel", "AllowChildOnlyApparel" }),
		("Sex", new string[] { "AllowMaleGender", "AllowFemaleGender" }),
		("HumanCorpses", new string[] { "AllowCorpsesColonist", "AllowCorpsesSlave", "AllowCorpsesStranger", "AllowCorpsesUnnatural" }),
		("MechCorpses", new string[] { "AllowCorpsesMechFriendly", "AllowCorpsesMechEnemy" }),
		("AnimalCorpses", new string[] { "UsefulFilters_AllowCorpsesColonyAnimal", "UsefulFilters_AllowCorpsesNonColonyAnimal" }),
		("Diet", new string[] { "AllowVegetarian", "AllowCarnivore", "AllowInsectMeat", "AllowCannibal" }),
		("Research", new string[] { "AllowMainResearch", "AllowAnomalyResearch" }),
		("Skills", new string[] { "AllowBookShooting", "AllowBookMelee", "AllowBookConstruction", "AllowBookMining", "AllowBookCooking", "AllowBookPlants", "AllowBookAnimals", "AllowBookCrafting", "AllowBookArtistic", "AllowBookMedicine", "AllowBookSocial", "AllowBookIntellectual" }),
		("Quality", new string[] { "UsefulFilters_AllowHasQuality", "UsefulFilters_AllowNoQuality" }),
		("Quality", new string[] { "UR_AllowQuality", "UR_AllowNoQuality" }),
		("Deterioration", new string[] { "UsefulFilters_AllowNonDeteriorable", "UsefulFilters_AllowDeteriorable" }),
		("Deterioration", new string[] { "UR_AllowNotDeteriorating", "UR_AllowDeteriorating" }),
		("Rot", new string[] { "UsefulFilters_AllowNonRottable", "UsefulFilters_AllowRottable" }),
		("Rot", new string[] { "UR_AllowNotPerishable", "UR_AllowPerishable" }),
		("Flammability", new string[] { "UsefulFilters_AllowNonFlammable", "UsefulFilters_AllowFlammable" }),
		("HitPoints", new string[] { "RUI_AllowHasHitPoints", "RUI_AllowNoHitPoints" }),
		("HitPoints", new string[] { "UR_AllowHitPoints", "UR_AllowNoHitPoints" }),
		("Stacking", new string[] { "RUI_AllowStackable", "RUI_AllowNonStackable" }),
		("Stacking", new string[] { "UR_AllowStackable", "UR_AllowNonStackable" })
	};

	private static Dictionary<string, int>? setOfDefName;

	private static readonly List<SpecialRow> specialRows = new List<SpecialRow>();

	private static readonly Dictionary<SpecialThingFilterDef, string> chipText = new Dictionary<SpecialThingFilterDef, string>();

	private const float ChipH = 20f;

	private const float ChipLineH = 24f;

	private const float TitleW = 104f;

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
			if (setOfDefName.TryGetValue(sf.defName, out var set))
			{
				key = "set:" + set;
				title = ("RUI.Inspector.SpecialSet" + SpecialSets[set].title).Translate();
			}
			else
			{
				key = "pair:" + Negation.Replace(sf.defName, "") + "|" + sf.parentCategory?.defName;
			}
			if (!byKey.TryGetValue(key, out var row))
			{
				row = new SpecialRow
				{
					title = title,
					scope = (sf.parentCategory != null && sf.parentCategory != root && sf.parentCategory != ThingCategoryDefOf.Root) ? sf.parentCategory.LabelCap.ToString() : null
				};
				byKey[key] = row;
				specialRows.Add(row);
			}
			row.choices.Add(sf);
		}
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

	private static void DrawSpecialRows(ThingFilter filter, ThingFilter? parentFilter, float vw, ref float cy, string q)
	{
		string all = "RUI.Inspector.SpecialAll".Translate();
		float allW = RadiusFont.Width(all, GameFont.Tiny, bold: false) + 16f;
		for (int r = 0; r < specialRows.Count; r++)
		{
			SpecialRow row = specialRows[r];
			if (!RowMatches(row, q))
			{
				continue;
			}
			bool multi = row.choices.Count > 1;
			float left = Indent + 6f + ((row.title != null) ? TitleW : 0f);
			float scopeW = (row.scope != null) ? (RadiusFont.Width(row.scope, GameFont.Tiny, bold: false) + 8f) : 0f;
			float right = vw - 4f - (multi ? (allW + 4f) : 0f) - scopeW;
			// Flow the chips: a new line when the next one does not fit.
			int lines = 1;
			float x = left;
			for (int i = 0; i < row.choices.Count; i++)
			{
				float w = ChipW(row.choices[i]);
				if (x > left && x + w > right)
				{
					lines++;
					x = left;
				}
				x += w + 4f;
			}
			float rowH = lines * ChipLineH;
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
					RadiusFont.Label(new Rect(Indent + 6f, cy, TitleW - 6f, ChipLineH), row.title, GameFont.Small, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
				}
				if (row.scope != null)
				{
					RadiusFont.Label(new Rect(right + 4f, cy, scopeW - 4f, ChipLineH), row.scope, GameFont.Tiny, heading: false, Palette.TextFaint, TextAnchor.MiddleLeft, wrap: false);
				}
			}
			x = left;
			float y = cy;
			int allowed = 0;
			for (int i = 0; i < row.choices.Count; i++)
			{
				SpecialThingFilterDef sf = row.choices[i];
				float w = ChipW(sf);
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
					memoDirty = true;
					InspectorCore.Poke();
				}
				x += w + 4f;
			}
			if (multi)
			{
				Rect allRect = new Rect(vw - 4f - allW, cy + (ChipLineH - ChipH) * 0.5f, allW, ChipH);
				bool full = allowed >= row.choices.Count;
				if (visible)
				{
					bool hot = !full && Mouse.IsOver(allRect);
					CardChrome.Rounded(allRect, full ? Palette.WithAlpha(RadiusTheme.Accent, 0.35f) : (hot ? Palette.Surface3 : Palette.Surface2), ChipH * 0.5f);
					CardChrome.Outline(allRect, full ? RadiusTheme.Accent : Palette.Border, 1f, ChipH * 0.5f);
					RadiusFont.Label(allRect, all, GameFont.Tiny, heading: false, full ? Palette.Ink : (hot ? Palette.Ink : Palette.TextDim), TextAnchor.MiddleCenter, wrap: false);
					TooltipHandler.TipRegion(allRect, "RUI.Inspector.SpecialAllTip".Translate());
				}
				if (!full && Widgets.ButtonInvisible(allRect))
				{
					BeginEvictWatch();
					for (int i = 0; i < row.choices.Count; i++)
					{
						if (!filter.Allows(row.choices[i]))
						{
							filter.SetAllow(row.choices[i], allow: true);
							SyncSpecialDefs(row.choices[i], allow: true, filter, parentFilter);
						}
					}
					EndEvictWatch();
					memoDirty = true;
					InspectorCore.Poke();
				}
			}
			cy += rowH;
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
