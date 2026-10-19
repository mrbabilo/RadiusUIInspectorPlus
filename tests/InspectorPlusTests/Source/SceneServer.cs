using System;
using System.Globalization;
using System.IO;
using System.Linq;
using RadiusUIInspector;
using RimWorld;
using UnityEngine;
using Verse;

namespace RUIInspectorTests;

// Measurement scenes (-ruiscene=<dir>) for Dubs Performance Analyzer: builds the fixture, then selects things and
// opens tabs on command so DPA can profile each panel. No Harmony patch in this mode: whatever DPA shows is the game
// and the mods loaded. Commands come from <dir>/scene.txt ("<id> <verb> [args]"), acks go to <dir>/scene-ack.txt,
// state to <dir>/scene-state.json.
public class SceneServer : GameComponent
{
	private static string dir;

	private bool ready;

	private int frames;

	private long lastCmd = -1;

	private int ackFrame = -1;

	private string ackText;

	private float fpsAccum;

	private int fpsFrames;

	private float fps;

	private Dialog_Materials dialog;

	private Fluency fluency;

	internal static void Bind()
	{
		if (GenCommandLine.TryGetCommandLineArg("ruiscene", out dir))
		{
			Application.runInBackground = true;
		}
	}

	public SceneServer(Game game)
	{
	}

	private static IInspectPane Pane => (MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;

	public override void GameComponentUpdate()
	{
		if (dir == null || Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent)
		{
			return;
		}
		fpsAccum += Time.unscaledDeltaTime;
		fpsFrames++;
		if (fpsAccum >= 1f)
		{
			fps = fpsFrames / fpsAccum;
			fpsAccum = 0f;
			fpsFrames = 0;
		}
		if (!ready)
		{
			if (++frames < 120)
			{
				return;
			}
			ready = true;
			Find.TickManager.Pause();
			string built;
			try
			{
				Fixture.Build(20);
				built = "colonists=" + Find.CurrentMap.mapPawns.FreeColonistsSpawnedCount + " stockpile=" + Fixture.Stockpile.Cells.Count;
			}
			catch (Exception ex)
			{
				built = "fixture failed: " + ex.Message;
			}
			WriteState();
			File.WriteAllText(Path.Combine(dir, "scene-ack.txt"), "0 ready " + built);
			return;
		}
		if (ackFrame >= 0 && Time.frameCount >= ackFrame)
		{
			File.WriteAllText(Path.Combine(dir, "scene-ack.txt"), ackText);
			ackFrame = -1;
		}
		if (fluency != null)
		{
			fluency.Update();
			if (fluency.Done)
			{
				fluency = null;
			}
		}
		if (Time.frameCount % 30 == 0)
		{
			WriteState();
		}
		if (Time.frameCount % 10 != 0)
		{
			return;
		}
		string path = Path.Combine(dir, "scene.txt");
		if (!File.Exists(path))
		{
			return;
		}
		string[] array;
		try
		{
			array = File.ReadAllText(path).Trim().Split(new char[1] { ' ' }, 3);
		}
		catch (IOException)
		{
			return;
		}
		if (array.Length < 2 || !long.TryParse(array[0], out var id) || id <= lastCmd)
		{
			return;
		}
		lastCmd = id;
		string note = "ok";
		int delay = 5;
		try
		{
			switch (array[1])
			{
			case "select":
				note = Select(array.Length > 2 ? array[2] : "none");
				break;
			case "tab":
				note = Tab(array.Length > 2 ? array[2] : "none");
				break;
			case "materials":
				CloseDialog();
				ThingFilter filter = new ThingFilter();
				filter.SetAllowAll(null);
				dialog = new Dialog_Materials(filter, (System.Collections.Generic.List<SpecialThingFilterDef>)HarmonyLib.AccessTools.Field(typeof(MaterialFilters), "Generated").GetValue(null));
				Find.WindowStack.Add(dialog);
				break;
			case "fluency":
			{
				string[] args = (array.Length > 2 ? array[2] : "").Split(' ');
				if (!Fluency.Known(args[0]))
				{
					note = "unknown scene";
					break;
				}
				CloseDialog();
				fluency = new Fluency(args[0], args.Length > 1 ? int.Parse(args[1]) : 3600, args.Length > 2 && args[2] == "vanillafirst", dir);
				note = "started, result in fluency-" + args[0] + ".txt";
				break;
			}
			case "closeall":
				CloseDialog();
				Pane.CloseOpenTab();
				Find.Selector.ClearSelection();
				break;
			case "speed":
				Find.TickManager.CurTimeSpeed = (TimeSpeed)int.Parse(array[2]);
				break;
			case "shot":
				ScreenCapture.CaptureScreenshot(Path.Combine(dir, array[2]));
				break;
			case "quit":
				File.WriteAllText(Path.Combine(dir, "scene-ack.txt"), id + " bye");
				Root.Shutdown();
				return;
			default:
				note = "unknown verb";
				break;
			}
		}
		catch (Exception ex)
		{
			note = "failed: " + ex.Message;
		}
		ackText = id + " " + note;
		ackFrame = Time.frameCount + delay;
	}

	private void CloseDialog()
	{
		if (dialog != null)
		{
			dialog.Close(doCloseSound: false);
			dialog = null;
		}
	}

	internal static string Select(string what)
	{
		Pane.CloseOpenTab();
		switch (what)
		{
		case "pawn":
			Fixture.Select(Fixture.Pawns[0]);
			break;
		case "pawns":
			Fixture.Select(Fixture.Pawns.Cast<object>().ToArray());
			break;
		case "pawns3":
			Fixture.Select(Fixture.Pawns.Take(3).Cast<object>().ToArray());
			break;
		case "bed":
			Fixture.Select(Fixture.Bed);
			break;
		case "zone":
			Fixture.Select(Fixture.Stockpile);
			break;
		case "shelf":
			Fixture.Select(Fixture.Shelves[0]);
			break;
		default:
			Fixture.Select();
			break;
		}
		if (Find.Selector.NumSelected > 0 && Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect)
		{
			Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect, playSound: false);
		}
		return "selected=" + Find.Selector.NumSelected;
	}

	internal static string Tab(string which)
	{
		IInspectPane pane = Pane;
		Type type = which switch
		{
			"storage" => typeof(ITab_Storage),
			"health" => typeof(ITab_Pawn_Health),
			"gear" => typeof(ITab_Pawn_Gear),
			"needs" => typeof(ITab_Pawn_Needs),
			_ => null,
		};
		if (type == null)
		{
			pane.CloseOpenTab();
			return "closed";
		}
		InspectTabBase tab = pane.CurTabs?.FirstOrDefault((InspectTabBase t) => type.IsInstanceOfType(t));
		if (tab == null)
		{
			return "tab not offered";
		}
		if (pane.OpenTabType != type)
		{
			TabPlumbing.Toggle(tab, pane);
		}
		return "open=" + pane.OpenTabType?.Name;
	}

	private void WriteState()
	{
		File.WriteAllText(Path.Combine(dir, "scene-state.json"), "{\"frame\":" + Time.frameCount
			+ ",\"fps\":" + fps.ToString("F1", CultureInfo.InvariantCulture)
			+ ",\"selected\":" + Find.Selector.NumSelected
			+ ",\"mode\":\"" + InspectorCore.Mode + "\""
			+ ",\"native\":\"" + InspectorCore.NativeKind + "\""
			+ ",\"hosting\":" + (InspectorCore.HostingTab ? "true" : "false")
			+ ",\"tripped\":" + (InspectorCore.KillSwitch || InspectorCore.Guard.Disabled ? "true" : "false")
			+ ",\"windows\":" + Find.WindowStack.Count
			+ ",\"fluency\":" + (fluency != null ? "true" : "false") + "}");
	}
}
