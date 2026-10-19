using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RadiusUIInspector;
using RimWorld;
using UnityEngine;
using Verse;

namespace RUIInspectorTests;

// Smoothness probe for the measurement scene ("fluency <scene> [frames] [vanillafirst]"). Cycles every 600 frames
// through three states on the same map and at the same moment of the game, so the game's own allocations cancel out:
//   off      nothing selected;
//   plus     the scene drawn by Inspector Plus;
//   vanilla  the same scene with Inspector Plus switched off (Settings.enabled), drawn by the vanilla pane.
// Inspector Plus's own share is "plus - vanilla". Per state it records memory allocated per frame (sum of the rises
// of GC.GetTotalMemory: the per-thread counter is always 0 under Unity's Boehm GC), gen-0 collections and frame times.
// The frame-rate cap is lifted during the run, otherwise every frame lasts 33 ms and frame times say nothing.
// The first 30 frames after each switch are reported apart: they hold the one-off cost of opening a panel.
// "vanillafirst" runs the vanilla state before the plus state: in a fresh game, the first opening of a panel then
// measures only Inspector Plus's own first-run cost, the vanilla code it also runs being warm already.
// The pane is pinned during the run, otherwise it minimizes after a few seconds without the mouse on it.
internal class Fluency
{
	private const int Block = 600;

	private const int SwitchFrames = 30;

	private static readonly string[] Names = new string[3] { "off", "plus", "vanilla" };

	private class Bucket
	{
		public readonly List<float> Ms = new List<float>();

		public readonly List<double> BlockKb = new List<double>();

		public int Frames;

		public long Bytes;

		public int Collections;

		public float SwitchMax;

		public long SwitchBytes;

		public string Seen;
	}

	private readonly string scene;

	private readonly int[] order;

	private readonly int total;

	private readonly string outPath;

	private readonly Bucket[] buckets = new Bucket[3]
	{
		new Bucket(),
		new Bucket(),
		new Bucket()
	};

	private readonly bool wasPinned;

	private readonly bool wasEnabled;

	private readonly int wasVSync;

	private readonly int wasTargetFps;

	private int frame = -1;

	private long lastMem;

	private int lastGc;

	private long blockBytes;

	private string applied;

	public bool Done { get; private set; }

	public Fluency(string scene, int total, bool vanillaFirst, string dir)
	{
		this.scene = scene;
		order = vanillaFirst ? new int[3] { 0, 2, 1 } : new int[3] { 0, 1, 2 };
		this.total = Math.Max(Block * 3, total / (Block * 3) * Block * 3);
		outPath = Path.Combine(dir, "fluency-" + scene + ".txt");
		if (File.Exists(outPath))
		{
			File.Delete(outPath);
		}
		wasPinned = InspectorCore.Pinned;
		wasEnabled = InspectorMod.Settings.enabled;
		wasVSync = QualitySettings.vSyncCount;
		wasTargetFps = Application.targetFrameRate;
		InspectorCore.Pinned = true;
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = -1;
	}

	public static bool Known(string scene)
	{
		return scene == "pawn" || scene == "health" || scene == "pawns" || scene == "zone" || scene == "shelf";
	}

	public void Update()
	{
		if (Done)
		{
			return;
		}
		if (frame < 0)
		{
			Apply(0);
			frame = 0;
			lastMem = GC.GetTotalMemory(forceFullCollection: false);
			lastGc = GC.CollectionCount(0);
			return;
		}
		long mem = GC.GetTotalMemory(forceFullCollection: false);
		int gc = GC.CollectionCount(0);
		float ms = Time.unscaledDeltaTime * 1000f;
		int state = order[frame / Block % 3];
		int inBlock = frame % Block;
		Bucket bucket = buckets[state];
		long rise = Math.Max(0L, mem - lastMem);
		bucket.Frames++;
		bucket.Bytes += rise;
		bucket.Collections += gc - lastGc;
		if (inBlock < SwitchFrames)
		{
			bucket.SwitchMax = Math.Max(bucket.SwitchMax, ms);
			bucket.SwitchBytes += rise;
		}
		else
		{
			bucket.Ms.Add(ms);
		}
		if (inBlock == 60 && bucket.Seen == null)
		{
			bucket.Seen = applied + "; mode=" + InspectorCore.Mode + " native=" + InspectorCore.NativeKind + " hosting=" + InspectorCore.HostingTab
				+ " openTab=" + (((IInspectPane)(MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow).OpenTabType?.Name ?? "none");
		}
		blockBytes += rise;
		lastMem = mem;
		lastGc = gc;
		frame++;
		if (frame % Block == 0)
		{
			bucket.BlockKb.Add(blockBytes / 1024.0 / Block);
			blockBytes = 0L;
			if (frame >= total)
			{
				Finish();
				return;
			}
			Apply(order[frame / Block % 3]);
		}
	}

	private void Apply(int state)
	{
		InspectorCore.Poke();
		SceneServer.Select("none");
		InspectorMod.Settings.enabled = state == 1 || (state == 0 && wasEnabled);
		if (state == 0)
		{
			applied = "none";
			return;
		}
		switch (scene)
		{
		case "health":
			applied = SceneServer.Select("pawn") + " " + SceneServer.Tab("health");
			break;
		case "zone":
		case "shelf":
			applied = SceneServer.Select(scene) + " " + SceneServer.Tab("storage");
			break;
		default:
			applied = SceneServer.Select(scene);
			break;
		}
	}

	private void Finish()
	{
		Done = true;
		SceneServer.Select("none");
		InspectorMod.Settings.enabled = wasEnabled;
		InspectorCore.Pinned = wasPinned;
		QualitySettings.vSyncCount = wasVSync;
		Application.targetFrameRate = wasTargetFps;
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("scene " + scene + ", " + total + " frames, " + Block + " per block, speed " + Find.TickManager.CurTimeSpeed
			+ ", order " + string.Join(" ", order.Select((int i) => Names[i])));
		for (int i = 1; i < 3; i++)
		{
			sb.AppendLine(Names[i] + " showed: " + buckets[i].Seen);
		}
		sb.AppendLine("state    KB/frame  per block            gen0   p50    p95    p99    max  >25ms >40ms | switch: max ms, KB");
		for (int j = 0; j < 3; j++)
		{
			Line(sb, Names[j], buckets[j]);
		}
		sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "plus - vanilla: {0:+0.0;-0.0} KB/frame, plus - off: {1:+0.0;-0.0}, vanilla - off: {2:+0.0;-0.0}",
			Kb(buckets[1]) - Kb(buckets[2]), Kb(buckets[1]) - Kb(buckets[0]), Kb(buckets[2]) - Kb(buckets[0])));
		File.WriteAllText(outPath, sb.ToString());
	}

	private static double Kb(Bucket b)
	{
		return b.Bytes / 1024.0 / Math.Max(1, b.Frames);
	}

	private static void Line(StringBuilder sb, string name, Bucket b)
	{
		List<float> ms = b.Ms.OrderBy((float x) => x).ToList();
		string blocks = string.Join(" ", b.BlockKb.Select((double x) => x.ToString("F1", CultureInfo.InvariantCulture)));
		sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-8} {1,8:F1}  {2,-20} {3,4} {4,6:F1} {5,6:F1} {6,6:F1} {7,6:F1} {8,5} {9,5} | {10:F1} ms, {11:F1} KB",
			name, Kb(b), blocks, b.Collections, Pct(ms, 0.5f), Pct(ms, 0.95f), Pct(ms, 0.99f), ms.Count > 0 ? ms[ms.Count - 1] : 0f,
			ms.Count((float x) => x > 25f), ms.Count((float x) => x > 40f), b.SwitchMax, b.SwitchBytes / 1024.0));
	}

	private static float Pct(List<float> sorted, float p)
	{
		return sorted.Count == 0 ? 0f : sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1)];
	}
}
