using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RadiusUIInspector;

// Special filters taken over from Useful Filters (kentotsu). Their defs keep the original defNames, so a
// save that refused one of them keeps it refused. They load only while Useful Filters itself is off
// (LoadFolders.xml).
//
// Each filter comes in a pair: "has the property" and "lacks it". The property is read from the def when the
// def alone decides it, and from the thing otherwise (a material can make a cloth shirt burn and a steel one
// not), so the filter never unticks a def it cannot judge.
public abstract class PropertyFilterWorker : SpecialThingFilterWorker
{
	// True for the "has the property" filter of the pair, false for "lacks it".
	protected abstract bool Wanted { get; }

	// Null when the material decides.
	protected abstract bool? DefHas(ThingDef def);

	protected abstract bool ThingHas(Thing t);

	// Defs do not change once loaded, and vanilla asks AlwaysMatches for every def of a tree.
	private readonly Dictionary<ThingDef, bool?> defMemo = new Dictionary<ThingDef, bool?>();

	private bool? DefHasMemo(ThingDef def)
	{
		if (!defMemo.TryGetValue(def, out var has))
		{
			try
			{
				has = DefHas(def);
			}
			catch (Exception)
			{
				has = null;
			}
			defMemo[def] = has;
		}
		return has;
	}

	public override bool Matches(Thing t)
	{
		return ThingHas(t) == Wanted;
	}

	public override bool AlwaysMatches(ThingDef def)
	{
		return DefHasMemo(def) == Wanted;
	}

	public override bool CanEverMatch(ThingDef def)
	{
		bool? has = DefHasMemo(def);
		if (has.HasValue)
		{
			return has.Value == Wanted;
		}
		return true;
	}
}

public abstract class QualityFilterWorker : PropertyFilterWorker
{
	protected override bool? DefHas(ThingDef def)
	{
		return def.HasComp(typeof(CompQuality));
	}

	protected override bool ThingHas(Thing t)
	{
		return t.TryGetComp<CompQuality>() != null;
	}
}

public class SpecialThingFilterWorker_HasQuality : QualityFilterWorker
{
	protected override bool Wanted => true;
}

public class SpecialThingFilterWorker_NoQuality : QualityFilterWorker
{
	protected override bool Wanted => false;
}

// Deterioration does not depend on the material: a stuffed def is judged with its default stuff.
public abstract class DeteriorableFilterWorker : PropertyFilterWorker
{
	protected override bool? DefHas(ThingDef def)
	{
		ThingDef? stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
		if (def.MadeFromStuff && stuff == null)
		{
			return null;
		}
		return def.GetStatValueAbstract(StatDefOf.DeteriorationRate, stuff) > 0f;
	}

	protected override bool ThingHas(Thing t)
	{
		return t.GetStatValue(StatDefOf.DeteriorationRate) > 0f;
	}
}

public class SpecialThingFilterWorker_Deteriorable : DeteriorableFilterWorker
{
	protected override bool Wanted => true;
}

public class SpecialThingFilterWorker_NonDeteriorable : DeteriorableFilterWorker
{
	protected override bool Wanted => false;
}

public abstract class RottableFilterWorker : PropertyFilterWorker
{
	protected override bool? DefHas(ThingDef def)
	{
		CompProperties_Rottable? props = def.GetCompProperties<CompProperties_Rottable>();
		return props != null && props.daysToRotStart > 0f;
	}

	protected override bool ThingHas(Thing t)
	{
		CompRottable? comp = t.TryGetComp<CompRottable>();
		return comp != null && !comp.disabled && comp.PropsRot.daysToRotStart > 0f;
	}
}

public class SpecialThingFilterWorker_Rottable : RottableFilterWorker
{
	protected override bool Wanted => true;
}

public class SpecialThingFilterWorker_NonRottable : RottableFilterWorker
{
	protected override bool Wanted => false;
}

// Flammability does depend on the material: a stuffed def is left to each thing.
public abstract class FlammableFilterWorker : PropertyFilterWorker
{
	protected override bool? DefHas(ThingDef def)
	{
		if (def.MadeFromStuff)
		{
			return null;
		}
		return def.GetStatValueAbstract(StatDefOf.Flammability) > 0f;
	}

	protected override bool ThingHas(Thing t)
	{
		return t.GetStatValue(StatDefOf.Flammability) > 0f;
	}
}

public class SpecialThingFilterWorker_Flammable : FlammableFilterWorker
{
	protected override bool Wanted => true;
}

public class SpecialThingFilterWorker_NonFlammable : FlammableFilterWorker
{
	protected override bool Wanted => false;
}

// Whose animal lay in the corpse is a property of each corpse, never of the def: Useful Filters answered "every
// def" here, so refusing either filter refused every animal corpse.
public abstract class AnimalCorpseFilterWorker : SpecialThingFilterWorker
{
	protected abstract bool Colony { get; }

	public override bool Matches(Thing t)
	{
		if (t is Corpse corpse && corpse.InnerPawn != null)
		{
			return (corpse.InnerPawn.Faction == Faction.OfPlayer) == Colony;
		}
		return false;
	}

	public override bool CanEverMatch(ThingDef def)
	{
		return def.IsCorpse;
	}
}

public class SpecialThingFilterWorker_CorpsesColonyAnimal : AnimalCorpseFilterWorker
{
	protected override bool Colony => true;
}

public class SpecialThingFilterWorker_CorpsesNonColonyAnimal : AnimalCorpseFilterWorker
{
	protected override bool Colony => false;
}

// Two pairs taken over from [UR] Better Filter, under defNames of their own; they load only while Better Filter
// is off, since it brings the same pairs.
public abstract class HitPointsFilterWorker : PropertyFilterWorker
{
	protected override bool? DefHas(ThingDef def)
	{
		return def.useHitPoints;
	}

	protected override bool ThingHas(Thing t)
	{
		return t.def.useHitPoints;
	}
}

public class SpecialThingFilterWorker_HasHitPoints : HitPointsFilterWorker
{
	protected override bool Wanted => true;
}

public class SpecialThingFilterWorker_NoHitPoints : HitPointsFilterWorker
{
	protected override bool Wanted => false;
}

public abstract class StackableFilterWorker : PropertyFilterWorker
{
	protected override bool? DefHas(ThingDef def)
	{
		return def.stackLimit > 1;
	}

	protected override bool ThingHas(Thing t)
	{
		return t.def.stackLimit > 1;
	}
}

public class SpecialThingFilterWorker_Stackable : StackableFilterWorker
{
	protected override bool Wanted => true;
}

public class SpecialThingFilterWorker_NonStackable : StackableFilterWorker
{
	protected override bool Wanted => false;
}
