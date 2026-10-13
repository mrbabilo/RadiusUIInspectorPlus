using System;
using System.Collections.Generic;
using HarmonyLib;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public static class ControlsPanel
{
	public const float BarH = 18f;

	public const float RowGap = 4f;

	private static readonly Dictionary<string, string> truncCache = new Dictionary<string, string>();

	private static readonly Func<Pawn_TrainingTracker, TrainableDef, int>? GetStepsFast = AccessTools.MethodDelegate<Func<Pawn_TrainingTracker, TrainableDef, int>>(AccessTools.Method(typeof(Pawn_TrainingTracker), "GetSteps"));

	private static string lastInspectSrc = string.Empty;

	private static string[] lastInspectLines = new string[0];

	public static bool SupportsArea(Pawn pawn)
	{
		if (pawn == null)
		{
			return false;
		}
		Pawn_PlayerSettings playerSettings = pawn.playerSettings;
		if (playerSettings == null || !playerSettings.SupportsAllowedAreas)
		{
			return false;
		}
		if (pawn.Faction != Faction.OfPlayer || pawn.HostFaction != null)
		{
			return false;
		}
		if (pawn.IsMutant && !pawn.mutant.Def.respectsAllowedArea)
		{
			return false;
		}
		return true;
	}

	public static float DrawArea(Pawn pawn, float x, float w, float y)
	{
		if (!SupportsArea(pawn))
		{
			return 0f;
		}
		Rect rect = new Rect(x, y, w, 18f);
		Area areaRestrictionInPawnCurrentMap = pawn.playerSettings.AreaRestrictionInPawnCurrentMap;
		SwatchRow(rect, "RUI.Inspector.CtlArea".Translate(), AreaUtility.AreaAllowedLabel(pawn), areaRestrictionInPawnCurrentMap?.Color ?? Palette.TextFaint);
		if (Mouse.IsOver(rect))
		{
			areaRestrictionInPawnCurrentMap?.MarkForDraw();
		}
		if (Widgets.ButtonInvisible(rect))
		{
			AreaUtility.MakeAllowedAreaListFloatMenu(delegate(Area a)
			{
				pawn.playerSettings.AreaRestrictionInPawnCurrentMap = a;
			}, addNullAreaOption: true, addManageOption: true, pawn.Map);
			InspectorCore.Poke();
		}
		return 22f;
	}

	public static bool SupportsTimetable(Pawn pawn)
	{
		if (pawn?.timetable != null)
		{
			return !pawn.IsPrisonerOfColony;
		}
		return false;
	}

	public static float DrawTimetable(Pawn pawn, float x, float w, float y)
	{
		if (!SupportsTimetable(pawn))
		{
			return 0f;
		}
		Rect rect = new Rect(x, y, w, 18f);
		TimeAssignmentDef currentAssignment = pawn.timetable.CurrentAssignment;
		SwatchRow(rect, "RUI.Inspector.CtlTimetable".Translate(), currentAssignment.LabelCap, currentAssignment.color);
		if (Widgets.ButtonInvisible(rect))
		{
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			List<TimeAssignmentDef> allDefsListForReading = DefDatabase<TimeAssignmentDef>.AllDefsListForReading;
			int hour = GenLocalDate.HourOfDay(pawn);
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				TimeAssignmentDef d = allDefsListForReading[i];
				list.Add(new FloatMenuOption(d.LabelCap, delegate
				{
					pawn.timetable.SetAssignment(hour, d);
					InspectorCore.Poke();
				}));
			}
			if (list.Count > 0)
			{
				Find.WindowStack.Add(new FloatMenu(list));
			}
		}
		return 22f;
	}

	public static bool SupportsPolicies(Pawn pawn)
	{
		if (pawn != null)
		{
			if (pawn.outfits == null && pawn.foodRestriction == null && pawn.drugs == null)
			{
				return pawn.reading != null;
			}
			return true;
		}
		return false;
	}

	public static float DrawPolicies(Pawn pawn, float x, float w, float y)
	{
		float num = y;
		if (pawn?.outfits != null)
		{
			ApparelPolicy currentApparelPolicy = pawn.outfits.CurrentApparelPolicy;
			if (PolicyRow(x, w, num, "RUI.Inspector.CtlApparel".Translate(), (currentApparelPolicy != null) ? currentApparelPolicy.label.CapitalizeFirst() : "-"))
			{
				OpenPolicyMenu(Current.Game.outfitDatabase.AllOutfits, delegate(ApparelPolicy sel)
				{
					pawn.outfits.CurrentApparelPolicy = sel;
				}, (ApparelPolicy o) => o.label.CapitalizeFirst());
			}
			num += 22f;
		}
		if (pawn?.foodRestriction != null)
		{
			FoodPolicy currentFoodPolicy = pawn.foodRestriction.CurrentFoodPolicy;
			if (PolicyRow(x, w, num, "RUI.Inspector.CtlFood".Translate(), (currentFoodPolicy != null) ? currentFoodPolicy.label.CapitalizeFirst() : "-"))
			{
				OpenPolicyMenu(Current.Game.foodRestrictionDatabase.AllFoodRestrictions, delegate(FoodPolicy sel)
				{
					pawn.foodRestriction.CurrentFoodPolicy = sel;
				}, (FoodPolicy o) => o.label.CapitalizeFirst());
			}
			num += 22f;
		}
		if (pawn?.drugs != null)
		{
			DrugPolicy currentPolicy = pawn.drugs.CurrentPolicy;
			if (PolicyRow(x, w, num, "RUI.Inspector.CtlDrug".Translate(), (currentPolicy != null) ? currentPolicy.label.CapitalizeFirst() : "-"))
			{
				OpenPolicyMenu(Current.Game.drugPolicyDatabase.AllPolicies, delegate(DrugPolicy sel)
				{
					pawn.drugs.CurrentPolicy = sel;
				}, (DrugPolicy o) => o.label.CapitalizeFirst());
			}
			num += 22f;
		}
		if (pawn?.reading != null)
		{
			ReadingPolicy currentPolicy2 = pawn.reading.CurrentPolicy;
			if (PolicyRow(x, w, num, "RUI.Inspector.CtlReading".Translate(), (currentPolicy2 != null) ? currentPolicy2.label.CapitalizeFirst() : "-"))
			{
				OpenPolicyMenu(Current.Game.readingPolicyDatabase.AllReadingPolicies, delegate(ReadingPolicy sel)
				{
					pawn.reading.CurrentPolicy = sel;
				}, (ReadingPolicy o) => o.label.CapitalizeFirst());
			}
			num += 22f;
		}
		return num - y;
	}

	private static void OpenPolicyMenu<T>(List<T> all, Action<T> set, Func<T, string> label) where T : class
	{
		if (all == null || all.Count == 0)
		{
			return;
		}
		List<FloatMenuOption> list = new List<FloatMenuOption>(all.Count);
		for (int i = 0; i < all.Count; i++)
		{
			T item = all[i];
			list.Add(new FloatMenuOption(label(item), delegate
			{
				set(item);
				InspectorCore.Poke();
			}));
		}
		Find.WindowStack.Add(new FloatMenu(list));
	}

	private static bool PolicyRow(float x, float w, float y, string label, string value)
	{
		Rect rect = new Rect(x, y, w, 18f);
		SwatchRow(rect, label, value, null);
		bool num = Widgets.ButtonInvisible(rect);
		if (num)
		{
			InspectorCore.Poke();
		}
		return num;
	}

	private static void SwatchRow(Rect r, string label, string value, Color? swatch)
	{
		bool flag = Mouse.IsOver(r);
		CardChrome.Rounded(r, flag ? Palette.Surface3 : Palette.Surface2, 8f);
		float num = r.x + 8f;
		if (swatch.HasValue)
		{
			Rect rect = new Rect(r.x + 7f, r.y + r.height * 0.5f - 5f, 10f, 10f);
			CardChrome.Rounded(rect, swatch.Value, 3f);
			CardChrome.Outline(rect, Palette.Border, 1f, 3f);
			num = rect.xMax + 7f;
		}
		float num2 = r.xMax - 20f;
		float num3 = Mathf.Min(RadiusFont.Size(label).x + 4f, r.width * 0.5f);
		float num4 = num2 - (num + num3 + 4f);
		if (num4 < 56f)
		{
			num3 = 0f;
			num4 = num2 - num;
		}
		else
		{
			RadiusFont.Label(new Rect(num, r.y, num3, r.height), label, GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		}
		if (num4 > 8f)
		{
			RadiusFont.Label(new Rect(num2 - num4, r.y, num4, r.height), value.Truncate(num4, truncCache), GameFont.Tiny, heading: false, Palette.Ink, TextAnchor.MiddleRight, wrap: false);
		}
		TooltipHandler.TipRegion(r, label + ": " + value);
		IconSet.Get("Action/ChevronDown").Draw(new Rect(r.xMax - 16f, r.y + r.height * 0.5f - 5f, 10f, 10f), Palette.TextDim);
	}

	public static float DrawMechEnergy(Pawn pawn, float x, float w, float y)
	{
		Need need = pawn?.needs?.energy;
		if (need == null)
		{
			return 0f;
		}
		float curLevelPercentage = need.CurLevelPercentage;
		Rect bar = new Rect(x, y, w, 18f);
		InspectorPanel.SuiteBar(bar, curLevelPercentage, (curLevelPercentage > 0.4f) ? Palette.Good : ((curLevelPercentage > 0.15f) ? Palette.Warn : Palette.Bad), -1f, -1f);
		InspectorPanel.BarInnerLabels(bar, need.LabelCap, curLevelPercentage.ToStringPercent(), curLevelPercentage);
		return 22f;
	}

	public static bool SupportsWorkMode(Pawn pawn)
	{
		if (pawn != null && pawn.RaceProps != null && pawn.RaceProps.IsMechanoid && pawn.Faction == Faction.OfPlayer)
		{
			return pawn.GetOverseer() != null;
		}
		return false;
	}

	public static float DrawWorkMode(Pawn pawn, float x, float w, float y)
	{
		if (!SupportsWorkMode(pawn))
		{
			return 0f;
		}
		MechanitorControlGroup group = pawn.GetMechControlGroup();
		float num = y;
		if (group != null)
		{
			if (PolicyRow(x, w, num, "RUI.Inspector.CtlWorkMode".Translate(), (group.WorkMode != null) ? ((string)group.WorkMode.LabelCap) : "-"))
			{
				List<MechWorkModeDef> allDefsListForReading = DefDatabase<MechWorkModeDef>.AllDefsListForReading;
				List<FloatMenuOption> list = new List<FloatMenuOption>(allDefsListForReading.Count);
				for (int i = 0; i < allDefsListForReading.Count; i++)
				{
					MechWorkModeDef d = allDefsListForReading[i];
					list.Add(new FloatMenuOption(d.LabelCap, delegate
					{
						group.SetWorkMode(d);
						InspectorCore.Poke();
					}, d.uiIcon, Color.white));
				}
				Find.WindowStack.Add(new FloatMenu(list));
			}
			num += 22f;
		}
		Pawn overseer = pawn.GetOverseer();
		Rect rect = new Rect(x, num, w, 18f);
		SwatchRow(rect, "RUI.Inspector.CtlOverseer".Translate(), overseer.LabelShortCap, null);
		if (Widgets.ButtonInvisible(rect))
		{
			CameraJumper.TryJumpAndSelect(overseer);
			InspectorCore.Poke();
		}
		num += 22f;
		return num - y;
	}

	public static bool SupportsResistance(Pawn pawn)
	{
		if (pawn != null && pawn.IsPrisonerOfColony)
		{
			return pawn.guest != null;
		}
		return false;
	}

	public static float DrawResistance(Pawn pawn, float x, float w, float y)
	{
		if (!SupportsResistance(pawn))
		{
			return 0f;
		}
		Pawn_GuestTracker guest = pawn.guest;
		float num = y;
		if (guest.resistance >= 0f)
		{
			Rect r = new Rect(x, num, w, 18f);
			string value = guest.resistance.ToString("0.0");
			if (!pawn.guest.Recruitable)
			{
				value = "RUI.Inspector.NonRecruitable".Translate();
			}
			SwatchRow(r, "RUI.Inspector.CtlResistance".Translate(), value, (guest.resistance <= 0f) ? new Color?(Palette.Good) : ((Color?)null));
			num += 22f;
		}
		if (ModsConfig.IdeologyActive && guest.will >= 0f)
		{
			SwatchRow(new Rect(x, num, w, 18f), "RUI.Inspector.CtlWill".Translate(), guest.will.ToString("0.0"), (guest.will <= 0f) ? new Color?(Palette.Good) : ((Color?)null));
			num += 22f;
		}
		PrisonerInteractionModeDef exclusiveInteractionMode = guest.ExclusiveInteractionMode;
		if (exclusiveInteractionMode != null)
		{
			SwatchRow(new Rect(x, num, w, 18f), "RUI.Inspector.CtlInteraction".Translate(), exclusiveInteractionMode.LabelCap, null);
			num += 22f;
		}
		return num - y;
	}

	public static bool SupportsTraining(Pawn pawn)
	{
		if (pawn != null && pawn.training != null && pawn.Faction == Faction.OfPlayer && pawn.RaceProps != null)
		{
			return pawn.RaceProps.trainability != null;
		}
		return false;
	}

	public static float DrawTraining(Pawn pawn, float x, float w, float y)
	{
		if (!SupportsTraining(pawn))
		{
			return 0f;
		}
		List<TrainableDef> trainableDefsInListOrder = TrainableUtility.TrainableDefsInListOrder;
		float num = y;
		for (int i = 0; i < trainableDefsInListOrder.Count; i++)
		{
			TrainableDef trainableDef = trainableDefsInListOrder[i];
			bool visible;
			AcceptanceReport acceptanceReport = pawn.training.CanAssignToTrain(trainableDef, out visible);
			if (!visible)
			{
				continue;
			}
			Rect rect = new Rect(x, num, w, 18f);
			bool wanted = pawn.training.GetWanted(trainableDef);
			bool flag = Mouse.IsOver(rect);
			CardChrome.Rounded(rect, flag ? Palette.Surface3 : Palette.Surface2, 8f);
			RadiusFont.Label(new Rect(rect.x + 8f, rect.y, rect.width * 0.5f - 8f, rect.height), trainableDef.LabelCap, GameFont.Tiny, heading: false, acceptanceReport.Accepted ? Palette.TextDim : Palette.TextFaint, TextAnchor.MiddleLeft, wrap: false);
			int num2 = ((GetStepsFast != null) ? GetStepsFast(pawn.training, trainableDef) : 0);
			float num3 = rect.xMax - 26f - (float)trainableDef.steps * 8f;
			for (int j = 0; j < trainableDef.steps; j++)
			{
				CardChrome.Pill(new Rect(num3 + (float)j * 8f, rect.y + rect.height * 0.5f - 2.5f, 5f, 5f), (j < num2) ? RadiusTheme.Accent : Palette.Surface3);
			}
			Rect rect2 = new Rect(rect.xMax - 20f, rect.y + 2f, 14f, 14f);
			if (acceptanceReport.Accepted)
			{
				CardChrome.Rounded(rect2, wanted ? RadiusTheme.Accent : Palette.Surface3, 4f);
				if (wanted)
				{
					IconSet.Get("Action/Check").Draw(rect2.ContractedBy(2f), Palette.InkOnAccent);
				}
				if (Widgets.ButtonInvisible(rect))
				{
					pawn.training.SetWantedRecursive(trainableDef, !wanted);
					InspectorCore.Poke();
				}
			}
			TooltipHandler.TipRegion(rect, acceptanceReport.Accepted ? ((TipSignal)string.Concat(trainableDef.LabelCap + ": ", num2.ToString(), " / ", trainableDef.steps.ToString())) : ((TipSignal)(trainableDef.LabelCap + ": " + acceptanceReport.Reason)));
			num += 22f;
		}
		return num - y;
	}

	public static bool SupportsMaster(Pawn pawn)
	{
		if (pawn != null && pawn.playerSettings != null && pawn.training != null && pawn.Faction == Faction.OfPlayer && pawn.RaceProps != null)
		{
			return pawn.RaceProps.Animal;
		}
		return false;
	}

	public static float DrawMaster(Pawn pawn, float x, float w, float y)
	{
		if (!SupportsMaster(pawn))
		{
			return 0f;
		}
		float num = y;
		if (pawn.training.CanAssignToTrain(TrainableDefOf.Obedience, out var visible).Accepted && visible)
		{
			if (PolicyRow(x, w, num, "RUI.Inspector.CtlMaster".Translate(), TrainableUtility.MasterString(pawn)))
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>
				{
					new FloatMenuOption("NoneLower".Translate().CapitalizeFirst(), delegate
					{
						pawn.playerSettings.Master = null;
						InspectorCore.Poke();
					})
				};
				foreach (Pawn item in PawnsFinder.AllMaps_FreeColonistsSpawned)
				{
					if (TrainableUtility.CanBeMaster(item, pawn))
					{
						Pawn master = item;
						list.Add(new FloatMenuOption(master.LabelShortCap, delegate
						{
							pawn.playerSettings.Master = master;
							InspectorCore.Poke();
						}));
					}
				}
				Find.WindowStack.Add(new FloatMenu(list));
			}
			num += 22f;
			if (pawn.playerSettings.Master != null)
			{
				num += FollowRow(pawn, x, w, num, "RUI.Inspector.CtlFollowDrafted".Translate(), pawn.playerSettings.followDrafted, delegate(bool v)
				{
					pawn.playerSettings.followDrafted = v;
				});
				num += FollowRow(pawn, x, w, num, "RUI.Inspector.CtlFollowFieldwork".Translate(), pawn.playerSettings.followFieldwork, delegate(bool v)
				{
					pawn.playerSettings.followFieldwork = v;
				});
			}
		}
		return num - y;
	}

	private static float FollowRow(Pawn pawn, float x, float w, float y, string label, bool value, Action<bool> set)
	{
		Rect rect = new Rect(x, y, w, 18f);
		bool flag = Mouse.IsOver(rect);
		CardChrome.Rounded(rect, flag ? Palette.Surface3 : Palette.Surface2, 8f);
		RadiusFont.Label(new Rect(rect.x + 8f, rect.y, rect.width - 40f, rect.height), label, GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		Rect rect2 = new Rect(rect.xMax - 20f, rect.y + 2f, 14f, 14f);
		CardChrome.Rounded(rect2, value ? RadiusTheme.Accent : Palette.Surface3, 4f);
		if (value)
		{
			IconSet.Get("Action/Check").Draw(rect2.ContractedBy(2f), Palette.InkOnAccent);
		}
		if (Widgets.ButtonInvisible(rect))
		{
			set(!value);
			InspectorCore.Poke();
		}
		return 22f;
	}

	public static float DrawInspect(Pawn pawn, float x, float w, float y, float scrollY, float viewH)
	{
		if (pawn == null)
		{
			return 0f;
		}
		string inspectString;
		try
		{
			inspectString = pawn.GetInspectString();
		}
		catch (Exception)
		{
			return 0f;
		}
		if (inspectString.NullOrEmpty())
		{
			return 0f;
		}
		if ((object)inspectString != lastInspectSrc && inspectString != lastInspectSrc)
		{
			lastInspectSrc = inspectString;
			lastInspectLines = inspectString.Split(new char[1] { '\n' });
		}
		float num = y;
		for (int i = 0; i < lastInspectLines.Length; i++)
		{
			string text = lastInspectLines[i];
			if (!text.NullOrEmpty())
			{
				float num2 = Mathf.Max(18f, Mathf.Ceil(RadiusFont.Height(text, w - 2f)));
				if (ViewCull.Visible(num - y, num2, scrollY, viewH))
				{
					RadiusFont.Label(new Rect(x + 2f, num, w - 2f, num2), text, GameFont.Small, heading: false, Palette.TextDim);
				}
				num += num2 + 1f;
			}
		}
		return num - y;
	}

	public static bool SupportsMedical(Pawn pawn)
	{
		Pawn_PlayerSettings pawn_PlayerSettings = pawn?.playerSettings;
		if (pawn_PlayerSettings == null)
		{
			return false;
		}
		if (pawn.RaceProps != null && pawn.RaceProps.IsFlesh)
		{
			return true;
		}
		if (pawn.IsColonist && pawn.workSettings != null)
		{
			return true;
		}
		return pawn_PlayerSettings.UsesConfigurableHostilityResponse;
	}

	public static float DrawMedical(Pawn pawn, float x, float w, float y)
	{
		if (!SupportsMedical(pawn))
		{
			return 0f;
		}
		Pawn_PlayerSettings ps = pawn.playerSettings;
		float num = y;
		if (pawn.RaceProps != null && pawn.RaceProps.IsFlesh)
		{
			if (PolicyRow(x, w, num, "RUI.Inspector.CtlMedical".Translate(), ps.medCare.GetLabel().CapitalizeFirst()))
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				foreach (MedicalCareCategory value in Enum.GetValues(typeof(MedicalCareCategory)))
				{
					MedicalCareCategory cc = value;
					list.Add(new FloatMenuOption(cc.GetLabel().CapitalizeFirst(), delegate
					{
						ps.medCare = cc;
						InspectorCore.Poke();
					}));
				}
				Find.WindowStack.Add(new FloatMenu(list));
			}
			num += 22f;
		}
		if (pawn.IsColonist && pawn.workSettings != null)
		{
			Rect rect = new Rect(x, num, w, 18f);
			bool flag = Mouse.IsOver(rect);
			CardChrome.Rounded(rect, flag ? Palette.Surface3 : Palette.Surface2, 8f);
			RadiusFont.Label(new Rect(rect.x + 8f, rect.y, rect.width - 40f, rect.height), "RUI.Inspector.CtlSelfTend".Translate(), GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			Rect rect2 = new Rect(rect.xMax - 26f, rect.y + 3f, 14f, 14f);
			CardChrome.Rounded(rect2, ps.selfTend ? RadiusTheme.Accent : Palette.Surface3, 4f);
			if (ps.selfTend)
			{
				IconSet.Get("Action/Check").Draw(rect2.ContractedBy(2f), Palette.InkOnAccent);
			}
			if (Widgets.ButtonInvisible(rect))
			{
				ps.selfTend = !ps.selfTend;
				InspectorCore.Poke();
			}
			num += 22f;
		}
		if (ps.UsesConfigurableHostilityResponse)
		{
			if (PolicyRow(x, w, num, "RUI.Inspector.CtlHostility".Translate(), ps.hostilityResponse.GetLabel().CapitalizeFirst()))
			{
				List<FloatMenuOption> list2 = new List<FloatMenuOption>();
				foreach (HostilityResponseMode value2 in Enum.GetValues(typeof(HostilityResponseMode)))
				{
					HostilityResponseMode mm = value2;
					list2.Add(new FloatMenuOption(mm.GetLabel().CapitalizeFirst(), delegate
					{
						ps.hostilityResponse = mm;
						InspectorCore.Poke();
					}));
				}
				Find.WindowStack.Add(new FloatMenu(list2));
			}
			num += 22f;
		}
		return num - y;
	}
}
