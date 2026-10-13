using System;
using System.Reflection;
using HarmonyLib;
using RadiusUI.Framework;
using Verse;

namespace RadiusUIInspector;

[StaticConstructorOnStartup]
public static class HarmonyBoot
{
	static HarmonyBoot()
	{
		FrameworkVersion.Require("Radius UI - Inspector", 3);
		if (ModCompat.IsActive("jaxe.rimhud"))
		{
			InspectorCore.ExternalPaneOwner = true;
			Log.Message("[Radius UI - Inspector] RimHUD is active - the inspector stands down and the pane is left to it.");
		}
		Harmony harmony = new Harmony("astryl.radiusui.inspector");
		Type[] types;
		try
		{
			types = Assembly.GetExecutingAssembly().GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			types = ex.Types;
		}
		Type[] array = types;
		foreach (Type type in array)
		{
			if (!(type == null) && type.IsDefined(typeof(HarmonyPatch), inherit: false))
			{
				try
				{
					harmony.CreateClassProcessor(type).Patch();
				}
				catch (Exception ex2)
				{
					Log.Warning("[Radius UI - Inspector] Skipped patch class " + type.Name + ": " + ex2.Message);
				}
			}
		}
		BIPCompat.TryHook(harmony);
	}
}
