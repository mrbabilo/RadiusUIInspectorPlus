using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

internal static class VSECompat
{
	private static readonly MethodInfo? PassionToDef;

	private static readonly PropertyInfo? IconProp;

	private static readonly Dictionary<byte, Texture2D?> iconCache;

	private static readonly Dictionary<byte, string> labelCache;

	internal static bool Active => PassionToDef != null;

	static VSECompat()
	{
		iconCache = new Dictionary<byte, Texture2D>();
		labelCache = new Dictionary<byte, string>();
		Type typeInAnyAssembly = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionManager");
		if (!(typeInAnyAssembly == null))
		{
			PassionToDef = typeInAnyAssembly.GetMethod("PassionToDef", BindingFlags.Static | BindingFlags.Public);
			IconProp = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionDef")?.GetProperty("Icon", BindingFlags.Instance | BindingFlags.Public);
			if (IconProp == null)
			{
				PassionToDef = null;
			}
		}
	}

	internal static Texture2D? PassionIcon(Passion passion)
	{
		if (PassionToDef == null)
		{
			return null;
		}
		byte key = (byte)passion;
		if (iconCache.TryGetValue(key, out Texture2D value))
		{
			return value;
		}
		Texture2D texture2D = null;
		try
		{
			object obj = PassionToDef.Invoke(null, new object[1] { passion });
			texture2D = ((obj != null) ? (IconProp.GetValue(obj) as Texture2D) : null);
			if (obj is Def def)
			{
				labelCache[key] = def.LabelCap;
			}
		}
		catch (Exception ex)
		{
			Log.WarningOnce("[Radius UI - Inspector] VSE passion lookup failed: " + ex.Message, 5351408);
		}
		iconCache[key] = texture2D;
		return texture2D;
	}

	internal static string PassionLabel(Passion passion)
	{
		if (!labelCache.TryGetValue((byte)passion, out string value))
		{
			return "";
		}
		return value;
	}
}
