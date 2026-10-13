using System;
using HarmonyLib;
using RimWorld;

namespace RadiusUIInspector;

[HarmonyPatch(typeof(MainTabWindow_Inspect), "WindowUpdate")]
public static class Patch_MainTabWindow_Inspect_WindowUpdate
{
	public static void Postfix(MainTabWindow_Inspect __instance)
	{
		try
		{
			bool active = InspectorCore.Active;
			__instance.doWindowBackground = !active;
			__instance.drawShadow = !active;
		}
		catch (Exception)
		{
		}
	}
}
