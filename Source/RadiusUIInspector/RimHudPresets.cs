using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using Verse;

namespace RadiusUIInspector;

// Reads RimHUD layouts (Jaxe) without RimHUD loaded: the player's presets and current layout in
// Config/RimHUD, the presets RimHUD ships, and the RimHUD/Presets folders of other mods. A layout becomes
// the body rows of each pawn profile; the rail is not part of RimHUD, the preset keeps the current one.
internal static class RimHudPresets
{
	internal sealed class Entry
	{
		public string Name = "";

		public string Source = "";

		public string Path = "";

		// The preset names mods ("Requires") and none of them is active.
		public bool MissingMods;
	}

	internal sealed class Result
	{
		public LayoutUserPreset? Preset;

		public readonly List<string> Skipped = new List<string>();

		public string Error = "";
	}

	private const string RimHudId = "jaxe.rimhud";

	private const int MaxCells = 3;

	// RimHUD widget -> Inspector cell. Widgets with no counterpart are left out and listed in the result.
	private static readonly Dictionary<string, string> Widgets = new Dictionary<string, string>
	{
		{ "NameHeader", "name" },
		{ "Food", "policies" },
		{ "Outfit", "policies" },
		{ "Area", "area" },
		{ "Timetable", "timetable" },
		{ "RelationKindAndFaction", "faction" },
		{ "GenderRaceAndAge", "genderage" },
		{ "Health", "healthbar" },
		{ "HealthCondition", "health" },
		{ "MentalCondition", "mental" },
		{ "Master", "master" },
		{ "Activity", "activity" },
		{ "Queued", "queued" },
		{ "Equipped", "equip" },
		{ "Carrying", "carrying" },
		{ "Traits", "traits" },
		{ "CompInfo", "inspect" },
		{ "PrisonerInfo", "resistance" },
		{ "NeedMood", "mood" },
		{ "NeedFood", "need:Food" },
		{ "NeedSleep", "need:Rest" },
		{ "NeedEnergy", "mechenergy" },
		{ "NeedRecreation", "need:Joy" },
		{ "NeedSuppression", "need:Suppression" }
	};

	// Layout pieces that only shape RimHUD's own grid.
	private static readonly HashSet<string> Ignored = new HashSet<string> { "Blank", "Separator" };

	private static readonly HashSet<string> Containers = new HashSet<string> { "Layout", "Docked", "Floating", "Panel", "HStack", "VStack" };

	internal static List<Entry> Find()
	{
		List<Entry> list = new List<Entry>();
		string text = System.IO.Path.Combine(GenFilePaths.ConfigFolderPath, "RimHUD");
		string text2 = System.IO.Path.Combine(text, "Docked.xml");
		if (File.Exists(text2) && HasContent(text2))
		{
			list.Add(new Entry
			{
				Name = "RUI.Inspector.RimHudCurrent".Translate(),
				Source = "RUI.Inspector.RimHudConfig".Translate(),
				Path = text2
			});
		}
		AddFolder(list, System.IO.Path.Combine(text, "Presets"), "RUI.Inspector.RimHudConfig".Translate());
		foreach (ModMetaData item in ModLister.AllInstalledMods)
		{
			if (item?.RootDir == null)
			{
				continue;
			}
			bool flag = item.PackageId != null && item.PackageId.StartsWith(RimHudId, StringComparison.OrdinalIgnoreCase);
			AddFolder(list, flag ? System.IO.Path.Combine(item.RootDir.FullName, "Presets") : System.IO.Path.Combine(item.RootDir.FullName, "RimHUD", "Presets"), item.Name);
		}
		return list;
	}

	private static void AddFolder(List<Entry> list, string dir, string source)
	{
		try
		{
			if (!Directory.Exists(dir))
			{
				return;
			}
			foreach (string item in Directory.GetFiles(dir, "*.xml").OrderBy((string f) => f, StringComparer.OrdinalIgnoreCase))
			{
				list.Add(new Entry
				{
					Name = System.IO.Path.GetFileNameWithoutExtension(item),
					Source = source,
					Path = item,
					MissingMods = RequiresMissing(item)
				});
			}
		}
		catch (Exception ex)
		{
			Log.Warning("[Radius UI Inspector] Cannot list RimHUD presets in " + dir + ": " + ex.Message);
		}
	}

	private static bool HasContent(string path)
	{
		try
		{
			return XDocument.Load(path).Root?.HasElements ?? false;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool RequiresMissing(string path)
	{
		try
		{
			string text = XDocument.Load(path).Root?.Attribute("Requires")?.Value;
			if (text.NullOrEmpty())
			{
				return false;
			}
			return !text.Split(new char[1] { '|' }, StringSplitOptions.RemoveEmptyEntries).Any((string id) => ModLister.GetActiveModWithIdentifier(id.Trim(), ignorePostfix: true) != null);
		}
		catch (Exception)
		{
			return false;
		}
	}

	// rails: the current rail of each profile; its cells are left out of the body so they do not show twice.
	internal static Result Import(Entry entry, InspectorSettings rails)
	{
		Result result = new Result();
		XElement xElement;
		try
		{
			xElement = XDocument.Load(entry.Path).Root;
		}
		catch (Exception ex)
		{
			result.Error = ex.Message;
			return result;
		}
		// A preset file holds <Docked> and <Floating>; Docked.xml is the layout itself. The Inspector is docked.
		XElement xElement2 = (xElement?.Name.LocalName == "Preset") ? (xElement.Element("Docked") ?? xElement.Element("Floating")) : xElement;
		if (xElement2 == null || !xElement2.HasElements)
		{
			result.Error = "RUI.Inspector.RimHudEmpty".Translate();
			return result;
		}
		LayoutUserPreset layoutUserPreset = new LayoutUserPreset
		{
			name = entry.Name,
			source = "RimHUD · " + entry.Source
		};
		HashSet<string> hashSet = new HashSet<string>();
		bool flag = false;
		for (int i = 0; i < 4; i++)
		{
			PawnProfileKind pawnProfileKind = (PawnProfileKind)i;
			List<string> list = layoutUserPreset.RowsFor(pawnProfileKind);
			// The preset keeps this rail, so applying it later never drops a cell left out of the body here.
			List<string> rail = layoutUserPreset.RailFor(pawnProfileKind);
			rail.AddRange(rails.EffectiveRailFor(pawnProfileKind));
			HashSet<string> used = new HashSet<string>();
			foreach (string item in rail)
			{
				used.UnionWith(item.Split(new char[1] { '|' }));
			}
			Collect(xElement2, TargetOf(pawnProfileKind), list, used, hashSet);
			flag |= list.Count > 0;
		}
		result.Skipped.AddRange(hashSet.OrderBy((string s) => s));
		if (!flag)
		{
			result.Error = "RUI.Inspector.RimHudEmpty".Translate();
			return result;
		}
		result.Preset = layoutUserPreset;
		return result;
	}

	// RimHUD targets: H player humanlike, h other humanlike, C player creature, c other creature.
	private static char TargetOf(PawnProfileKind kind)
	{
		return kind switch
		{
			PawnProfileKind.Prisoner => 'h',
			PawnProfileKind.Animal => 'C',
			PawnProfileKind.Mech => 'C',
			_ => 'H',
		};
	}

	private static bool Shown(XElement el, char target)
	{
		string text = el.Attribute("Targets")?.Value;
		return text.NullOrEmpty() || text.IndexOf(target) >= 0;
	}

	private static void Collect(XElement el, char target, List<string> rows, HashSet<string> used, HashSet<string> skipped)
	{
		foreach (XElement item in el.Elements())
		{
			if (!Shown(item, target))
			{
				continue;
			}
			string localName = item.Name.LocalName;
			if (localName == "Row")
			{
				AddRow(item, target, rows, used, skipped);
			}
			else if (Containers.Contains(localName))
			{
				Collect(item, target, rows, used, skipped);
			}
		}
	}

	private static void AddRow(XElement row, char target, List<string> rows, HashSet<string> used, HashSet<string> skipped)
	{
		List<string> list = new List<string>(MaxCells);
		foreach (XElement item in row.Elements())
		{
			if (!Shown(item, target))
			{
				continue;
			}
			string text = CellFor(item, skipped);
			// One cell of each kind: two RimHUD selectors (food, outfit) both become the policies cell.
			if (text != null && used.Add(text))
			{
				if (list.Count == MaxCells)
				{
					rows.Add(string.Join("|", list.ToArray()));
					list.Clear();
				}
				list.Add(text);
			}
		}
		if (list.Count > 0)
		{
			rows.Add(string.Join("|", list.ToArray()));
		}
	}

	private static string? CellFor(XElement widget, HashSet<string> skipped)
	{
		string localName = widget.Name.LocalName;
		if (Ignored.Contains(localName))
		{
			return null;
		}
		string text = widget.Attribute("DefName")?.Value;
		string text2 = null;
		if (Widgets.TryGetValue(localName, out string value))
		{
			text2 = value;
		}
		else if (localName.StartsWith("Skill") && localName.Length > 5)
		{
			text2 = "skill:" + localName.Substring(5);
		}
		else if (localName.StartsWith("Training"))
		{
			text2 = "training";
		}
		else if (!text.NullOrEmpty())
		{
			text2 = localName switch
			{
				"Need" => "need:" + text,
				"Skill" => "skill:" + text,
				"Stat" => "stat:" + text,
				"Record" => "record:" + text,
				_ => null,
			};
		}
		if (text2 == null || !DefExists(text2))
		{
			skipped.Add(text.NullOrEmpty() ? localName : (localName + " (" + text + ")"));
			return null;
		}
		return text2;
	}

	// Cells naming a def the game does not have (a mod missing) would stay empty: leave them out.
	private static bool DefExists(string cell)
	{
		int num = cell.IndexOf(':');
		if (num < 0)
		{
			return true;
		}
		string defName = cell.Substring(num + 1);
		return cell.Substring(0, num) switch
		{
			"need" => DefDatabase<NeedDef>.GetNamedSilentFail(defName) != null,
			"skill" => DefDatabase<SkillDef>.GetNamedSilentFail(defName) != null,
			"stat" => DefDatabase<StatDef>.GetNamedSilentFail(defName) != null,
			"record" => DefDatabase<RecordDef>.GetNamedSilentFail(defName) != null,
			_ => true,
		};
	}
}
