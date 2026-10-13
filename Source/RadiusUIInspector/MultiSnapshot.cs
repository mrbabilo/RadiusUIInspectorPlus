using System;
using System.Collections.Generic;
using System.Text;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public class MultiSnapshot
{
	private readonly SnapshotTable<MultiEntry> table = new SnapshotTable<MultiEntry>();

	private readonly HashSet<int> live = new HashSet<int>();

	public readonly List<MultiEntry> Entries = new List<MultiEntry>(16);

	private int frameStamp = -1;

	private int lastHash;

	private float nextPrune;

	public int TotalCount;

	public string SummaryLine = "";

	private static readonly Dictionary<ThingDef, int> defCounts = new Dictionary<ThingDef, int>();

	private static readonly List<KeyValuePair<ThingDef, int>> defSorted = new List<KeyValuePair<ThingDef, int>>();

	private static readonly Comparison<KeyValuePair<ThingDef, int>> ByCountDesc = (KeyValuePair<ThingDef, int> a, KeyValuePair<ThingDef, int> b) => b.Value.CompareTo(a.Value);

	private static readonly StringBuilder sb = new StringBuilder(96);

	public int SelHash => lastHash;

	public void Refresh(List<object> selected)
	{
		if (!FrameGate.NewFrame(ref frameStamp))
		{
			return;
		}
		int num = selected.Count;
		for (int i = 0; i < selected.Count; i++)
		{
			if (selected[i] is Thing thing)
			{
				num = num * 31 + thing.thingIDNumber;
			}
		}
		bool num2 = num != lastHash || SummaryLine.Length == 0;
		lastHash = num;
		if (num2)
		{
			BuildSummary(selected);
		}
		Entries.Clear();
		live.Clear();
		for (int j = 0; j < selected.Count; j++)
		{
			if (Entries.Count >= 24)
			{
				break;
			}
			if (selected[j] is Thing thing2)
			{
				live.Add(thing2.thingIDNumber);
				bool stale;
				MultiEntry multiEntry = table.Get(thing2.thingIDNumber, out stale);
				if (stale || multiEntry.Thing != thing2)
				{
					Fill(multiEntry, thing2);
				}
				Entries.Add(multiEntry);
			}
		}
		if (Throttle.Every(ref nextPrune, 2f))
		{
			table.Prune(live);
		}
	}

	private void BuildSummary(List<object> selected)
	{
		defCounts.Clear();
		int num = 0;
		for (int i = 0; i < selected.Count; i++)
		{
			if (selected[i] is Thing thing)
			{
				num++;
				ThingDef def = thing.def;
				defCounts.TryGetValue(def, out var value);
				defCounts[def] = value + Mathf.Max(1, thing.stackCount);
			}
		}
		TotalCount = num;
		sb.Length = 0;
		sb.Append("RUI.Inspector.MultiSelected".Translate(num).Resolve());
		if (defCounts.Count > 0)
		{
			defSorted.Clear();
			foreach (KeyValuePair<ThingDef, int> defCount in defCounts)
			{
				defSorted.Add(defCount);
			}
			defSorted.Sort(ByCountDesc);
			sb.Append(" - ");
			int num2 = Mathf.Min(defSorted.Count, 4);
			for (int j = 0; j < num2; j++)
			{
				if (j > 0)
				{
					sb.Append(", ");
				}
				sb.Append(defSorted[j].Value.ToString("N0"));
				sb.Append(' ');
				sb.Append(defSorted[j].Key.label);
			}
			if (defSorted.Count > num2)
			{
				sb.Append(' ');
				sb.Append("RUI.Inspector.MultiMore".Translate(defSorted.Count - num2).Resolve());
			}
		}
		SummaryLine = sb.ToString();
	}

	private static void Fill(MultiEntry e, Thing t)
	{
		e.Thing = t;
		if (t is Pawn pawn)
		{
			e.Pawn = pawn;
			int num = 0;
			List<Hediff> list = pawn.health?.hediffSet?.hediffs;
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i].Visible && list[i].TendableNow())
					{
						num++;
					}
				}
			}
			string labelShortCap = pawn.LabelShortCap;
			Need need = pawn.needs?.mood;
			e.Name = labelShortCap;
			e.Initial = (labelShortCap.NullOrEmpty() ? "?" : labelShortCap.Substring(0, 1));
			e.MoodStr = ((need != null) ? need.CurLevelPercentage.ToStringPercent() : "");
			e.HasMood = need != null;
			e.Untended = num;
			e.UntendedStr = num.ToString();
		}
		else
		{
			string text;
			try
			{
				text = t.LabelCap;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[Radius UI - Inspector] Thing label failed: " + ex.Message, 5351394);
				text = t.def.defName;
			}
			e.Pawn = null;
			e.Name = text;
			e.Initial = (text.NullOrEmpty() ? "?" : text.Substring(0, 1).ToUpperInvariant());
			e.MoodStr = "";
			e.HasMood = false;
			e.Untended = 0;
			e.UntendedStr = "";
		}
	}
}
