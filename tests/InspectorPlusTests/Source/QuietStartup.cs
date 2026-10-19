using HarmonyLib;
using LudeonTK;
using Verse;

namespace RUIInspectorTests;

// Two windows open over a fresh sandbox at start-up and cover the scene: the game's debug log (at the first mod
// metadata warning) and the Radius UI Framework notice (the sandbox has never seen this framework generation).
// Neither is what a run checks, so the test helpers keep both shut; errors are still collected by the checks.
internal static class QuietStartup
{
	internal static void Apply()
	{
		EditWindow_Log.wantsToOpen = false;
		Harmony harmony = new Harmony("mrbabilo.ruiinspector.tests.quiet");
		harmony.Patch(AccessTools.Method(typeof(EditWindow_Log), "TryAutoOpen"), new HarmonyMethod(typeof(QuietStartup), "Skip"));
		harmony.Patch(AccessTools.Method(typeof(WindowStack), "Add"), new HarmonyMethod(typeof(QuietStartup), "SkipNotice"));
	}

	public static bool Skip()
	{
		return false;
	}

	public static bool SkipNotice(Window window)
	{
		return window == null || window.GetType().Name != "Dialog_FrameworkUpdate";
	}
}
