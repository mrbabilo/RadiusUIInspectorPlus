using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RadiusUI.Framework;
using RadiusUIInspector;
using RimWorld;
using UnityEngine;
using Verse;

namespace RUIInspectorTests;

// Automated checks for Inspector Plus. Inert unless the game is started with -ruitest=<results file> (and -quicktest):
// it then builds a scene on the quick test map, runs every step once, writes PASS/FAIL lines to the file and quits.
// Data checks call the mod directly. Panel checks select things and open tabs through the game, wait for real
// OnGUI frames, then read the mod's state: the panels are never drawn by hand.
[StaticConstructorOnStartup]
public static class Boot
{
	internal static string ResultsPath;

	internal static readonly List<Message> Messages = new List<Message>();

	internal static readonly List<string> Errors = new List<string>();

	static Boot()
	{
		SceneServer.Bind();
		bool scene = GenCommandLine.TryGetCommandLineArg("ruiscene", out _);
		bool test = GenCommandLine.TryGetCommandLineArg("ruitest", out ResultsPath);
		if (scene || test)
		{
			QuietStartup.Apply();
		}
		if (!test)
		{
			return;
		}
		Application.runInBackground = true;
		File.WriteAllText(ResultsPath, "# Inspector Plus checks\n");
		Harmony harmony = new Harmony("mrbabilo.ruiinspector.tests");
		harmony.Patch(AccessTools.Method(typeof(Verse.Messages), "Message", new Type[2]
		{
			typeof(Message),
			typeof(bool)
		}), postfix: new HarmonyMethod(typeof(Boot), "MessagePostfix"));
		harmony.Patch(AccessTools.Method(typeof(Log), "Error", new Type[1] { typeof(string) }), postfix: new HarmonyMethod(typeof(Boot), "ErrorPostfix"));
		harmony.Patch(AccessTools.Method(typeof(Log), "Warning", new Type[1] { typeof(string) }), postfix: new HarmonyMethod(typeof(Boot), "WarningPostfix"));
		harmony.Patch(AccessTools.Method(typeof(StoragePanel), "DrawTree"), new HarmonyMethod(typeof(Boot), "DrawTreePrefix"), new HarmonyMethod(typeof(Boot), "DrawTreePostfix"));
	}

	// A check sets ForceFull: the next DrawTree pass walks every subtree (no height kept) and leaves its content
	// height in FullWalkH, the reference the stepped passes are held to.
	internal static bool ForceFull;

	internal static float FullWalkH = -1f;

	public static void DrawTreePrefix(out bool __state)
	{
		__state = ForceFull;
		if (ForceFull)
		{
			ForceFull = false;
			AccessTools.Field(typeof(StoragePanel), "treeShapeDirty").SetValue(null, true);
		}
	}

	public static void DrawTreePostfix(bool __state)
	{
		if (__state)
		{
			FullWalkH = (float)AccessTools.Field(typeof(StoragePanel), "lastContentH").GetValue(null);
		}
	}

	public static void MessagePostfix(Message msg)
	{
		Messages.Add(msg);
	}

	public static void ErrorPostfix(string text)
	{
		Errors.Add(text);
	}

	// Warnings from the Radius suite count as failures: a skipped patch or fix only shows up there.
	public static void WarningPostfix(string text)
	{
		if (text != null && text.Contains("Radius UI"))
		{
			Errors.Add("warning: " + text);
		}
	}
}

public class TestRunner : GameComponent
{
	private class Step
	{
		internal string Name;

		internal Action Setup;

		internal int Wait;

		internal Func<string> Verify;
	}

	private static bool done;

	private int frames;

	private List<Step> steps;

	private int index;

	private int verifyAt = -1;

	private int errorsBefore;

	private int pass;

	private int fail;

	public TestRunner(Game game)
	{
	}

	public override void GameComponentUpdate()
	{
		if (done || Boot.ResultsPath == null || Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent || ++frames < 120)
		{
			return;
		}
		if (steps == null)
		{
			Write("# Biotech active: " + ModsConfig.BiotechActive + ", flat scroll fix flag off: " + GenCommandLine.CommandLineArgPassed("ruinoflatfix"));
			try
			{
				Find.TickManager.Pause();
				Fixture.Build(6);
				steps = new Checks().Steps().Select((ValueTuple<string, Action, int, Func<string>> s) => new Step
				{
					Name = s.Item1,
					Setup = s.Item2,
					Wait = s.Item3,
					Verify = s.Item4
				}).ToList();
			}
			catch (Exception ex)
			{
				Write("FAIL fixture: " + ex);
				Finish();
			}
			return;
		}
		if (verifyAt >= 0)
		{
			if (Time.frameCount < verifyAt)
			{
				return;
			}
			Conclude(steps[index]);
			index++;
			verifyAt = -1;
			return;
		}
		if (index >= steps.Count)
		{
			Finish();
			return;
		}
		Step step = steps[index];
		errorsBefore = Boot.Errors.Count;
		try
		{
			step.Setup?.Invoke();
		}
		catch (Exception ex)
		{
			Record(step.Name, "setup threw " + ex.GetType().Name + ": " + ex.Message);
			index++;
			return;
		}
		if (step.Wait <= 0)
		{
			Conclude(step);
			index++;
		}
		else
		{
			verifyAt = Time.frameCount + step.Wait;
		}
	}

	private void Conclude(Step step)
	{
		string reason;
		try
		{
			reason = step.Verify();
		}
		catch (Exception ex)
		{
			reason = "verify threw " + ex.GetType().Name + ": " + ex.Message;
		}
		if (reason == null && Boot.Errors.Count > errorsBefore)
		{
			reason = "errors logged: " + string.Join(" | ", Boot.Errors.Skip(errorsBefore).Take(3).Select((string e) => e.Split('\n')[0]));
		}
		Record(step.Name, reason);
	}

	private void Record(string name, string reason)
	{
		if (reason == null)
		{
			pass++;
			Write("PASS " + name);
		}
		else
		{
			fail++;
			Write("FAIL " + name + ": " + reason);
		}
	}

	private void Finish()
	{
		done = true;
		Write("# " + pass + " passed, " + fail + " failed");
		Write("# END");
		Root.Shutdown();
	}

	internal static void Write(string line)
	{
		File.AppendAllText(Boot.ResultsPath, line + "\n");
	}
}

internal class Checks
{
	private static IInspectPane Pane => (MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;

	private static object StorageField(string name)
	{
		return AccessTools.Field(typeof(StoragePanel), name).GetValue(null);
	}

	private static object CallStorage(string name, params object[] args)
	{
		return AccessTools.Method(typeof(StoragePanel), name).Invoke(null, args);
	}

	private static void OpenInspect()
	{
		if (Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect)
		{
			Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect, playSound: false);
		}
	}

	private static void OpenTab<T>() where T : InspectTabBase
	{
		OpenInspect();
		IInspectPane pane = Pane;
		InspectTabBase tab = pane.CurTabs?.FirstOrDefault((InspectTabBase t) => t is T) ?? throw new Exception(typeof(T).Name + " not offered");
		if (pane.OpenTabType != typeof(T))
		{
			TabPlumbing.Toggle(tab, pane);
		}
	}

	private static void CloseTab()
	{
		Pane.CloseOpenTab();
	}

	private static string Expect(bool ok, string why)
	{
		return ok ? null : why;
	}

	private static string Mode(PaneMode expected)
	{
		PaneMode mode = InspectorCore.Mode;
		return Expect(mode == expected, "mode " + mode + ", expected " + expected);
	}

	private static string Guarded()
	{
		return Expect(!InspectorCore.KillSwitch && !InspectorCore.Guard.Disabled, "panel tripped back to vanilla");
	}

	// The FlatScrollFix copy that owns the patch (Inspector Plus or Colonist Bar Plus), or the Inspector's when none does.
	private static Type FixType()
	{
		Patches info = Harmony.GetPatchInfo(AccessTools.Method(typeof(FlatScroll), "Push"));
		Patch patch = info?.Prefixes.FirstOrDefault((Patch p) => p.owner == "mrbabilo.radiusui.flatscrollfix");
		return patch?.PatchMethod.DeclaringType ?? AccessTools.TypeByName("RadiusUIInspector.FlatScrollFix");
	}

	private static float ContentH => (float)StorageField("lastContentH");

	private static void SetScroll(float y)
	{
		AccessTools.Field(typeof(StoragePanel), "scroll").SetValue(null, new Vector2(0f, y));
	}

	// Scrolls the tree, asks for one full walk, and later compares the stepped height with it.
	private static void FullWalkAt(float y)
	{
		SetScroll(y);
		Boot.FullWalkH = -1f;
		Boot.ForceFull = true;
	}

	private static string MatchesFullWalk(string where)
	{
		if (Boot.FullWalkH < 0f)
		{
			return "no full walk ran " + where;
		}
		return Expect(Mathf.Abs(ContentH - Boot.FullWalkH) < 0.5f, "stepped height " + ContentH + " vs full walk " + Boot.FullWalkH + " " + where);
	}

	private static void ShapeDirty()
	{
		AccessTools.Field(typeof(StoragePanel), "treeShapeDirty").SetValue(null, true);
	}

	private static string First(params string[] reasons)
	{
		return reasons.FirstOrDefault((string r) => r != null);
	}

	private static HashSet<ThingDef> Allowed(ThingFilter f)
	{
		return new HashSet<ThingDef>(f.AllowedThingDefs);
	}

	internal IEnumerable<ValueTuple<string, Action, int, Func<string>>> Steps()
	{
		Pawn pawn = Fixture.Pawns[0];
		Zone_Stockpile zone = Fixture.Stockpile;
		ThingFilter zoneFilter = zone.settings.filter;
		ThingFilter zoneParent = zone.GetParentStoreSettings()?.filter;

		// Boot
		yield return ("boot: Inspector patches applied", null, 0, delegate
		{
			string[] targets = new string[2] { "InspectPaneOnGUI", "PaneSizeFor" };
			foreach (string t in targets)
			{
				Patches info = Harmony.GetPatchInfo(AccessTools.Method(typeof(InspectPaneUtility), t));
				if (info == null || !info.Owners.Contains("astryl.radiusui.inspector"))
				{
					return "InspectPaneUtility." + t + " not patched";
				}
			}
			return null;
		});
		yield return ("boot: FlatScroll fix owned by Inspector copy, or off with -ruinoflatfix", null, 0, delegate
		{
			Patches info = Harmony.GetPatchInfo(AccessTools.Method(typeof(FlatScroll), "Push"));
			bool patched = info != null && info.Owners.Contains("mrbabilo.radiusui.flatscrollfix");
			if (GenCommandLine.CommandLineArgPassed("ruinoflatfix"))
			{
				return Expect(!patched, "patched despite -ruinoflatfix");
			}
			if (!patched)
			{
				return "FlatScroll.Push not patched";
			}
			// Colonist Bar Plus ships the same fix: whichever copy loads first patches, the other stands down.
			string asm = FixType().Assembly.GetName().Name;
			bool cbPlus = ModsConfig.IsActive("mrbabilo.RadiusUI.ColonistBarPlus");
			return Expect(asm == "RadiusUIInspector" || (cbPlus && asm == "RadiusColonistBar"), "patched by " + asm);
		});

		// Snapshots
		yield return ("snapshot: pawn refresh fills identity line without Biotech crash", null, 0, delegate
		{
			PawnSnapshot snap = new PawnSnapshot();
			snap.Refresh(pawn);
			if (string.IsNullOrEmpty(snap.GenderAge))
			{
				return "GenderAge empty";
			}
			string kind = ModsConfig.BiotechActive && pawn.genes != null ? pawn.genes.XenotypeLabelCap : pawn.def.LabelCap.ToString();
			return First(Expect(snap.GenderAge.Contains(kind), "GenderAge '" + snap.GenderAge + "' lacks '" + kind + "'"), Expect(snap.NameShort == pawn.LabelShortCap, "NameShort " + snap.NameShort));
		});
		yield return ("snapshot: thing refresh reads bed label and hit points", null, 0, delegate
		{
			ThingSnapshot snap = new ThingSnapshot();
			snap.Refresh(Fixture.Bed);
			return First(Expect(snap.Thing == Fixture.Bed, "Thing not set"), Expect(snap.HasHP, "HasHP false"), Expect(!string.IsNullOrEmpty(snap.Label), "Label empty"));
		});
		yield return ("snapshot: multi refresh counts 3 pawns", null, 0, delegate
		{
			MultiSnapshot snap = new MultiSnapshot();
			snap.Refresh(Fixture.Pawns.Take(3).Cast<object>().ToList());
			return First(Expect(snap.TotalCount == 3, "TotalCount " + snap.TotalCount), Expect(snap.Entries.Count == 3, "Entries " + snap.Entries.Count));
		});

		// Saved filters
		ThingDef steel = ThingDefOf.Steel;
		ThingDef wood = ThingDefOf.WoodLog;
		ThingDef meal = ThingDefOf.MealSimple;
		ThingDef silver = ThingDefOf.Silver;
		SpecialThingFilterDef special = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowRotten") ?? DefDatabase<SpecialThingFilterDef>.AllDefs.First((SpecialThingFilterDef s) => s.configurable);
		StorageBookmark bookmark = null;
		yield return ("bookmark: capture keeps allowed defs and refused special", null, 0, delegate
		{
			ThingFilter f = new ThingFilter();
			f.SetAllow(steel, allow: true);
			f.SetAllow(wood, allow: true);
			f.SetAllow(meal, allow: true);
			f.SetAllow(special, allow: false);
			bookmark = StorageBookmark.Capture("test", f);
			return First(Expect(new HashSet<string>(bookmark.allowedDefs).SetEquals(new string[3] { steel.defName, wood.defName, meal.defName }), "allowedDefs " + string.Join(",", bookmark.allowedDefs)), Expect(bookmark.disallowedSpecialFilters.SequenceEqual(new string[1] { special.defName }), "disallowed " + string.Join(",", bookmark.disallowedSpecialFilters)));
		});
		yield return ("bookmark: Replace sets exactly the saved filter", null, 0, delegate
		{
			ThingFilter f = new ThingFilter();
			f.SetAllowAll(null);
			bookmark.ApplyTo(f, null, StorageBookmark.Mode.Replace);
			return First(Expect(Allowed(f).SetEquals(new ThingDef[3] { steel, wood, meal }), "allowed " + f.AllowedDefCount), Expect(!f.Allows(special), "special allowed"));
		});
		yield return ("bookmark: Add keeps existing defs", null, 0, delegate
		{
			ThingFilter f = new ThingFilter();
			f.SetAllow(silver, allow: true);
			bookmark.ApplyTo(f, null, StorageBookmark.Mode.Add);
			return Expect(Allowed(f).SetEquals(new ThingDef[4] { steel, wood, meal, silver }), "allowed " + f.AllowedDefCount);
		});
		yield return ("bookmark: Remove refuses only the saved defs", null, 0, delegate
		{
			ThingFilter f = new ThingFilter();
			f.SetAllowAll(null);
			int before = f.AllowedDefCount;
			bookmark.ApplyTo(f, null, StorageBookmark.Mode.Remove);
			return First(Expect(!f.Allows(steel) && !f.Allows(wood) && !f.Allows(meal), "saved def still allowed"), Expect(f.Allows(silver), "silver refused"), Expect(f.AllowedDefCount == before - 3, "count " + before + " -> " + f.AllowedDefCount));
		});
		yield return ("bookmark: parent filter caps what Add allows", null, 0, delegate
		{
			ThingFilter parent = new ThingFilter();
			parent.SetAllow(steel, allow: true);
			ThingFilter f = new ThingFilter();
			bookmark.ApplyTo(f, parent, StorageBookmark.Mode.Add);
			return Expect(Allowed(f).SetEquals(new ThingDef[1] { steel }), "allowed " + string.Join(",", f.AllowedThingDefs.Select((ThingDef d) => d.defName)));
		});
		yield return ("bookmark: unknown defName is skipped silently", null, 0, delegate
		{
			StorageBookmark b = new StorageBookmark();
			b.allowedDefs.Add("RUITest_NoSuchDef");
			b.allowedDefs.Add(steel.defName);
			b.disallowedSpecialFilters.Add("RUITest_NoSuchSpecial");
			ThingFilter f = new ThingFilter();
			b.ApplyTo(f, null, StorageBookmark.Mode.Replace);
			return Expect(Allowed(f).SetEquals(new ThingDef[1] { steel }), "allowed " + f.AllowedDefCount);
		});
		yield return ("bookmark: save and load round trip", null, 0, delegate
		{
			string path = Path.Combine(Path.GetDirectoryName(Boot.ResultsPath), "ruitest-bookmark.xml");
			StorageBookmark b = bookmark;
			b.hitPoints = new FloatRange(0.25f, 0.9f);
			b.quality = new QualityRange(QualityCategory.Normal, QualityCategory.Excellent);
			Scribe.saver.InitSaving(path, "bookmark");
			Scribe_Deep.Look(ref b, "b");
			Scribe.saver.FinalizeSaving();
			StorageBookmark loaded = null;
			Scribe.loader.InitLoading(path);
			Scribe_Deep.Look(ref loaded, "b");
			Scribe.loader.FinalizeLoading();
			File.Delete(path);
			if (loaded == null)
			{
				return "nothing loaded";
			}
			return First(Expect(loaded.name == b.name, "name"), Expect(loaded.allowedDefs.SequenceEqual(b.allowedDefs), "allowedDefs"), Expect(loaded.disallowedSpecialFilters.SequenceEqual(b.disallowedSpecialFilters), "specials"), Expect(loaded.hitPoints == b.hitPoints, "hitPoints " + loaded.hitPoints), Expect(loaded.quality == b.quality, "quality " + loaded.quality));
		});

		// Invert
		yield return ("invert: flips item rows, keeps specials, twice restores", null, 0, delegate
		{
			HashSet<ThingDef> before = Allowed(zoneFilter);
			List<bool> specials = DefDatabase<SpecialThingFilterDef>.AllDefs.Select((SpecialThingFilterDef s) => zoneFilter.Allows(s)).ToList();
			CallStorage("InvertDefs", zoneFilter, zoneParent);
			HashSet<ThingDef> once = Allowed(zoneFilter);
			bool specialsKept = DefDatabase<SpecialThingFilterDef>.AllDefs.Select((SpecialThingFilterDef s) => zoneFilter.Allows(s)).SequenceEqual(specials);
			CallStorage("InvertDefs", zoneFilter, zoneParent);
			HashSet<ThingDef> twice = Allowed(zoneFilter);
			return First(Expect(!once.Overlaps(before.Where((ThingDef d) => (bool)CallStorage("Visible", d, zoneParent))), "an allowed row stayed allowed"), Expect(once.Count > 0, "nothing allowed after invert"), Expect(specialsKept, "special filter changed"), Expect(twice.SetEquals(before), "twice: " + before.Count + " -> " + twice.Count));
		});

		// Materials
		yield return ("materials: steel filter refuses steel-made things only", null, 0, delegate
		{
			SpecialThingFilterDef sf = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("MaterialFilter_allowSteel");
			if (sf == null)
			{
				return "MaterialFilter_allowSteel missing";
			}
			ThingFilter f = new ThingFilter();
			f.SetAllow(ThingDefOf.Bed, allow: true);
			f.SetAllow(steel, allow: true);
			f.SetAllow(sf, allow: false);
			return First(Expect(sf.workerClass == typeof(MaterialFilterWorker), "worker " + sf.workerClass), Expect(!f.Allows(Fixture.Make("Bed", steel)), "steel bed allowed"), Expect(f.Allows(Fixture.Make("Bed", wood)), "wood bed refused"), Expect(f.Allows(Fixture.Make("Steel")), "raw steel refused"));
		});

		// Special filter workers taken over from other mods
		yield return ("special filters: workers come from Inspector Plus and match", null, 0, delegate
		{
			Thing steelStack = Fixture.Make("Steel");
			Thing woodStack = Fixture.Make("WoodLog");
			Thing bed = Fixture.Make("Bed", wood, QualityCategory.Good);
			Thing mealThing = Fixture.Make("MealSimple");
			Thing knife = Fixture.Make("MeleeWeapon_Knife", null, QualityCategory.Good);
			Thing worn = Fixture.Make("Apparel_Parka", null, QualityCategory.Good);
			worn.HitPoints = Mathf.RoundToInt(worn.MaxHitPoints * 0.3f);
			Thing fine = Fixture.Make("Apparel_Parka", null, QualityCategory.Good);
			ValueTuple<string, Thing, bool>[] cases = new ValueTuple<string, Thing, bool>[]
			{
				("UsefulFilters_AllowHasQuality", bed, true),
				("UsefulFilters_AllowHasQuality", steelStack, false),
				("UsefulFilters_AllowNoQuality", steelStack, true),
				("NoQuality_Weapons", knife, false),
				("UsefulFilters_AllowRottable", mealThing, true),
				("UsefulFilters_AllowNonRottable", steelStack, true),
				("UsefulFilters_AllowFlammable", woodStack, true),
				("UsefulFilters_AllowNonFlammable", woodStack, false),
				("RUI_AllowStackable", steelStack, true),
				("RUI_AllowNonStackable", bed, true),
				("RUI_AllowHasHitPoints", bed, true),
				("UselessApparelFilter", worn, true),
				("UselessApparelFilter", fine, false),
				("NonUselessApparelFilter", fine, true)
			};
			List<string> bad = new List<string>();
			foreach (var (defName, thing, expected) in cases)
			{
				SpecialThingFilterDef sf = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail(defName);
				if (sf == null)
				{
					bad.Add(defName + " missing");
				}
				else if (sf.workerClass?.Namespace != "RadiusUIInspector")
				{
					bad.Add(defName + " worker " + sf.workerClass);
				}
				else if (sf.Worker.Matches(thing) != expected)
				{
					bad.Add(defName + "(" + thing.def.defName + ") != " + expected);
				}
			}
			return bad.Count == 0 ? null : string.Join("; ", bad);
		});

		// Pane modes, through real selection and OnGUI frames
		yield return ("pane: one colonist gives SinglePawn and fills the pawn snapshot", delegate
		{
			Fixture.Select(pawn);
		}, 20, delegate
		{
			PawnSnapshot snap = (PawnSnapshot)AccessTools.Field(typeof(InspectorPanel), "Snap").GetValue(null);
			return First(Mode(PaneMode.SinglePawn), Expect(snap.NameShort == pawn.LabelShortCap, "drawn snapshot is '" + snap.NameShort + "'"), Guarded());
		});
		yield return ("pane: health tab is hosted inside the panel", delegate
		{
			OpenTab<ITab_Pawn_Health>();
		}, 40, delegate
		{
			return First(Expect(Pane.OpenTabType == typeof(ITab_Pawn_Health), "open tab " + Pane.OpenTabType), Expect(InspectorCore.HostingTab, "HostingTab false"), Expect((bool)AccessTools.Property(typeof(TabPlumbing), "HostedTabPainted").GetValue(null), "tab never painted"), Guarded());
		});
		yield return ("pane: closing the tab ends hosting", CloseTab, 20, delegate
		{
			return First(Expect(!InspectorCore.HostingTab, "still hosting"), Guarded());
		});
		yield return ("pane: bed gives SingleThing and fills the thing snapshot", delegate
		{
			Fixture.Select(Fixture.Bed);
		}, 20, delegate
		{
			ThingSnapshot snap = (ThingSnapshot)AccessTools.Field(typeof(ThingPanel), "Snap").GetValue(null);
			return First(Mode(PaneMode.SingleThing), Expect(snap.Thing == Fixture.Bed, "drawn snapshot is " + snap.Thing), Guarded());
		});
		yield return ("pane: three colonists give Multi", delegate
		{
			Fixture.Select(Fixture.Pawns.Take(3).Cast<object>().ToArray());
		}, 20, delegate
		{
			MultiSnapshot snap = (MultiSnapshot)AccessTools.Field(typeof(InspectorPanel), "Multi").GetValue(null);
			return First(Mode(PaneMode.Multi), Expect(InspectorCore.MultiCount == 3, "MultiCount " + InspectorCore.MultiCount), Expect(snap.TotalCount == 3, "drawn TotalCount " + snap.TotalCount), Guarded());
		});
		yield return ("pane: two shelves leave the vanilla pane", delegate
		{
			Fixture.Select(Fixture.Shelves.Cast<object>().ToArray());
		}, 10, () => Mode(PaneMode.None));
		yield return ("pane: stockpile gives SingleZone", delegate
		{
			Fixture.Select(zone);
		}, 20, () => First(Mode(PaneMode.SingleZone), Guarded()));

		// Native storage panel
		yield return ("storage: opening the tab draws the native panel with stock and contents", delegate
		{
			OpenTab<ITab_Storage>();
		}, 60, delegate
		{
			int stored = ((Dictionary<ThingDef, int>)StorageField("storedCount")).Count;
			int rows = ((System.Collections.IList)StorageField("contents")).Count;
			return First(Expect(InspectorCore.NativeKind == NativeTabKind.Storage, "NativeKind " + InspectorCore.NativeKind), Expect(StorageField("lastParent") == zone, "panel drew " + StorageField("lastParent")), Expect(StoragePanel.LastSource == StoragePanel.Source.Zone, "source " + StoragePanel.LastSource), Expect(stored > 0, "no stock counted"), Expect(rows > 0, "contents list empty"), Guarded());
		});
		StorageBookmark original = null;
		int messagesBefore = 0;
		yield return ("storage: applying a filter that refuses stored things warns once", delegate
		{
			original = StorageBookmark.Capture("orig", zoneFilter);
			messagesBefore = Boot.Messages.Count;
			CallStorage("ApplyBookmark", new StorageBookmark(), zoneFilter, zoneParent, StorageBookmark.Mode.Replace);
		}, 0, delegate
		{
			List<Message> news = Boot.Messages.Skip(messagesBefore).ToList();
			int refused = zoneFilter.AllowedDefCount;
			original.ApplyTo(zoneFilter, zoneParent, StorageBookmark.Mode.Replace);
			return First(Expect(refused == 0, "Replace with empty bookmark left " + refused + " defs"), Expect(news.Count(m => m.def == MessageTypeDefOf.CautionInput) == 1, "caution messages: " + news.Count + " " + string.Join(" | ", news.Select((Message m) => m.text))), Expect(Allowed(zoneFilter).SetEquals(original.allowedDefs.Select(ThingDef.Named)), "filter not restored"));
		});
		yield return ("storage: expand all opens every non-empty branch", delegate
		{
			object root = AccessTools.Method(typeof(StoragePanel), "RootFor").Invoke(null, new object[2] { zoneFilter, zoneParent });
			CallStorage("ExpandBranch", ((TreeNode_ThingCategory)root).catDef);
		}, 30, delegate
		{
			object root = AccessTools.Method(typeof(StoragePanel), "RootFor").Invoke(null, new object[2] { zoneFilter, zoneParent });
			HashSet<ThingCategoryDef> expanded = (HashSet<ThingCategoryDef>)StorageField("expanded");
			bool any = (bool)CallStorage("AnyBranchOpen", ((TreeNode_ThingCategory)root).catDef);
			int count = expanded.Count;
			return First(Expect(count > 0, "nothing expanded"), Expect(any, "AnyBranchOpen false"), Guarded());
		});
		float fullH = 0f;
		yield return ("tree: stepped height matches a full walk at the top", delegate
		{
			FullWalkAt(0f);
		}, 10, delegate
		{
			fullH = Boot.FullWalkH;
			int memo = ((System.Collections.IDictionary)StorageField("subtreeH")).Count;
			return First(Expect(fullH > 1000f, "tree not expanded: " + fullH), MatchesFullWalk("at the top"), Expect(memo > 0, "no subtree height kept"), Guarded());
		});
		yield return ("tree: stepped height matches a full walk mid-way", delegate
		{
			FullWalkAt(fullH * 0.5f);
		}, 10, () => First(MatchesFullWalk("mid-way"), Expect((float)((Vector2)StorageField("scroll")).y > 1000f, "scroll not mid-way")));
		yield return ("tree: stepped height matches a full walk at the bottom", delegate
		{
			FullWalkAt(fullH);
		}, 10, () => MatchesFullWalk("at the bottom"));
		ThingCategoryDef folded = null;
		float foldedH = 0f;
		yield return ("tree: folding a branch shrinks the tree by that branch", delegate
		{
			HashSet<ThingCategoryDef> expanded = (HashSet<ThingCategoryDef>)StorageField("expanded");
			Dictionary<ThingCategoryDef, float> heights = (Dictionary<ThingCategoryDef, float>)StorageField("subtreeH");
			folded = heights.Keys.Where((ThingCategoryDef c) => expanded.Contains(c) && heights[c] > 100f).OrderByDescending((ThingCategoryDef c) => heights[c]).First();
			foldedH = heights[folded];
			expanded.Remove(folded);
			FullWalkAt(fullH * 0.5f);
		}, 10, () => First(MatchesFullWalk("after folding"), Expect(Mathf.Abs(ContentH - (fullH - foldedH + 24f)) < 0.5f, "folding " + folded.defName + " (" + foldedH + "): " + fullH + " -> " + ContentH)));
		yield return ("tree: unfolding restores the height", delegate
		{
			((HashSet<ThingCategoryDef>)StorageField("expanded")).Add(folded);
			FullWalkAt(fullH * 0.5f);
		}, 10, () => First(MatchesFullWalk("after unfolding"), Expect(Mathf.Abs(ContentH - fullH) < 0.5f, "height " + fullH + " -> " + ContentH)));
		float searchH = 0f;
		yield return ("tree: a search reshapes the tree", delegate
		{
			AccessTools.Field(typeof(StoragePanel), "query").SetValue(null, "steel");
			FullWalkAt(0f);
		}, 10, delegate
		{
			searchH = ContentH;
			return First(MatchesFullWalk("during the search"), Expect(searchH < fullH - 100f, "search height " + searchH + " vs " + fullH));
		});
		yield return ("tree: clearing the search restores the height", delegate
		{
			AccessTools.Field(typeof(StoragePanel), "query").SetValue(null, "");
			FullWalkAt(fullH * 0.5f);
		}, 10, () => First(MatchesFullWalk("after the search"), Expect(Mathf.Abs(ContentH - fullH) < 0.5f, "height " + fullH + " -> " + ContentH)));
		ThingDef revealed = null;
		yield return ("tree: revealing an item from the contents scrolls to its row", delegate
		{
			// From the bottom of the expanded tree, where the subtrees above the view are stepped over.
			SetScroll(fullH);
			revealed = DefDatabase<ThingDef>.GetNamed("Apparel_Parka");
			CallStorage("Reveal", revealed);
		}, 15, delegate
		{
			Vector2 s = (Vector2)StorageField("scroll");
			bool pending = (bool)StorageField("revealScroll");
			float bottom = fullH - (float)StorageField("lastViewH");
			return First(Expect(!pending, "reveal still pending"), Expect(s.y < bottom - 50f, "scroll " + s.y + " stayed near the bottom " + bottom), Expect(StorageField("revealDef") == revealed, "revealDef " + StorageField("revealDef")), Guarded());
		});
		yield return ("tree: collapse after the tree checks", delegate
		{
			((HashSet<ThingCategoryDef>)StorageField("expanded")).Clear();
			ShapeDirty();
			SetScroll(0f);
		}, 5, () => Guarded());
		yield return ("storage: shelf resolves as a thing source", delegate
		{
			CloseTab();
			Fixture.Select(Fixture.Shelves[0]);
			OpenTab<ITab_Storage>();
		}, 40, delegate
		{
			return First(Expect(StorageField("lastParent") == Fixture.Shelves[0], "panel drew " + StorageField("lastParent")), Expect(StoragePanel.LastSource == StoragePanel.Source.Thing, "source " + StoragePanel.LastSource), Guarded());
		});

		// Materials dialog
		Dialog_Materials dialog = null;
		yield return ("materials dialog: opens and draws", delegate
		{
			CloseTab();
			Fixture.Select();
			ThingFilter f = new ThingFilter();
			f.SetAllowAll(null);
			dialog = new Dialog_Materials(f, (List<SpecialThingFilterDef>)AccessTools.Field(typeof(MaterialFilters), "Generated").GetValue(null));
			Find.WindowStack.Add(dialog);
		}, 30, delegate
		{
			bool open = Find.WindowStack.IsOpen(dialog);
			dialog.Close(doCloseSound: false);
			return Expect(open, "dialog closed itself");
		});

		// Pawns hauling with the special filters
		foreach (ValueTuple<string, Action, int, Func<string>> step in HaulChecks.Steps())
		{
			yield return step;
		}

		// Final state
		yield return ("end: no trip, FlatScroll styles registered at most once", null, 0, delegate
		{
			int registrations = (int)AccessTools.Field(FixType(), "registrations").GetValue(null);
			int expected = GenCommandLine.CommandLineArgPassed("ruinoflatfix") ? 0 : 1;
			return First(Guarded(), Expect(registrations == expected, "registrations " + registrations + ", expected " + expected));
		});
	}
}
