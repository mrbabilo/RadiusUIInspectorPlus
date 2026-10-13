using System.Collections.Generic;
using RimWorld;

namespace RadiusUIInspector;

internal static class LayoutDefRegistry
{
	private static readonly List<StatDef> stats = new List<StatDef>(8);

	private static readonly List<RecordDef> records = new List<RecordDef>(8);

	internal static int Version { get; private set; } = 1;

	internal static IReadOnlyList<StatDef> Stats => stats;

	internal static IReadOnlyList<RecordDef> Records => records;

	internal static void Register(StatDef? def)
	{
		if (def != null && !stats.Contains(def))
		{
			stats.Add(def);
			Version++;
		}
	}

	internal static void Register(RecordDef? def)
	{
		if (def != null && !records.Contains(def))
		{
			records.Add(def);
			Version++;
		}
	}
}
