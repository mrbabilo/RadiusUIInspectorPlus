using System;
using System.IO;
using System.Xml;
using RimWorld;
using Verse;

namespace RadiusUIInspector;

// "Useless" and "non-useless" apparel and weapons, taken over from Useless Filter (Mlie). Their defs keep the
// original defNames and load only while Useless Filter itself is off (LoadFolders.xml). What counts as useless
// is set in the mod settings: any one condition met makes the thing useless.
public static class UselessRules
{
	public static bool IsUseless(Thing thing)
	{
		InspectorSettings settings = InspectorMod.Settings;
		if (thing == null || thing.Destroyed || settings == null)
		{
			return false;
		}
		if (thing.def.useHitPoints && settings.uselessHitPoints.Includes((float)thing.HitPoints / (float)thing.MaxHitPoints))
		{
			return true;
		}
		if (thing.TryGetQuality(out var qc) && settings.uselessQuality.Includes(qc))
		{
			return true;
		}
		if (thing is Apparel apparel)
		{
			if (settings.uselessTainted && apparel.WornByCorpse)
			{
				return true;
			}
			if (settings.uselessClean && !apparel.WornByCorpse)
			{
				return true;
			}
		}
		// A persona weapon is bonded, not biocoded: it never counts here. Useless Filter called a biocoded weapon
		// useless only when coded to one of the colony's pawns; here, as for apparel, biocoded is useless unless
		// one of the colony's pawns can use it.
		bool biocoded = thing.TryGetComp<CompBladelinkWeapon>() == null && CompBiocodable.IsBiocoded(thing);
		if (settings.uselessBiocoded && biocoded && thing.TryGetComp<CompBiocodable>()?.CodedPawn?.Faction != Faction.OfPlayerSilentFail)
		{
			return true;
		}
		if (settings.uselessNonBiocoded && !biocoded)
		{
			return true;
		}
		return false;
	}
}

public abstract class UselessFilterWorker : SpecialThingFilterWorker
{
	protected abstract bool Useless { get; }

	public override bool Matches(Thing t)
	{
		return UselessRules.IsUseless(t) == Useless;
	}

	public override bool CanEverMatch(ThingDef def)
	{
		if (!def.IsApparel)
		{
			return def.IsWeapon;
		}
		return true;
	}
}

public class SpecialThingFilterWorker_Useless : UselessFilterWorker
{
	protected override bool Useless => true;
}

public class SpecialThingFilterWorker_NonUseless : UselessFilterWorker
{
	protected override bool Useless => false;
}

// Useless Filter keeps its conditions in its own settings file (Config/Mod_<folder>_UselessFilterMod.xml).
// They are copied into the Inspector settings once, on the first start that finds the file, and again on
// demand from the settings page. The file is only read.
[StaticConstructorOnStartup]
public static class UselessSettingsImport
{
	static UselessSettingsImport()
	{
		InspectorSettings settings = InspectorMod.Settings;
		if (settings == null || settings.uselessImported || FindFile() == null)
		{
			return;
		}
		if (TryImport())
		{
			Log.Message("[Radius UI - Inspector] Useless Filter's conditions copied into the Inspector Plus settings.");
		}
	}

	// The most recent file: the mod's folder name is part of it (Workshop number or local folder).
	public static string? FindFile()
	{
		try
		{
			string? best = null;
			DateTime bestTime = DateTime.MinValue;
			foreach (string path in Directory.GetFiles(GenFilePaths.ConfigFolderPath, "Mod_*_UselessFilterMod.xml"))
			{
				DateTime time = File.GetLastWriteTimeUtc(path);
				if (best == null || time > bestTime)
				{
					best = path;
					bestTime = time;
				}
			}
			return best;
		}
		catch (Exception)
		{
			return null;
		}
	}

	public static bool TryImport()
	{
		InspectorSettings settings = InspectorMod.Settings;
		string? path = FindFile();
		if (settings == null || path == null)
		{
			return false;
		}
		try
		{
			XmlDocument doc = new XmlDocument();
			doc.Load(path);
			XmlNode? node = doc.SelectSingleNode("//ModSettings");
			if (node == null)
			{
				return false;
			}
			// Each value Useless Filter left at its default is absent from the file: ours goes to that default.
			settings.uselessHitPoints = Read(node, "DurabilityRange", InspectorSettings.DefaultUselessHitPoints, FloatRange.FromString);
			settings.uselessQuality = Read(node, "QualityRange", InspectorSettings.DefaultUselessQuality, QualityRange.FromString);
			settings.uselessTainted = Read(node, "Tainted", true, bool.Parse);
			settings.uselessClean = Read(node, "Clean", false, bool.Parse);
			settings.uselessBiocoded = Read(node, "Biocoded", true, bool.Parse);
			settings.uselessNonBiocoded = Read(node, "NonBiocoded", false, bool.Parse);
			settings.uselessImported = true;
			settings.Write();
			return true;
		}
		catch (Exception ex)
		{
			Log.Warning("[Radius UI - Inspector] Useless Filter's settings could not be read from " + path + ": " + ex.Message);
			return false;
		}
	}

	private static T Read<T>(XmlNode node, string name, T fallback, Func<string, T> parse)
	{
		XmlNode? child = node[name];
		if (child == null || child.InnerText.NullOrEmpty())
		{
			return fallback;
		}
		return parse(child.InnerText.Trim());
	}
}
