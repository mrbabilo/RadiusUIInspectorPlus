using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

// Detailed tooltips for the pawn cells, content after RimHUD's (Jaxe), colors from the Radius
// palette: health by body part, thoughts, bio, traits, need and skill stats. Text is built only while the cell is hovered, and kept for a few
// frames because some stats are costly.
internal static class RichTips
{
	internal const int HealthId = 52710101;

	internal const int MoodId = 52710102;

	internal const int BioId = 52710103;

	internal const int TraitsId = 52710104;

	internal const int AnimalId = 52710105;

	internal const int ActivityId = 52710106;

	// Per-def ids: base + shortHash (0-65535), so the bases are 100000 apart.
	private const int NeedBase = 61000000;

	private const int SkillBase = 61100000;

	private const int CapacityBase = 61200000;

	private const int CacheFrames = 30;

	private static readonly Func<Pawn, bool, IEnumerable<IGrouping<BodyPartRecord, Hediff>>>? VisibleHediffGroups = Bind<Func<Pawn, bool, IEnumerable<IGrouping<BodyPartRecord, Hediff>>>>(typeof(HealthCardUtility), "VisibleHediffGroupsInOrder");

	private static readonly Func<SkillRecord, string>? SkillDescription = Bind<Func<SkillRecord, string>>(typeof(SkillUI), "GetSkillDescription");

	private static readonly string[] CommonSkillStats = new string[2] { "WorkSpeedGlobal", "GeneralLaborSpeed" };

	private static readonly Dictionary<string, string[]> SkillStats = new Dictionary<string, string[]>
	{
		{
			"Shooting",
			new string[2] { "ShootingAccuracyPawn", "AimingDelayFactor" }
		},
		{
			"Melee",
			new string[3] { "MeleeDPS", "MeleeHitChance", "MeleeDodgeChance" }
		},
		{
			"Construction",
			new string[4] { "ConstructSuccessChance", "ConstructionSpeed", "FixBrokenDownBuildingSuccessChance", "SmoothingSpeed" }
		},
		{
			"Mining",
			new string[3] { "MiningYield", "MiningSpeed", "DeepDrillingSpeed" }
		},
		{
			"Cooking",
			new string[5] { "CookSpeed", "FoodPoisonChance", "ButcheryFleshEfficiency", "ButcheryFleshSpeed", "DrugCookingSpeed" }
		},
		{
			"Plants",
			new string[3] { "PlantWorkSpeed", "PlantHarvestYield", "PruningSpeed" }
		},
		{
			"Animals",
			new string[5] { "AnimalGatherSpeed", "AnimalGatherYield", "TameAnimalChance", "TrainAnimalChance", "HuntingStealth" }
		},
		{
			"Crafting",
			new string[3] { "SmeltingSpeed", "ButcheryMechanoidSpeed", "ButcheryMechanoidEfficiency" }
		},
		{
			"Medicine",
			new string[4] { "MedicalOperationSpeed", "MedicalSurgerySuccessChance", "MedicalTendSpeed", "MedicalTendQuality" }
		},
		{
			"Social",
			new string[5] { "NegotiationAbility", "TradePriceImprovement", "SocialImpact", "ConversionPower", "SuppressionPower" }
		},
		{
			"Intellectual",
			new string[2] { "ResearchSpeed", "HackingSpeed" }
		}
	};

	private static readonly List<Thought> thoughtGroups = new List<Thought>();

	private static readonly List<Thought> thoughtsOfGroup = new List<Thought>();

	private static int cacheId;

	private static Pawn? cachePawn;

	private static int cacheFrame = -9999;

	private static string cacheText = "";

	internal static int NeedId(NeedDef def)
	{
		return NeedBase + def.shortHash;
	}

	internal static int SkillId(SkillDef def)
	{
		return SkillBase + def.shortHash;
	}

	internal static int CapacityId(PawnCapacityDef def)
	{
		return CapacityBase + def.shortHash;
	}

	private static T? Bind<T>(Type type, string method) where T : Delegate
	{
		try
		{
			MethodInfo? methodInfo = AccessTools.Method(type, method);
			return (methodInfo == null) ? null : AccessTools.MethodDelegate<T>(methodInfo);
		}
		catch (Exception ex)
		{
			Log.Warning("[Radius UI Inspector] " + type.Name + "." + method + " unavailable, its tooltip is reduced: " + ex.Message);
			return null;
		}
	}

	// Tooltip for a hovered cell: the detailed text, then the cell's own hint (dimmed) when it has one.
	internal static void Region(Rect rect, Pawn? pawn, int id, Func<Pawn, string?>? body, string? hint = null)
	{
		if (!Mouse.IsOver(rect))
		{
			return;
		}
		if (pawn == null || body == null)
		{
			if (!hint.NullOrEmpty())
			{
				TooltipHandler.TipRegion(rect, hint);
			}
			return;
		}
		TooltipHandler.TipRegion(rect, new TipSignal(() => Compose(pawn, id, body, hint), id));
	}

	// The cache holds the body only: cells sharing an id (mood bar and mental line) differ by their hint.
	private static string Compose(Pawn pawn, int id, Func<Pawn, string?> body, string? hint)
	{
		if (id != cacheId || pawn != cachePawn || Time.frameCount - cacheFrame >= CacheFrames)
		{
			try
			{
				cacheText = body(pawn) ?? "";
			}
			catch (Exception ex)
			{
				cacheText = "";
				Log.ErrorOnce("[Radius UI Inspector] Tooltip failed: " + ex, id);
			}
			cacheId = id;
			cachePawn = pawn;
			cacheFrame = Time.frameCount;
		}
		if (hint.NullOrEmpty())
		{
			return cacheText;
		}
		return (cacheText.Length == 0) ? hint! : (cacheText + "\n\n" + hint.Colorize(Palette.TextDim));
	}

	private static void AppendValue(StringBuilder sb, string? label, string? value)
	{
		if (!label.NullOrEmpty() && !value.NullOrEmpty())
		{
			sb.AppendLine(label.Colorize(RadiusTheme.Accent) + ": " + value);
		}
	}

	private static void AppendStat(StringBuilder sb, Pawn pawn, StatDef? def)
	{
		if (def?.Worker == null || def.Worker.IsDisabledFor(pawn))
		{
			return;
		}
		try
		{
			AppendValue(sb, def.LabelCap, def.ValueToString(pawn.GetStatValue(def)));
		}
		catch (Exception)
		{
		}
	}

	private static void AppendStat(StringBuilder sb, Pawn pawn, string defName)
	{
		AppendStat(sb, pawn, DefDatabase<StatDef>.GetNamedSilentFail(defName));
	}

	private static string? Done(StringBuilder sb)
	{
		string text = sb.ToString().Trim();
		return (text.Length == 0) ? null : text;
	}

	internal static string? Health(Pawn pawn)
	{
		if (pawn.health?.hediffSet == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		IEnumerable<Hediff> enumerable = (VisibleHediffGroups != null) ? VisibleHediffGroups(pawn, arg2: true).SelectMany((IGrouping<BodyPartRecord, Hediff> g) => g) : pawn.health.hediffSet.hediffs;
		foreach (Hediff item in enumerable)
		{
			if (item.Visible)
			{
				Color color = ((!item.def.isBad) ? Palette.Good : ((item.IsPermanent() || item.FullyImmune()) ? Palette.TextDim : ((item.def.IsAddiction || item.IsTended()) ? Palette.Warn : Palette.Bad)));
				string text = item.Part?.LabelCap ?? "WholeBody".TranslateSimple();
				stringBuilder.AppendLine((text + ": " + item.LabelCap).Colorize(color));
			}
		}
		if (stringBuilder.Length == 0)
		{
			stringBuilder.AppendLine("NoHealthConditions".TranslateSimple().CapitalizeFirst().Colorize(Palette.TextDim));
		}
		return Done(stringBuilder);
	}

	internal static string? Mood(Pawn pawn)
	{
		Need_Mood need_Mood = pawn.needs?.mood;
		if (need_Mood?.thoughts == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		thoughtGroups.Clear();
		PawnNeedsUIUtility.GetThoughtGroupsInDisplayOrder(need_Mood, thoughtGroups);
		foreach (Thought thoughtGroup in thoughtGroups)
		{
			thoughtsOfGroup.Clear();
			need_Mood.thoughts.GetMoodThoughts(thoughtGroup, thoughtsOfGroup);
			int num = Mathf.Max(1, thoughtsOfGroup.Count);
			float num2 = thoughtGroup.MoodOffset() * (float)num;
			Color color = ((num2 <= -10f) ? Palette.Bad : ((num2 < 0f) ? Palette.Warn : ((num2 > 0f) ? Palette.Good : Palette.TextDim)));
			string text = thoughtGroup.LabelCap;
			if (num > 1)
			{
				text = text + " x" + num;
			}
			stringBuilder.AppendLine((text + ": " + num2.ToString("+0;-0;0")).Colorize(color));
		}
		if (pawn.Inspired && pawn.Inspiration != null)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine(pawn.Inspiration.InspectLine.Colorize(Palette.Good));
		}
		return Done(stringBuilder);
	}

	internal static string? Bio(Pawn pawn)
	{
		if (pawn.RaceProps != null && pawn.RaceProps.Animal)
		{
			return Animal(pawn);
		}
		if (pawn.story == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		AppendValue(stringBuilder, "Title".TranslateSimple(), pawn.story.TitleCap);
		AppendValue(stringBuilder, "Faction".TranslateSimple(), pawn.Faction?.Name);
		if (ModsConfig.IdeologyActive)
		{
			AppendValue(stringBuilder, "RUI.Inspector.TipIdeo".Translate(), pawn.Ideo?.name);
		}
		if (ModsConfig.BiotechActive)
		{
			AppendValue(stringBuilder, "RUI.Inspector.TipXenotype".Translate(), pawn.genes?.XenotypeLabelCap);
		}
		stringBuilder.AppendLine();
		AppendValue(stringBuilder, "Childhood".TranslateSimple(), pawn.story.GetBackstory(BackstorySlot.Childhood)?.TitleCapFor(pawn.gender));
		AppendValue(stringBuilder, "Adulthood".TranslateSimple(), pawn.story.GetBackstory(BackstorySlot.Adulthood)?.TitleCapFor(pawn.gender));
		WorkTags disabledWorkTagsBackstoryAndTraits = pawn.story.DisabledWorkTagsBackstoryAndTraits;
		if (disabledWorkTagsBackstoryAndTraits != WorkTags.None)
		{
			stringBuilder.AppendLine();
			AppendValue(stringBuilder, "IncapableOf".TranslateSimple().TrimEnd(':', ' '), (from WorkTags tag in disabledWorkTagsBackstoryAndTraits.GetAllSelectedItems<WorkTags>()
				where tag != WorkTags.None
				select tag.LabelTranslated().CapitalizeFirst()).ToCommaList().Colorize(Palette.TextDim));
		}
		stringBuilder.AppendLine();
		AppendStat(stringBuilder, pawn, StatDefOf.PsychicSensitivity);
		if (ModsConfig.RoyaltyActive)
		{
			AppendValue(stringBuilder, "MeditationFocuses".TranslateSimple().CapitalizeFirst(), MeditationUtility.FocusTypesAvailableForPawnString(pawn).CapitalizeFirst());
			AppendStat(stringBuilder, pawn, StatDefOf.MeditationFocusGain);
		}
		if (ModsConfig.IdeologyActive)
		{
			AppendStat(stringBuilder, pawn, StatDefOf.CertaintyLossFactor);
		}
		return Done(stringBuilder);
	}

	internal static string? Traits(Pawn pawn)
	{
		List<Trait> list = pawn.story?.traits?.TraitsSorted;
		if (list == null || list.Count == 0)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (Trait item in list)
		{
			stringBuilder.AppendLine(item.LabelCap.Colorize(RadiusTheme.Accent));
			stringBuilder.AppendLine(item.TipString(pawn).StripTags());
			stringBuilder.AppendLine();
		}
		return Done(stringBuilder);
	}

	internal static string? Animal(Pawn pawn)
	{
		RaceProperties raceProps = pawn.RaceProps;
		if (raceProps == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		AppendValue(stringBuilder, "RUI.Inspector.TipTrainability".Translate(), raceProps.trainability?.LabelCap);
		AppendValue(stringBuilder, "TrainingDecayInterval".TranslateSimple(), TrainableUtility.DegradationPeriodTicks(pawn.def).ToStringTicksToDays());
		if (!TrainableUtility.TamenessCanDecay(pawn.def))
		{
			stringBuilder.AppendLine("TamenessWillNotDecay".TranslateSimple().Colorize(Palette.TextDim));
		}
		AppendValue(stringBuilder, "RUI.Inspector.TipPetness".Translate(), raceProps.petness.ToStringPercent());
		AppendValue(stringBuilder, "Diet".TranslateSimple(), raceProps.ResolvedDietCategory.ToStringHuman());
		AppendValue(stringBuilder, "Master".TranslateSimple(), pawn.playerSettings?.Master?.LabelShort);
		return Done(stringBuilder);
	}

	internal static string? Activity(Pawn pawn)
	{
		WorkTypeDef workTypeDef = pawn.CurJob?.workGiverDef?.workType;
		if (workTypeDef == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		AppendValue(stringBuilder, "RUI.Inspector.TipWorkType".Translate(), workTypeDef.labelShort.CapitalizeFirst());
		if (!workTypeDef.relevantSkills.NullOrEmpty())
		{
			AppendValue(stringBuilder, "RUI.Inspector.TipRelevantSkills".Translate(), workTypeDef.relevantSkills.Select((SkillDef s) => s.LabelCap.ToString()).ToCommaList());
		}
		return Done(stringBuilder);
	}

	internal static string? Need(Pawn pawn, NeedDef def)
	{
		Need need = pawn.needs?.TryGetNeed(def);
		if (need == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine(need.GetTipString());
		stringBuilder.AppendLine();
		if (def == NeedDefOf.Food)
		{
			if (pawn.RaceProps != null)
			{
				AppendValue(stringBuilder, "Diet".TranslateSimple(), pawn.RaceProps.foodType.ToHumanString().CapitalizeFirst());
			}
			AppendStat(stringBuilder, pawn, StatDefOf.EatingSpeed);
			AppendStat(stringBuilder, pawn, "BedHungerRateFactor");
		}
		else if (def == NeedDefOf.Rest)
		{
			AppendStat(stringBuilder, pawn, StatDefOf.RestRateMultiplier);
		}
		else if (def.defName == "Joy")
		{
			AppendNeedLevel(stringBuilder, pawn, "Beauty");
			AppendNeedLevel(stringBuilder, pawn, "Comfort");
			AppendNeedLevel(stringBuilder, pawn, "Outdoors");
		}
		return Done(stringBuilder);
	}

	private static void AppendNeedLevel(StringBuilder sb, Pawn pawn, string defName)
	{
		NeedDef namedSilentFail = DefDatabase<NeedDef>.GetNamedSilentFail(defName);
		if (namedSilentFail != null)
		{
			AppendValue(sb, namedSilentFail.LabelCap, pawn.needs?.TryGetNeed(namedSilentFail)?.CurLevelPercentage.ToStringPercent());
		}
	}

	internal static string? Skill(Pawn pawn, SkillDef def, string? passion)
	{
		SkillRecord skillRecord = pawn.skills?.GetSkill(def);
		if (skillRecord == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine((SkillDescription != null) ? SkillDescription(skillRecord) : ((string)def.LabelCap));
		if (!passion.NullOrEmpty())
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine(passion);
		}
		if (skillRecord.TotallyDisabled)
		{
			return Done(stringBuilder);
		}
		stringBuilder.AppendLine();
		for (int i = 0; i < CommonSkillStats.Length; i++)
		{
			AppendStat(stringBuilder, pawn, CommonSkillStats[i]);
		}
		if (SkillStats.TryGetValue(def.defName, out string[] value))
		{
			stringBuilder.AppendLine();
			for (int j = 0; j < value.Length; j++)
			{
				AppendStat(stringBuilder, pawn, value[j]);
			}
		}
		return Done(stringBuilder);
	}

	internal static string? Capacity(Pawn pawn, PawnCapacityDef def)
	{
		return (pawn.health?.capacities == null) ? null : HealthCardUtility.GetPawnCapacityTip(pawn, def);
	}
}
