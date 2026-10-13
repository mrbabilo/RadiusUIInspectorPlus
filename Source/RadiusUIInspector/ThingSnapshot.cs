using System;
using System.Collections.Generic;
using System.Text;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public class ThingSnapshot
{
	private const int FastTicks = 30;

	private const int MaxInspectLines = 40;

	public Thing? Thing;

	public string Label = "";

	public string SubLine = "";

	public string Initial = "?";

	public bool HasHP;

	public float HPPct;

	public string HPStr = "";

	public string QualityStr = "";

	public string StackStr = "";

	public string ValueStr = "";

	public bool Forbidden;

	public bool BrokenDown;

	public string PowerStr = "";

	public bool PowerOn;

	public bool HasPower;

	public bool HasFuel;

	public float FuelPct;

	public string FuelStr = "";

	public bool HasGrowth;

	public float GrowthPct;

	public string GrowthStr = "";

	public bool HasFresh;

	public float FreshPct;

	public string FreshStr = "";

	public bool HasBattery;

	public float BatteryPct;

	public string BatteryStr = "";

	public bool HasGrid;

	public float GridPct;

	public string GridStr = "";

	public bool HasPipe;

	public float PipePct;

	public string PipeStr = "";

	public string PipeLabel = "";

	public Color PipeColor = Color.white;

	public string[] InspectLines = Array.Empty<string>();

	private static readonly string FrozenWord = "CurrentlyFrozen".Translate();

	private static readonly string RottingWord = "RotStateRotting".Translate();

	private static readonly string DessicatedWord = "RotStateDessicated".Translate();

	private int nextTick;

	private int lastId = -1;

	public void Refresh(Thing t)
	{
		if (t.thingIDNumber == lastId && !Throttle.EveryTicks(ref nextTick, 30))
		{
			return;
		}
		lastId = t.thingIDNumber;
		Thing = t;
		Label = Safe(() => t.LabelCap, t.def.defName);
		SubLine = Safe<string>(() => t.def.LabelCap, "");
		Initial = (Label.NullOrEmpty() ? "?" : Label.Substring(0, 1).ToUpperInvariant());
		HasHP = t.def.useHitPoints && t.MaxHitPoints > 0;
		if (HasHP)
		{
			HPPct = Mathf.Clamp01((float)t.HitPoints / (float)t.MaxHitPoints);
			HPStr = ScratchText.Sb().Append(t.HitPoints).Append(" / ")
				.Append(t.MaxHitPoints)
				.ToString();
		}
		QualityStr = (t.TryGetQuality(out var qc) ? qc.GetLabel().CapitalizeFirst() : "");
		StackStr = ((t.stackCount > 1) ? ("x" + t.stackCount) : "");
		ValueStr = Safe(delegate
		{
			float num10 = t.MarketValue * (float)t.stackCount;
			return (!(num10 > 0f)) ? "" : num10.ToStringMoney();
		}, "");
		Forbidden = Safe(() => t.IsForbidden(Faction.OfPlayer), fallback: false);
		BrokenDown = Safe(() => t.IsBrokenDown(), fallback: false);
		CompPowerTrader compPowerTrader = (t as ThingWithComps)?.GetComp<CompPowerTrader>();
		HasPower = compPowerTrader != null;
		if (compPowerTrader != null)
		{
			PowerOn = compPowerTrader.PowerOn;
			float num = Mathf.Abs(compPowerTrader.PowerOutput);
			StringBuilder stringBuilder = ScratchText.Sb();
			stringBuilder.Append((PowerOn ? "RUI.Inspector.PowerOn".Translate() : "RUI.Inspector.PowerOff".Translate()).ToString());
			if (num > 0f)
			{
				stringBuilder.Append(" - ").Append(num.ToString("F0")).Append(" W");
			}
			PowerStr = stringBuilder.ToString();
		}
		else
		{
			PowerStr = "";
		}
		CompRefuelable compRefuelable = (t as ThingWithComps)?.GetComp<CompRefuelable>();
		HasFuel = compRefuelable != null && compRefuelable.Props.fuelCapacity > 0f;
		if (HasFuel)
		{
			FuelPct = Mathf.Clamp01(compRefuelable.Fuel / compRefuelable.Props.fuelCapacity);
			FuelStr = ScratchText.Sb().Append(compRefuelable.Fuel.ToString("F0")).Append(" / ")
				.Append(compRefuelable.Props.fuelCapacity.ToString("F0"))
				.ToString();
		}
		HasGrowth = t is Plant;
		if (HasGrowth)
		{
			Plant plant = (Plant)t;
			GrowthPct = Mathf.Clamp01(plant.Growth);
			GrowthStr = plant.Growth.ToStringPercent();
		}
		ThingWithComps thingWithComps = t as ThingWithComps;
		CompRottable compRottable = thingWithComps?.GetComp<CompRottable>();
		HasFresh = compRottable != null && compRottable.Active && compRottable.PropsRot.TicksToRotStart > 0;
		if (HasFresh)
		{
			FreshPct = Mathf.Clamp01(1f - compRottable.RotProgressPct);
			switch (compRottable.Stage)
			{
			case RotStage.Fresh:
			{
				float num2 = GenTemperature.RotRateAtTemperature(Mathf.RoundToInt(t.AmbientTemperature));
				FreshStr = ((num2 < 0.001f) ? FrozenWord : compRottable.TicksUntilRotAtCurrentTemp.ToStringTicksToPeriod());
				break;
			}
			case RotStage.Rotting:
				FreshStr = RottingWord;
				break;
			default:
				FreshStr = DessicatedWord;
				break;
			}
		}
		CompPowerBattery compPowerBattery = thingWithComps?.GetComp<CompPowerBattery>();
		HasBattery = compPowerBattery != null && compPowerBattery.Props.storedEnergyMax > 0f;
		if (HasBattery)
		{
			BatteryPct = Mathf.Clamp01(compPowerBattery.StoredEnergy / compPowerBattery.Props.storedEnergyMax);
			BatteryStr = ScratchText.Sb().Append(compPowerBattery.StoredEnergy.ToString("F0")).Append(" / ")
				.Append(compPowerBattery.Props.storedEnergyMax.ToString("F0"))
				.Append(" Wd")
				.ToString();
		}
		PowerNet powerNet = thingWithComps?.GetComp<CompPower>()?.PowerNet;
		float num3 = 0f;
		if (powerNet != null)
		{
			List<CompPowerBattery> batteryComps = powerNet.batteryComps;
			for (int num4 = 0; num4 < batteryComps.Count; num4++)
			{
				num3 += batteryComps[num4].Props.storedEnergyMax;
			}
		}
		HasGrid = num3 > 0f;
		if (HasGrid)
		{
			float num5 = powerNet.CurrentStoredEnergy();
			GridPct = Mathf.Clamp01(num5 / num3);
			GridStr = ScratchText.Sb().Append(num5.ToString("F0")).Append(" / ")
				.Append(num3.ToString("F0"))
				.Append(" Wd")
				.ToString();
		}
		HasPipe = false;
		ThingComp thingComp = PipeNetCompat.FindComp(thingWithComps);
		if (thingComp != null && PipeNetCompat.TryGetNetworkData(thingComp, out var stored, out var totalCapacity) && totalCapacity > 0f)
		{
			HasPipe = true;
			PipePct = Mathf.Clamp01(stored / totalCapacity);
			PipeStr = ScratchText.Sb().Append(stored.ToString("F0")).Append(" / ")
				.Append(totalCapacity.ToString("F0"))
				.ToString();
			PipeLabel = PipeNetCompat.GetResourceLabel(thingComp)?.CapitalizeFirst() ?? "";
			PipeColor = (PipeNetCompat.TryGetResourceColor(thingComp, out var color) ? new Color(color.r, color.g, color.b, 0.7f) : Palette.FillNeutral);
		}
		string text = Safe(() => t.GetInspectString(), "");
		if (text.NullOrEmpty())
		{
			InspectLines = Array.Empty<string>();
			return;
		}
		string[] array = text.Split(new char[1] { '\n' });
		int num6 = 0;
		for (int num7 = 0; num7 < array.Length; num7++)
		{
			if (num6 >= 40)
			{
				break;
			}
			if (!array[num7].TrimEnd(Array.Empty<char>()).NullOrEmpty())
			{
				num6++;
			}
		}
		string[] array2 = new string[num6];
		int num8 = 0;
		for (int num9 = 0; num9 < array.Length; num9++)
		{
			if (num8 >= num6)
			{
				break;
			}
			string text2 = array[num9].TrimEnd(Array.Empty<char>());
			if (!text2.NullOrEmpty())
			{
				array2[num8++] = text2;
			}
		}
		InspectLines = array2;
	}

	private static T Safe<T>(Func<T> f, T fallback)
	{
		try
		{
			return f();
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Thing read failed: " + ex.Message, 5351393);
			return fallback;
		}
	}
}
