using System;
using System.Collections.Generic;
using RadiusUI.Framework;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public static class InspectorCore
{
	public static readonly PatchGuard Guard = new PatchGuard("Radius UI - Inspector pane");

	public static bool KillSwitch;

	public static bool ExternalPaneOwner;

	public static readonly Dictionary<string, bool> Fold = new Dictionary<string, bool>();

	public static bool Minimized;

	public static bool Pinned;

	private static float lastPoke;

	private static int modeFrame = -1;

	private static PaneMode cachedMode;

	private static int cachedCount;

	private static int selHashLast;

	private static int lastRawCount = -1;

	public const float CompactHeight = 58f;

	public const float MultiStripH = 46f;

	public const float MultiGap = 4f;

	public const float TabStripH = 26f;

	public static float PillBlockH = 30f;

	public const float CardMargin = 6f;

	public const float IconBox = 24f;

	public const float IconPitch = 28f;

	public const float IconInset = 4f;

	public const float IconOptical = 16f;

	public const float HostPad = 6f;

	public const float HostBleed = 2f;

	private static float animW = -1f;

	private static float animH = -1f;

	private static int animFrame = -1;

	private static bool animInit;

	public static float FullHeight => Mathf.Min((InspectorMod.Settings != null) ? ((float)InspectorMod.Settings.paneHeight) : 232f, (float)UI.screenHeight * 0.5f);

	public static float MultiMaxHeight => Mathf.Min(416f, (float)UI.screenHeight * 0.5f);

	public static float PaneWidth
	{
		get
		{
			if (Mode == PaneMode.Multi)
			{
				return 460f;
			}
			return Mathf.Min((InspectorMod.Settings != null) ? ((float)InspectorMod.Settings.paneWidth) : 540f, Mathf.Floor((float)UI.screenWidth * 0.6f));
		}
	}

	public static float PaneHeight
	{
		get
		{
			Classify();
			if (cachedMode == PaneMode.Multi)
			{
				return Mathf.Min(44f + (float)cachedCount * 50f - 4f + 4f, MultiMaxHeight);
			}
			if (Minimized)
			{
				return 58f;
			}
			if (cachedMode == PaneMode.SingleThing || cachedMode == PaneMode.SingleZone)
			{
				float num = ((cachedMode == PaneMode.SingleThing) ? ThingPanel.BodyContentH : ZonePanel.BodyContentH);
				return Mathf.Clamp(62f + PillBlockH + num + 14f, 120f, FullHeight);
			}
			return Mathf.Clamp(Mathf.Max(HostedContentTop + InspectorPanel.BodyContentH, 26f + InspectorPanel.RailContentH) + 14f, Mathf.Min(165f, FullHeight) + 26f, FullHeight + 26f);
		}
	}

	public static PaneMode Mode
	{
		get
		{
			Classify();
			return cachedMode;
		}
	}

	public static int MultiCount
	{
		get
		{
			Classify();
			return cachedCount;
		}
	}

	public static bool Active => Mode != PaneMode.None;

	public static float RailWidth => Mathf.Clamp(148f + (PaneWidth - 460f) * 0.25f, 140f, 213f);

	public static float ChromeAbovePills
	{
		get
		{
			if (Mode != PaneMode.SinglePawn)
			{
				return 87f;
			}
			return 62f;
		}
	}

	public static float HostedContentTop
	{
		get
		{
			if (Mode != PaneMode.SinglePawn)
			{
				return ChromeAbovePills + PillBlockH;
			}
			return ChromeAbovePills;
		}
	}

	public static bool AnimSettled { get; private set; }

	public static bool HostingTab { get; private set; }

	public static Vector2 HostedTabSize { get; private set; }

	public static float CurrentPaneHeight => Mathf.Round((animH > 0f) ? animH : PaneHeight);

	public static float CurrentPaneWidth => Mathf.Round((animW > 0f) ? animW : PaneWidth);

	public static float AnimTargetH { get; private set; }

	public static NativeTabKind NativeKind { get; private set; }

	public static bool NativeTabOpen => NativeKind != NativeTabKind.None;

	public static PawnProfileKind ProfileOf(Pawn pawn)
	{
		RaceProperties raceProperties = pawn?.RaceProps;
		if (raceProperties != null)
		{
			if (raceProperties.IsMechanoid)
			{
				return PawnProfileKind.Mech;
			}
			if (raceProperties.Animal)
			{
				return PawnProfileKind.Animal;
			}
		}
		if (pawn != null && pawn.IsPrisoner)
		{
			return PawnProfileKind.Prisoner;
		}
		return PawnProfileKind.Colonist;
	}

	public static Rect HostedTabLocalRect()
	{
		float num = 12f;
		float hostedContentTop = HostedContentTop;
		float num2 = CurrentPaneWidth - num * 2f;
		float num3 = CurrentPaneHeight - 6f - 6f - hostedContentTop;
		float b = ((HostedTabSize.x > 0f) ? Mathf.Min(HostedTabSize.x, num2) : num2);
		float b2 = ((HostedTabSize.y > 0f) ? Mathf.Min(HostedTabSize.y, num3) : num3);
		return new Rect(num, hostedContentTop, Mathf.Max(0f, b), Mathf.Max(0f, b2));
	}

	public static Rect HostedCardHole()
	{
		Rect rect = HostedTabLocalRect();
		float num = Mathf.Min(2f, rect.width * 0.5f);
		float num2 = Mathf.Min(2f, rect.height * 0.5f);
		return new Rect(rect.x + num, rect.y + num2, Mathf.Max(0f, rect.width - num * 2f), Mathf.Max(0f, rect.height - num2 * 2f));
	}

	private static float Approach(float cur, float target, float k, float floor)
	{
		float f = target - cur;
		float num = Mathf.Abs(f) * k;
		if (num < floor)
		{
			num = Mathf.Min(floor, Mathf.Abs(f));
		}
		return cur + Mathf.Sign(f) * num;
	}

	public static void SetNative(NativeTabKind kind)
	{
		NativeKind = kind;
	}

	public static void SetHosting(bool hosting, Vector2 tabSize)
	{
		HostingTab = hosting;
		HostedTabSize = tabSize;
	}

	public static Vector2 AnimatePaneSize(Vector2 target)
	{
		if (FrameGate.NewFrame(ref animFrame))
		{
			if (!animInit || animW < 0f)
			{
				animW = target.x;
				animH = target.y;
				animInit = true;
			}
			else
			{
				float num = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
				float k = 1f - Mathf.Exp((0f - num) * 16f);
				float floor = 2400f * num;
				animW = Approach(animW, target.x, k, floor);
				animH = Approach(animH, target.y, k, floor);
			}
			if (Mathf.Abs(target.x - animW) < 0.75f)
			{
				animW = target.x;
			}
			if (Mathf.Abs(target.y - animH) < 0.75f)
			{
				animH = target.y;
			}
			animFrame = Time.frameCount;
		}
		AnimTargetH = target.y;
		AnimSettled = animW == target.x && animH == target.y;
		return new Vector2(Mathf.Round(animW), Mathf.Round(animH));
	}

	private static void Classify()
	{
		bool flag = Current.ProgramState == ProgramState.Playing && Find.UIRoot is UIRoot_Play && !WorldRendererUtility.WorldRendered;
		int num = (flag ? (Find.Selector?.SelectedObjects?.Count).GetValueOrDefault() : 0);
		if (!FrameGate.NewFrame(ref modeFrame) && num == lastRawCount)
		{
			return;
		}
		lastRawCount = num;
		cachedMode = PaneMode.None;
		cachedCount = 0;
		if (KillSwitch || !Guard.ShouldRun || ExternalPaneOwner || InspectorMod.Settings == null || !InspectorMod.Settings.enabled || !flag)
		{
			return;
		}
		Selector selector = Find.Selector;
		if (selector == null)
		{
			return;
		}
		List<object> selectedObjects = selector.SelectedObjects;
		if (selectedObjects == null || selectedObjects.Count == 0)
		{
			return;
		}
		if (selectedObjects.Count == 1 && selectedObjects[0] is Zone zone)
		{
			cachedCount = 1;
			cachedMode = PaneMode.SingleZone;
			int hashCode = zone.GetHashCode();
			if (hashCode != selHashLast)
			{
				selHashLast = hashCode;
				Poke();
			}
			return;
		}
		int num2 = selectedObjects.Count;
		bool flag2 = true;
		int num3 = 0;
		for (int i = 0; i < selectedObjects.Count; i++)
		{
			if (!(selectedObjects[i] is Thing thing) || thing.def.hideInspect)
			{
				return;
			}
			if (!(thing is Pawn))
			{
				flag2 = false;
			}
			if (thing is IStorageGroupMember)
			{
				num3++;
			}
			num2 = num2 * 31 + thing.thingIDNumber;
		}
		if (selectedObjects.Count <= 1 || num3 != selectedObjects.Count)
		{
			cachedCount = selectedObjects.Count;
			cachedMode = ((selectedObjects.Count > 1) ? PaneMode.Multi : (flag2 ? PaneMode.SinglePawn : PaneMode.SingleThing));
			if (num2 != selHashLast)
			{
				selHashLast = num2;
				Poke();
			}
		}
	}

	public static void Poke()
	{
		lastPoke = Time.realtimeSinceStartup;
		Minimized = false;
	}

	public static void PokeSoft()
	{
		lastPoke = Time.realtimeSinceStartup;
	}

	public static void TickMinimize()
	{
		InspectorSettings settings = InspectorMod.Settings;
		if (!Minimized && !Pinned && settings.autoMinimize && Time.realtimeSinceStartup - lastPoke > settings.minimizeDelay)
		{
			Minimized = true;
		}
	}

	public static bool Folded(string id)
	{
		bool value;
		return Fold.TryGetValue(id, out value) && value;
	}

	public static void ToggleFold(string id)
	{
		Fold[id] = !Folded(id);
	}

	public static void Trip(Exception e)
	{
		Guard.Fail(e);
		if (!Guard.ShouldRun && !KillSwitch)
		{
			KillSwitch = true;
			Log.Error("[Radius UI - Inspector] Falling back to the vanilla inspect pane for this session.");
		}
	}
}
