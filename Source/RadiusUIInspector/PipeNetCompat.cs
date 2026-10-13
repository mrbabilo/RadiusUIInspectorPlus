using System;
using System.Collections.Generic;
using System.Reflection;
using RadiusUI.Framework;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

internal static class PipeNetCompat
{
	private const string VefPackageId = "OskarPotocki.VanillaFactionsExpanded.Core";

	private static readonly Type? CompResourceType;

	private static readonly PropertyInfo? PipeNetProp;

	private static readonly PropertyInfo? StoredProp;

	private static readonly PropertyInfo? AvailableCapacityProp;

	private static readonly PropertyInfo? ResourceProp;

	private static readonly FieldInfo? ResourceColorField;

	private static readonly FieldInfo? ResourceNameField;

	internal static bool Ready { get; }

	static PipeNetCompat()
	{
		if (!ModCompat.IsActive("OskarPotocki.VanillaFactionsExpanded.Core"))
		{
			return;
		}
		try
		{
			CompResourceType = GenTypes.GetTypeInAnyAssembly("PipeSystem.CompResource");
			if (CompResourceType == null)
			{
				return;
			}
			PipeNetProp = CompResourceType.GetProperty("PipeNet");
			Type obj = PipeNetProp?.PropertyType;
			StoredProp = obj?.GetProperty("Stored");
			AvailableCapacityProp = obj?.GetProperty("AvailableCapacity");
			ResourceProp = CompResourceType.GetProperty("Resource");
			Type obj2 = ResourceProp?.PropertyType;
			ResourceColorField = obj2?.GetField("color");
			ResourceNameField = obj2?.GetField("name");
		}
		catch (Exception ex)
		{
			Log.Warning("[Radius UI - Inspector] VEF pipe system reflection failed (" + ex.Message + ") - the pipe network cell will stay hidden.");
			return;
		}
		Ready = PipeNetProp != null && StoredProp != null && AvailableCapacityProp != null;
		if (!Ready)
		{
			Log.Warning("[Radius UI - Inspector] VEF is active but PipeSystem.CompResource/PipeNet members did not resolve (version drift?) - the pipe network cell will stay hidden.");
		}
	}

	internal static ThingComp? FindComp(ThingWithComps? twc)
	{
		if (!Ready || twc == null)
		{
			return null;
		}
		List<ThingComp> allComps = twc.AllComps;
		for (int i = 0; i < allComps.Count; i++)
		{
			if (CompResourceType.IsAssignableFrom(allComps[i].GetType()))
			{
				return allComps[i];
			}
		}
		return null;
	}

	internal static bool TryGetNetworkData(ThingComp comp, out float stored, out float totalCapacity)
	{
		stored = 0f;
		totalCapacity = 0f;
		if (!Ready)
		{
			return false;
		}
		try
		{
			object value = PipeNetProp.GetValue(comp);
			if (value == null)
			{
				return false;
			}
			if (StoredProp.GetValue(value) is float num && AvailableCapacityProp.GetValue(value) is float num2)
			{
				stored = num;
				totalCapacity = num + num2;
				return true;
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	internal static bool TryGetResourceColor(ThingComp comp, out Color color)
	{
		color = default(Color);
		if (!Ready || ResourceProp == null || ResourceColorField == null)
		{
			return false;
		}
		try
		{
			object value = ResourceProp.GetValue(comp);
			if (value != null && ResourceColorField.GetValue(value) is Color { a: >0f } color2)
			{
				color = color2;
				return true;
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	internal static string? GetResourceLabel(ThingComp comp)
	{
		if (!Ready || ResourceProp == null || ResourceNameField == null)
		{
			return null;
		}
		try
		{
			object value = ResourceProp.GetValue(comp);
			string text = ((value == null) ? null : (ResourceNameField.GetValue(value) as string));
			return text.NullOrEmpty() ? null : text;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
