using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RadiusUI.Framework;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

// Radius UI Framework's FlatScroll swaps eight GUI.skin scrollbar styles through their setters, on entry and on exit
// of every scroll view. Each setter makes Unity rebuild the skin's whole style cache (GUISkin.Apply, BuildStyleCache):
// 16 rebuilds per scroll view, about 0.5 ms per frame, and spikes from the dictionaries it allocates.
// Unity reads only the two bar styles from the skin. It finds the thumb and the buttons by name (bar name + "thumb",
// "upbutton", "downbutton", "leftbutton", "rightbutton"). So this fix registers its own named thumbs and buttons in
// customStyles once, and FlatScroll's Push and Restore only write the two bar fields: no rebuild per frame.
// The bars stay out of customStyles: a cache rebuild renames whatever sits in a built-in slot, and a registered style
// renamed "verticalscrollbar" would then replace the vanilla one for good.
// The same file ships in Colonist Bar Plus and Inspector Plus. The first one loaded patches, under one Harmony id.
// Checked against framework version 32: any other version keeps the original code. "-ruinoflatfix" turns it off.
[StaticConstructorOnStartup]
internal static class FlatScrollFix
{
	private const string HarmonyId = "mrbabilo.radiusui.flatscrollfix";

	private const int CheckedFrameworkVersion = 32;

	private const string Prefix = "RUIFix.";

	private const string VName = "RUIFix.VScroll";

	private const string HName = "RUIFix.HScroll";

	private static AccessTools.FieldRef<GUISkin, GUIStyle> skinVBar;

	private static AccessTools.FieldRef<GUISkin, GUIStyle> skinHBar;

	private static AccessTools.FieldRef<int> depthRef;

	private static AccessTools.FieldRef<GUIStyle> flatBarRef;

	private static AccessTools.FieldRef<GUIStyle> flatThumbRef;

	private static AccessTools.FieldRef<GUIStyle> flatHBarRef;

	private static AccessTools.FieldRef<GUIStyle> flatHThumbRef;

	private static AccessTools.FieldRef<GUIStyle> noButtonRef;

	private static AccessTools.FieldRef<Texture2D> trackTexRef;

	private static AccessTools.FieldRef<Texture2D> thumbTexRef;

	private static AccessTools.FieldRef<Texture2D> thumbHotTexRef;

	private static Action ensureStyles;

	private static GUIStyle vBar;

	private static GUIStyle vThumb;

	private static GUIStyle vUp;

	private static GUIStyle vDown;

	private static GUIStyle hBar;

	private static GUIStyle hThumb;

	private static GUIStyle hLeft;

	private static GUIStyle hRight;

	private static Texture2D builtTrack;

	private static Texture2D builtThumb;

	private static Texture2D builtThumbHot;

	private static GUISkin pushedSkin;

	private static GUIStyle savedV;

	private static GUIStyle savedH;

	private static int registrations;

	static FlatScrollFix()
	{
		try
		{
			Install();
		}
		catch (Exception ex)
		{
			Log.Warning("[Radius UI - FlatScroll fix] skipped: " + ex.Message);
		}
	}

	private static void Install()
	{
		if (GenCommandLine.CommandLineArgPassed("ruinoflatfix"))
		{
			Log.Message("[Radius UI - FlatScroll fix] off (-ruinoflatfix).");
			return;
		}
		Type t = typeof(FlatScroll);
		object version = typeof(FrameworkVersion).GetField("Current", BindingFlags.Static | BindingFlags.Public)?.GetRawConstantValue();
		if (!(version is int v) || v != CheckedFrameworkVersion)
		{
			Log.Warning("[Radius UI - FlatScroll fix] skipped: framework version " + (version ?? "?") + ", checked against " + CheckedFrameworkVersion + ".");
			return;
		}
		MethodInfo push = AccessTools.Method(t, "Push");
		MethodInfo restore = AccessTools.Method(t, "Restore");
		MethodInfo apply = AccessTools.Method(t, "Apply");
		MethodInfo ensure = AccessTools.Method(t, "EnsureStyles");
		if (push == null || restore == null || apply == null || ensure == null)
		{
			Log.Warning("[Radius UI - FlatScroll fix] skipped: FlatScroll members not found.");
			return;
		}
		Patches info = Harmony.GetPatchInfo(push);
		if (info != null && info.Owners.Contains(HarmonyId))
		{
			return;
		}
		skinVBar = AccessTools.FieldRefAccess<GUISkin, GUIStyle>("m_verticalScrollbar");
		skinHBar = AccessTools.FieldRefAccess<GUISkin, GUIStyle>("m_horizontalScrollbar");
		depthRef = AccessTools.StaticFieldRefAccess<int>(AccessTools.Field(t, "depth"));
		flatBarRef = AccessTools.StaticFieldRefAccess<GUIStyle>(AccessTools.Field(t, "flatBar"));
		flatThumbRef = AccessTools.StaticFieldRefAccess<GUIStyle>(AccessTools.Field(t, "flatThumb"));
		flatHBarRef = AccessTools.StaticFieldRefAccess<GUIStyle>(AccessTools.Field(t, "flatHBar"));
		flatHThumbRef = AccessTools.StaticFieldRefAccess<GUIStyle>(AccessTools.Field(t, "flatHThumb"));
		noButtonRef = AccessTools.StaticFieldRefAccess<GUIStyle>(AccessTools.Field(t, "noButton"));
		trackTexRef = AccessTools.StaticFieldRefAccess<Texture2D>(AccessTools.Field(t, "trackTex"));
		thumbTexRef = AccessTools.StaticFieldRefAccess<Texture2D>(AccessTools.Field(t, "thumbTex"));
		thumbHotTexRef = AccessTools.StaticFieldRefAccess<Texture2D>(AccessTools.Field(t, "thumbHotTex"));
		ensureStyles = (Action)Delegate.CreateDelegate(typeof(Action), ensure);
		Harmony harmony = new Harmony(HarmonyId);
		try
		{
			// Restore first and Push last: a Push patched without its Restore would leave our bars in the skin.
			harmony.Patch(restore, new HarmonyMethod(typeof(FlatScrollFix), nameof(RestorePrefix)));
			harmony.Patch(apply, new HarmonyMethod(typeof(FlatScrollFix), nameof(ApplyPrefix)));
			harmony.Patch(push, new HarmonyMethod(typeof(FlatScrollFix), nameof(PushPrefix)));
		}
		catch
		{
			harmony.UnpatchAll(HarmonyId);
			throw;
		}
		Log.Message("[Radius UI - FlatScroll fix] active (" + typeof(FlatScrollFix).Assembly.GetName().Name + ").");
	}

	private static bool PushPrefix()
	{
		ensureStyles();
		ref int depth = ref depthRef();
		if (depth++ == 0)
		{
			GUISkin skin = GUI.skin;
			Sync(skin);
			pushedSkin = skin;
			savedV = skinVBar(skin);
			savedH = skinHBar(skin);
			skinVBar(skin) = vBar;
			skinHBar(skin) = hBar;
		}
		return false;
	}

	private static bool RestorePrefix()
	{
		GUISkin skin = pushedSkin;
		if ((object)skin == null)
		{
			return false;
		}
		pushedSkin = null;
		if (savedV != null)
		{
			skinVBar(skin) = savedV;
		}
		if (savedH != null)
		{
			skinHBar(skin) = savedH;
		}
		savedV = null;
		savedH = null;
		if (vBar.name != VName || hBar.name != HName)
		{
			// A cache rebuild ran inside the scroll view and renamed our bars after the built-in slots: name them
			// back and rebuild once, so the vanilla keys point to the vanilla styles again.
			vBar.name = VName;
			hBar.name = HName;
			skin.customStyles = skin.customStyles;
		}
		return false;
	}

	// Radius UI Everywhere installs the flat styles globally every frame: skip the eight setters when they are in place.
	private static bool ApplyPrefix()
	{
		GUISkin skin = GUI.skin;
		GUIStyle bar = flatBarRef();
		GUIStyle none = noButtonRef();
		return bar == null || skin.verticalScrollbar != bar || skin.verticalScrollbarThumb != flatThumbRef() || skin.verticalScrollbarUpButton != none || skin.verticalScrollbarDownButton != none || skin.horizontalScrollbar != flatHBarRef() || skin.horizontalScrollbarThumb != flatHThumbRef() || skin.horizontalScrollbarLeftButton != none || skin.horizontalScrollbarRightButton != none;
	}

	private static void Sync(GUISkin skin)
	{
		if (vBar == null || !ReferenceEquals(builtTrack, trackTexRef()) || !ReferenceEquals(builtThumb, thumbTexRef()) || !ReferenceEquals(builtThumbHot, thumbHotTexRef()))
		{
			Build();
		}
		if (skin.FindStyle(VName + "thumb") != vThumb || skin.FindStyle(HName + "thumb") != hThumb)
		{
			Register(skin);
		}
	}

	private static void Build()
	{
		builtTrack = trackTexRef();
		builtThumb = thumbTexRef();
		builtThumbHot = thumbHotTexRef();
		GUIStyle none = noButtonRef();
		vBar = Named(flatBarRef(), VName);
		vThumb = Named(flatThumbRef(), VName + "thumb");
		vUp = Named(none, VName + "upbutton");
		vDown = Named(none, VName + "downbutton");
		hBar = Named(flatHBarRef(), HName);
		hThumb = Named(flatHThumbRef(), HName + "thumb");
		hLeft = Named(none, HName + "leftbutton");
		hRight = Named(none, HName + "rightbutton");
		static GUIStyle Named(GUIStyle source, string name)
		{
			return new GUIStyle(source)
			{
				name = name
			};
		}
	}

	private static void Register(GUISkin skin)
	{
		GUIStyle[] old = skin.customStyles ?? new GUIStyle[0];
		List<GUIStyle> list = new List<GUIStyle>(old.Length + 6);
		foreach (GUIStyle s in old)
		{
			if (s == null || s.name == null || !s.name.StartsWith(Prefix, StringComparison.Ordinal))
			{
				list.Add(s);
			}
		}
		list.Add(vThumb);
		list.Add(vUp);
		list.Add(vDown);
		list.Add(hThumb);
		list.Add(hLeft);
		list.Add(hRight);
		skin.customStyles = list.ToArray();
		registrations++;
		if (Prefs.DevMode && registrations <= 3)
		{
			Log.Message("[Radius UI - FlatScroll fix] styles registered in skin " + skin.name + " (" + skin.GetInstanceID() + "), registration " + registrations + ".");
		}
		else if (registrations == 10)
		{
			Log.Warning("[Radius UI - FlatScroll fix] styles registered 10 times: the skin keeps changing, and each registration rebuilds its cache once.");
		}
	}
}
