using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public class PawnSnapshot
{
	public struct ConditionLine
	{
		public string Label;

		public string Part;

		public string IconPath;

		public Color Color;

		public bool Untended;
	}

	public struct NeedLine
	{
		public Need Need;

		public string DefName;

		public string Label;

		public string Pct;

		public string IconPath;
	}

	public struct SkillLine
	{
		public SkillDef Def;

		public string DefName;

		public string Label;

		public string Level;

		public float FillPct;

		public int Passion;

		public bool Disabled;

		public Texture2D? PassionTex;

		public string PassionLabel;
	}

	public struct CapLine
	{
		public PawnCapacityDef? Def;

		public string Label;

		public string Pct;

		public float Value;
	}

	public struct ValueLine
	{
		public Def Def;

		public string Label;

		public string Value;

		public string Tip;
	}

	public struct GearLine
	{
		public Thing Thing;

		public string Label;

		public float HpPct;
	}

	public struct ProductionLine
	{
		public string Label;

		public string Pct;

		public float Fill;

		public string Tip;

		public bool Ready;
	}

	public struct SocialLine
	{
		public string Who;

		public string Rel;

		public string Op;

		public Color OpColor;
	}

	private const int FastTicks = 30;

	private const int SlowTicks = 120;

	private const int MaxConditions = 8;

	public string NameShort = "";

	public string Initial = "?";

	public ThingWithComps? WeaponThing;

	public string SubLine = "";

	public string Activity = "";

	public string Weapon = "";

	public string ArmorLine = "";

	public string MassLine = "";

	public string MoodPctStr = "";

	public string MoodMinorLabel = "";

	public string HealthPctStr = "";

	public string HealthCondition = "";

	public float HealthPct = 1f;

	public bool HealthCritical;

	public string UntendedLabel = "";

	public string UntendedCountStr = "";

	public int Untended;

	public bool Bleeding;

	public string BleedLabel = "";

	public bool BleedingOut;

	public bool HasMood;

	public string GenderAge = "";

	public string TraitsLine = "";

	public string Carrying = "";

	public string Queued = "";

	public string MentalLine = "";

	public bool MentalBad;

	public readonly List<ValueLine> StatLines = new List<ValueLine>(8);

	public readonly List<ValueLine> RecordLines = new List<ValueLine>(8);

	public readonly List<ConditionLine> Conditions = new List<ConditionLine>(8);

	public readonly List<NeedLine> Needs = new List<NeedLine>(8);

	public readonly List<SkillLine> Skills = new List<SkillLine>(12);

	public readonly List<CapLine> Capacities = new List<CapLine>(12);

	public readonly List<GearLine> Apparel = new List<GearLine>(10);

	public readonly List<ProductionLine> Production = new List<ProductionLine>(2);

	public readonly List<SocialLine> Relations = new List<SocialLine>(6);

	public readonly List<string> LogLines = new List<string>(5);

	private Pawn? lastPawn;

	private int nextFastTick;

	private int nextSlowTick;

	private int lastRegVersion;

	private static readonly Dictionary<string, string> NeedIcons = new Dictionary<string, string>
	{
		{ "Mood", "Stat/Mood" },
		{ "Food", "Stat/Food" },
		{ "Rest", "Status/Zzz" },
		{ "Joy", "Common/Note" },
		{ "Beauty", "Stat/Glow" },
		{ "Comfort", "Stat/Wave" },
		{ "Outdoors", "Common/Leaf" },
		{ "Indoors", "Common/Leaf" },
		{ "MechEnergy", "Common/Bolt" }
	};

	private static readonly PropertyInfo? GatherResourceDef = typeof(CompHasGatherableBodyResource).GetProperty("ResourceDef", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? EggProgress = typeof(CompEggLayer).GetField("eggProgress", BindingFlags.Instance | BindingFlags.NonPublic);

	public void Refresh(Pawn pawn)
	{
		bool flag = pawn != lastPawn;
		bool flag2 = Throttle.EveryTicks(ref nextFastTick, 30);
		bool flag3 = lastRegVersion != LayoutDefRegistry.Version;
		if (flag || flag2 || flag3)
		{
			lastPawn = pawn;
			lastRegVersion = LayoutDefRegistry.Version;
			NameShort = pawn.LabelShortCap;
			Initial = (NameShort.NullOrEmpty() ? "?" : NameShort.Substring(0, 1));
			SubLine = BuildSubLine(pawn);
			Activity = SafeReport(pawn);
			ThingWithComps thingWithComps = (WeaponThing = pawn.equipment?.Primary);
			Weapon = ((thingWithComps != null) ? thingWithComps.LabelCap : ((string)"RUI.Inspector.Unarmed".Translate()));
			Need need = pawn.needs?.mood;
			HasMood = need != null;
			if (need != null)
			{
				MoodPctStr = need.CurLevelPercentage.ToStringPercent();
				float f = pawn.mindState?.mentalBreaker?.BreakThresholdMinor ?? 0.35f;
				MoodMinorLabel = "RUI.Inspector.MinorBreak".Translate(f.ToStringPercent());
			}
			else
			{
				MoodPctStr = "";
				MoodMinorLabel = "";
			}
			Pawn_HealthTracker health = pawn.health;
			if (health != null)
			{
				HealthPct = health.summaryHealth.SummaryHealthPercent;
				HealthPctStr = HealthPct.ToStringPercent();
				HealthCondition = HealthUtility.GetGeneralConditionLabel(pawn, shortVersion: true);
				HealthCritical = health.Dead || pawn.Downed;
			}
			else
			{
				HealthPct = 1f;
				HealthPctStr = "";
				HealthCondition = "";
				HealthCritical = false;
			}
			RefreshIdentityValues(pawn);
			RefreshNeeds(pawn);
			RefreshArmorAndMass(pawn);
			RefreshHealth(pawn);
			RefreshSkills(pawn);
			RefreshCapacities(pawn);
			RefreshApparel(pawn);
			RefreshProduction(pawn);
			RefreshStatCells(pawn);
			RefreshRecordCells(pawn);
			if (Throttle.EveryTicks(ref nextSlowTick, 120) || flag)
			{
				RefreshRelations(pawn);
				RefreshLog(pawn);
			}
		}
	}

	private void RefreshNeeds(Pawn pawn)
	{
		Needs.Clear();
		List<Need> list = pawn.needs?.AllNeeds;
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				Need need = list[i];
				string defName = need.def.defName;
				Needs.Add(new NeedLine
				{
					Need = need,
					DefName = defName,
					Label = need.LabelCap,
					Pct = need.CurLevelPercentage.ToStringPercent(),
					IconPath = (NeedIcons.TryGetValue(defName, out string value) ? value : "Stat/BarGraph")
				});
			}
		}
	}

	private void RefreshArmorAndMass(Pawn pawn)
	{
		if (pawn.equipment == null && pawn.apparel == null)
		{
			ArmorLine = "";
			MassLine = "";
			return;
		}
		float statValue = pawn.GetStatValue(StatDefOf.ArmorRating_Sharp);
		float statValue2 = pawn.GetStatValue(StatDefOf.ArmorRating_Blunt);
		ArmorLine = "RUI.Inspector.Armor".Translate(statValue.ToStringPercent(), statValue2.ToStringPercent());
		float num = MassUtility.GearAndInventoryMass(pawn);
		float num2 = MassUtility.Capacity(pawn);
		MassLine = "RUI.Inspector.Mass".Translate(num.ToString("0.#"), num2.ToString("0.#"));
	}

	private void RefreshHealth(Pawn pawn)
	{
		Bleeding = false;
		Untended = 0;
		Conditions.Clear();
		HediffSet hediffSet = pawn.health?.hediffSet;
		if (hediffSet == null)
		{
			UntendedLabel = "";
			UntendedCountStr = "";
			return;
		}
		Bleeding = hediffSet.BleedRateTotal > 0.01f;
		BleedLabel = "";
		if (Bleeding)
		{
			int num = ((ModsConfig.BiotechActive && pawn.genes != null && pawn.genes.HasActiveGene(GeneDefOf.Deathless)) ? int.MaxValue : HealthUtility.TicksUntilDeathDueToBloodLoss(pawn));
			BleedingOut = num < 60000;
			BleedLabel = (BleedingOut ? ((string)"RUI.Inspector.BleedingDeath".Translate("TimeToDeath".Translate(num.ToStringTicksToPeriod()).Resolve())) : ((string)"RUI.Inspector.Bleeding".Translate()));
		}
		else
		{
			BleedingOut = false;
		}
		List<Hediff> hediffs = hediffSet.hediffs;
		for (int i = 0; i < hediffs.Count; i++)
		{
			Hediff hediff = hediffs[i];
			if (hediff.Visible)
			{
				bool flag = hediff.TendableNow();
				if (flag)
				{
					Untended++;
				}
				if (Conditions.Count < 8)
				{
					Conditions.Add(new ConditionLine
					{
						Label = hediff.LabelCap,
						Part = ((hediff.Part != null) ? hediff.Part.LabelCap : ((string)"RUI.Inspector.WholeBody".Translate())),
						IconPath = IconFor(hediff),
						Color = ColorFor(hediff, flag),
						Untended = flag
					});
				}
			}
		}
		UntendedLabel = ((Untended == 1) ? ((string)"RUI.Inspector.WoundNeedsTending".Translate()) : ((string)"RUI.Inspector.WoundsNeedTending".Translate(Untended)));
		UntendedCountStr = Untended.ToString();
	}

	private void RefreshSkills(Pawn pawn)
	{
		Skills.Clear();
		List<SkillRecord> list = pawn.skills?.skills;
		if (list == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			SkillRecord skillRecord = list[i];
			bool totallyDisabled = skillRecord.TotallyDisabled;
			float num = 0f;
			if (!totallyDisabled && skillRecord.Level < 20)
			{
				float num2 = Mathf.Max(skillRecord.XpRequiredForLevelUp, 1f);
				num = Mathf.Clamp01(skillRecord.xpSinceLastLevel / num2);
			}
			Texture2D passionTex = null;
			string passionLabel = "";
			if (!totallyDisabled && skillRecord.passion != Passion.None && VSECompat.Active)
			{
				passionTex = VSECompat.PassionIcon(skillRecord.passion);
				passionLabel = VSECompat.PassionLabel(skillRecord.passion);
			}
			Skills.Add(new SkillLine
			{
				Def = skillRecord.def,
				DefName = skillRecord.def.defName,
				Label = skillRecord.def.LabelCap,
				Level = (totallyDisabled ? "-" : skillRecord.Level.ToString()),
				FillPct = (totallyDisabled ? 0f : Mathf.Clamp01(((float)skillRecord.Level + num) / 20f)),
				Passion = (int)((!totallyDisabled) ? skillRecord.passion : Passion.None),
				Disabled = totallyDisabled,
				PassionTex = passionTex,
				PassionLabel = passionLabel
			});
		}
	}

	private void RefreshCapacities(Pawn pawn)
	{
		Capacities.Clear();
		PawnCapacitiesHandler pawnCapacitiesHandler = pawn.health?.capacities;
		if (pawnCapacitiesHandler == null)
		{
			return;
		}
		List<PawnCapacityDef> allDefsListForReading = DefDatabase<PawnCapacityDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			if (Capacities.Count >= 20)
			{
				break;
			}
			PawnCapacityDef pawnCapacityDef = allDefsListForReading[i];
			if ((pawn.RaceProps.Humanlike ? pawnCapacityDef.showOnHumanlikes : (pawn.RaceProps.IsMechanoid ? pawnCapacityDef.showOnMechanoids : pawnCapacityDef.showOnAnimals)) && PawnCapacityUtility.BodyCanEverDoCapacity(pawn.RaceProps.body, pawnCapacityDef))
			{
				float level = pawnCapacitiesHandler.GetLevel(pawnCapacityDef);
				Capacities.Add(new CapLine
				{
					Def = pawnCapacityDef,
					Label = pawnCapacityDef.LabelCap,
					Pct = level.ToStringPercent(),
					Value = level
				});
			}
		}
	}

	private void RefreshApparel(Pawn pawn)
	{
		Apparel.Clear();
		List<Apparel> list = pawn.apparel?.WornApparel;
		if (list == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (Apparel.Count >= 10)
			{
				break;
			}
			Apparel apparel = list[i];
			Apparel.Add(new GearLine
			{
				Thing = apparel,
				Label = apparel.LabelCap,
				HpPct = ((apparel.MaxHitPoints > 0) ? ((float)apparel.HitPoints / (float)apparel.MaxHitPoints) : 1f)
			});
		}
	}

	private void RefreshProduction(Pawn pawn)
	{
		Production.Clear();
		List<ThingComp> allComps = pawn.AllComps;
		if (allComps == null)
		{
			return;
		}
		for (int i = 0; i < allComps.Count; i++)
		{
			ThingComp thingComp = allComps[i];
			if (thingComp is CompHasGatherableBodyResource compHasGatherableBodyResource)
			{
				string text = SafeCompLine(thingComp);
				if (!text.NullOrEmpty())
				{
					string label = ((GatherResourceDef?.GetValue(compHasGatherableBodyResource) is ThingDef thingDef) ? ((string)thingDef.LabelCap) : text.Split(new char[1] { ':' })[0]);
					AddProduction(label, Mathf.Clamp01(compHasGatherableBodyResource.Fullness), text);
				}
			}
			else if (thingComp is CompEggLayer)
			{
				string text2 = SafeCompLine(thingComp);
				if (!text2.NullOrEmpty())
				{
					float fill = ((EggProgress != null) ? Mathf.Clamp01((float)EggProgress.GetValue(thingComp)) : 0f);
					AddProduction("EggProgress".Translate(), fill, text2);
				}
			}
		}
	}

	private void AddProduction(string label, float fill, string tip)
	{
		Production.Add(new ProductionLine
		{
			Label = label.CapitalizeFirst(),
			Fill = fill,
			Pct = fill.ToStringPercent(),
			Tip = tip,
			Ready = (fill >= 0.999f)
		});
	}

	private static string? SafeCompLine(ThingComp comp)
	{
		try
		{
			return comp.CompInspectStringExtra();
		}
		catch (Exception)
		{
			return null;
		}
	}

	private void RefreshRelations(Pawn pawn)
	{
		Relations.Clear();
		List<DirectPawnRelation> list = pawn.relations?.DirectRelations;
		if (list == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (Relations.Count >= 6)
			{
				break;
			}
			DirectPawnRelation directPawnRelation = list[i];
			if (directPawnRelation.otherPawn != null)
			{
				int num = 0;
				try
				{
					num = pawn.relations.OpinionOf(directPawnRelation.otherPawn);
				}
				catch (Exception)
				{
				}
				Relations.Add(new SocialLine
				{
					Who = directPawnRelation.otherPawn.LabelShortCap,
					Rel = directPawnRelation.def.GetGenderSpecificLabelCap(directPawnRelation.otherPawn),
					Op = ((num > 0) ? ("+" + num) : num.ToString()),
					OpColor = ((num > 0) ? Palette.Good : ((num < 0) ? Palette.Bad : Palette.TextDim))
				});
			}
		}
	}

	private void RefreshLog(Pawn pawn)
	{
		LogLines.Clear();
		try
		{
			List<LogEntry> list = Find.PlayLog?.AllEntries;
			if (list == null)
			{
				return;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (LogLines.Count >= 5)
				{
					break;
				}
				LogEntry logEntry = list[i];
				if (logEntry.Concerns(pawn))
				{
					LogLines.Add(logEntry.ToGameStringFromPOV(pawn));
				}
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Play-log read failed: " + ex.Message, 5351395);
		}
	}

	private void RefreshIdentityValues(Pawn pawn)
	{
		string value = pawn.genes?.XenotypeLabelCap ?? ((string)pawn.def.LabelCap);
		GenderAge = ScratchText.Sb().Append(pawn.gender.GetLabel(pawn.RaceProps.Animal).CapitalizeFirst()).Append(", ")
			.Append(value)
			.Append(", ")
			.Append(pawn.ageTracker?.AgeBiologicalYears ?? 0)
			.ToString();
		List<Trait> list = pawn.story?.traits?.allTraits;
		if (list == null || list.Count == 0)
		{
			TraitsLine = "";
		}
		else
		{
			StringBuilder stringBuilder = ScratchText.Sb();
			for (int i = 0; i < list.Count; i++)
			{
				if (!list[i].Suppressed)
				{
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append(", ");
					}
					stringBuilder.Append(list[i].LabelCap);
				}
			}
			TraitsLine = stringBuilder.ToString();
		}
		Thing thing = pawn.carryTracker?.CarriedThing;
		Carrying = ((thing != null) ? thing.LabelCap : "");
		int valueOrDefault = (pawn.jobs?.jobQueue?.Count).GetValueOrDefault();
		Queued = ((valueOrDefault > 0) ? ((string)"RUI.Inspector.QueuedJobs".Translate(valueOrDefault)) : "");
		if (pawn.MentalState != null)
		{
			MentalLine = pawn.MentalState.InspectLine;
			MentalBad = true;
		}
		else
		{
			Inspiration inspiration = pawn.mindState?.inspirationHandler?.CurState;
			MentalLine = ((inspiration != null) ? ((string)inspiration.def.LabelCap) : "");
			MentalBad = false;
		}
	}

	private void RefreshStatCells(Pawn pawn)
	{
		StatLines.Clear();
		IReadOnlyList<StatDef> stats = LayoutDefRegistry.Stats;
		for (int i = 0; i < stats.Count; i++)
		{
			StatDef statDef = stats[i];
			try
			{
				if (statDef.Worker != null && !statDef.Worker.IsDisabledFor(pawn) && statDef.Worker.ShouldShowFor(StatRequest.For(pawn)))
				{
					string text = statDef.ValueToString(pawn.GetStatValue(statDef));
					StatLines.Add(new ValueLine
					{
						Def = statDef,
						Label = statDef.LabelCap,
						Value = text,
						Tip = statDef.LabelCap + ": " + text
					});
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[Radius UI - Inspector] Stat cell read failed for " + statDef.defName + ": " + ex.Message, 5351399);
			}
		}
	}

	private void RefreshRecordCells(Pawn pawn)
	{
		RecordLines.Clear();
		if (pawn.records == null)
		{
			return;
		}
		IReadOnlyList<RecordDef> records = LayoutDefRegistry.Records;
		for (int i = 0; i < records.Count; i++)
		{
			RecordDef recordDef = records[i];
			try
			{
				string text = ((recordDef.type == RecordType.Time) ? pawn.records.GetAsInt(recordDef).ToStringTicksToPeriod() : ((recordDef.type == RecordType.Int) ? pawn.records.GetAsInt(recordDef).ToString() : pawn.records.GetValue(recordDef).ToString("0.##")));
				RecordLines.Add(new ValueLine
				{
					Def = recordDef,
					Label = recordDef.LabelCap,
					Value = text,
					Tip = recordDef.LabelCap + ": " + text
				});
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[Radius UI - Inspector] Record cell read failed for " + recordDef.defName + ": " + ex.Message, 5351400);
			}
		}
	}

	private static string BuildSubLine(Pawn pawn)
	{
		string text = pawn.story?.TitleCap;
		if (text.NullOrEmpty() && pawn.kindDef != null)
		{
			text = pawn.kindDef.LabelCap;
		}
		string text2 = pawn.Faction?.Name ?? "";
		if (text.NullOrEmpty())
		{
			return text2;
		}
		if (text2.NullOrEmpty())
		{
			return text;
		}
		return text + " - " + text2;
	}

	private static string SafeReport(Pawn pawn)
	{
		try
		{
			string str = pawn.jobs?.curDriver?.GetReport();
			if (!str.NullOrEmpty())
			{
				return str.CapitalizeFirst();
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Job report failed: " + ex.Message, 5351396);
		}
		return "RUI.Inspector.Idle".Translate();
	}

	private static string IconFor(Hediff h)
	{
		if (h.Bleeding)
		{
			return "Marker/Blood";
		}
		if (h is Hediff_MissingPart)
		{
			return "Medical/Missing";
		}
		if (h.def.makesSickThought)
		{
			return "Marker/Virus";
		}
		if (h is Hediff_Injury)
		{
			return "Medical/Injury";
		}
		return "Medical/Illness";
	}

	private static Color ColorFor(Hediff h, bool tendable)
	{
		if (h.Bleeding)
		{
			return Palette.Bad;
		}
		if (tendable)
		{
			return Palette.Warn;
		}
		if (h is Hediff_Injury hd && hd.IsPermanent())
		{
			return Palette.TextDim;
		}
		return Palette.Ink;
	}
}
