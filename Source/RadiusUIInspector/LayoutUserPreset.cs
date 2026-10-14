using System.Collections.Generic;
using Verse;

namespace RadiusUIInspector;

// A layout the player saved (or imported from RimHUD): body and rail rows of each pawn profile.
// An empty lane is not part of the preset and keeps the current layout when applied.
public class LayoutUserPreset : IExposable
{
	public string name = "";

	// Where an imported preset came from, shown under its name; empty for a saved layout.
	public string source = "";

	private List<string> layoutRows = new List<string>();

	private List<string> railRows = new List<string>();

	private List<string> animalRows = new List<string>();

	private List<string> animalRail = new List<string>();

	private List<string> mechRows = new List<string>();

	private List<string> mechRail = new List<string>();

	private List<string> prisonerRows = new List<string>();

	private List<string> prisonerRail = new List<string>();

	public List<string> RowsFor(PawnProfileKind kind)
	{
		return kind switch
		{
			PawnProfileKind.Animal => animalRows,
			PawnProfileKind.Mech => mechRows,
			PawnProfileKind.Prisoner => prisonerRows,
			_ => layoutRows,
		};
	}

	public List<string> RailFor(PawnProfileKind kind)
	{
		return kind switch
		{
			PawnProfileKind.Animal => animalRail,
			PawnProfileKind.Mech => mechRail,
			PawnProfileKind.Prisoner => prisonerRail,
			_ => railRows,
		};
	}

	public static LayoutUserPreset Capture(InspectorSettings s, string name)
	{
		LayoutUserPreset layoutUserPreset = new LayoutUserPreset
		{
			name = name
		};
		layoutUserPreset.CaptureFrom(s);
		return layoutUserPreset;
	}

	public void CaptureFrom(InspectorSettings s)
	{
		source = "";
		for (int i = 0; i < 4; i++)
		{
			PawnProfileKind kind = (PawnProfileKind)i;
			Replace(RowsFor(kind), s.EffectiveLayoutFor(kind));
			Replace(RailFor(kind), s.EffectiveRailFor(kind));
		}
	}

	public void ApplyTo(InspectorSettings s)
	{
		for (int i = 0; i < 4; i++)
		{
			PawnProfileKind kind = (PawnProfileKind)i;
			if (RowsFor(kind).Count > 0)
			{
				Replace(s.RowsFor(kind), RowsFor(kind));
			}
			if (RailFor(kind).Count > 0)
			{
				Replace(s.RailFor(kind), RailFor(kind));
			}
		}
		s.LayoutStamp++;
	}

	private static void Replace(List<string> into, List<string> from)
	{
		into.Clear();
		into.AddRange(from);
	}

	public void ExposeData()
	{
		Scribe_Values.Look(ref name, "name", "");
		Scribe_Values.Look(ref source, "source", "");
		Scribe_Collections.Look(ref layoutRows, "layoutRows", LookMode.Value);
		Scribe_Collections.Look(ref railRows, "railRows", LookMode.Value);
		Scribe_Collections.Look(ref animalRows, "animalRows", LookMode.Value);
		Scribe_Collections.Look(ref animalRail, "animalRail", LookMode.Value);
		Scribe_Collections.Look(ref mechRows, "mechRows", LookMode.Value);
		Scribe_Collections.Look(ref mechRail, "mechRail", LookMode.Value);
		Scribe_Collections.Look(ref prisonerRows, "prisonerRows", LookMode.Value);
		Scribe_Collections.Look(ref prisonerRail, "prisonerRail", LookMode.Value);
		name = name ?? "";
		source = source ?? "";
		layoutRows = layoutRows ?? new List<string>();
		railRows = railRows ?? new List<string>();
		animalRows = animalRows ?? new List<string>();
		animalRail = animalRail ?? new List<string>();
		mechRows = mechRows ?? new List<string>();
		mechRail = mechRail ?? new List<string>();
		prisonerRows = prisonerRows ?? new List<string>();
		prisonerRail = prisonerRail ?? new List<string>();
	}
}
