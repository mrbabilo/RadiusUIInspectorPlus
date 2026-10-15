using System;
using System.Reflection;
using HarmonyLib;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

// While its own view is off, Modern Bio Tab draws its "✨ Modern Bio" switch as an 88x16 Tiny button at the top right
// of the vanilla card. In the pane it pressed against the card's header buttons, and over Bio Tab+ (whose card starts
// at the very top) it covered Bio Tab+'s own header icons, inside the pane or not.
// The switch becomes an icon (Modern Bio's own card-layout one), with "Modern Bio" as its tooltip:
// - vanilla card: in the strip right of the card when the pane leaves one, else in the margin above the card, left of
//   the tab's close cross;
// - Bio Tab+ card, anywhere: in Bio Tab+'s header, left of its gear. Clicking it also turns
//   Bio Tab+ off: both replace the same tab, and Bio Tab+ would keep setting the tab size under Modern Bio. Bio Tab+ is
//   turned back on from the vanilla card, by its own icon there.
internal static class ModernBioTabCompat
{
	internal const string PackageId = "astryl.ModernBioTab";

	internal const string SwitchPatchType = "ModernBioTab.Patch_ITab_Pawn_Character_FillTab";

	internal const string CardType = "BioTabEnhanced.EnhancedCharacterCard";

	private const string Label = "Modern Bio";

	private const string IconPath = "UI/DisplayMode";

	private static Texture2D? icon;

	private static readonly Color Tint = new Color(0.55f, 0.82f, 1f);

	// Bio Tab+'s hover tint for its header icons (EnhancedCharacterCard.HeaderHoverTint).
	private static readonly Color HeaderHover = new Color(32f / 85f, 0.69803923f, 44f / 51f);

	private static bool bound;

	private static FieldInfo? modernEnabled;

	private static Func<float>? plusLayoutScale;

	private static void Bind()
	{
		if (bound)
		{
			return;
		}
		bound = true;
		modernEnabled = AccessTools.Field(ModCompat.FindType(SwitchPatchType), "Enabled");
		if (modernEnabled == null)
		{
			Log.Warning("[Radius UI - Inspector] Modern Bio Tab is active but " + SwitchPatchType + ".Enabled did not resolve (version drift?) - its switch keeps its own drawing.");
		}
		Type? type = ModCompat.FindType(CardType);
		MethodInfo? methodInfo = (type == null) ? null : AccessTools.PropertyGetter(type, "LayoutScale");
		if (methodInfo != null)
		{
			plusLayoutScale = AccessTools.MethodDelegate<Func<float>>(methodInfo);
		}
	}

	// Null when Modern Bio could not be read.
	private static bool? ModernOn
	{
		get
		{
			Bind();
			return (modernEnabled == null) ? null : ((bool)modernEnabled.GetValue(null));
		}
	}

	private static bool PlusOn => ModCompat.IsActive(BioTabPlusCompat.PackageId) && BioTabPlusCompat.Enabled;

	// False: the original switch draws.
	internal static bool TryDrawSwitch(bool hosted)
	{
		bool? modernOn = ModernOn;
		if (!modernOn.HasValue)
		{
			return false;
		}
		if (modernOn.Value)
		{
			return true;
		}
		if (PlusOn)
		{
			// Bio Tab+ shows the switch in its header (DrawPlusIcon).
			return true;
		}
		Pawn? pawn = SelectedPawn();
		if (pawn == null)
		{
			return true;
		}
		// The vanilla card is drawn 17 px in from the tab's left edge. Without a strip, the icon goes in the 17 px margin
		// above the card, left of the tab's close cross (18 px, 4 px from the corner).
		float cardRight = 17f + CharacterCardUtility.PawnCardSize(pawn).x;
		float tabW = hosted ? InspectorCore.HostedTabLocalRect().width : (cardRight + 17f);
		Rect rect = ((tabW - cardRight >= 22f + 16f) ? new Rect(tabW - 22f - 8f, 17f, 22f, 22f) : new Rect(tabW - 26f - 17f, 0f, 17f, 17f));
		if (SwitchIcon(rect, rect.width * 0.75f, Tint, Color.white))
		{
			modernEnabled!.SetValue(null, true);
		}
		return true;
	}

	// Mirrors the layout of Bio Tab+'s right header icons (close, back to vanilla unless hidden, gear) and adds one
	// more slot on their left.
	internal static void DrawPlusIcon(Rect header, bool revertHidden)
	{
		if (ModernOn != false)
		{
			return;
		}
		float num = plusLayoutScale?.Invoke() ?? 1f;
		float size = 22f * num;
		float gap = 6f * num;
		float slots = revertHidden ? 3f : 4f;
		Rect rect = new Rect(header.xMax - 10f * num - slots * size - (slots - 1f) * gap, header.y + (header.height - size) * 0.5f, size, size);
		if (SwitchIcon(rect, 14f * num, Tint, HeaderHover))
		{
			modernEnabled!.SetValue(null, true);
			BioTabPlusCompat.Disable();
		}
	}

	// Modern Bio's own card-layout icon (the one of its display mode setting), in its blue; a letter if it is missing.
	private static bool SwitchIcon(Rect rect, float iconSize, Color normal, Color hover)
	{
		if (icon == null)
		{
			icon = ContentFinder<Texture2D>.Get(IconPath, reportFailure: false) ?? BaseContent.BadTex;
		}
		Color color = GUI.color;
		GUI.color = Mouse.IsOver(rect) ? hover : normal;
		if (icon != BaseContent.BadTex)
		{
			GUI.DrawTexture(new Rect(rect.x + (rect.width - iconSize) * 0.5f, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize), icon, ScaleMode.ScaleToFit);
		}
		else
		{
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect, "M");
			Text.Anchor = anchor;
		}
		GUI.color = color;
		TooltipHandler.TipRegion(rect, Label);
		return Widgets.ButtonInvisible(rect);
	}

	private static Pawn? SelectedPawn()
	{
		Thing? thing = Find.Selector?.SingleSelectedThing;
		if (thing is Pawn pawn)
		{
			return pawn;
		}
		return (thing as Corpse)?.InnerPawn;
	}
}

[HarmonyPatch]
internal static class Patch_ModernBioTab_Switch
{
	private static bool Prepare()
	{
		return ModCompat.IsActive(ModernBioTabCompat.PackageId) && TargetMethod() != null;
	}

	private static MethodBase? TargetMethod()
	{
		Type? type = ModCompat.FindType(ModernBioTabCompat.SwitchPatchType);
		return (type == null) ? null : AccessTools.Method(type, "Postfix");
	}

	private static bool Prefix()
	{
		try
		{
			bool hosted = InspectorCore.Active && InspectorCore.HostingTab && !InspectorMod.Settings.showVanillaTabs;
			return !ModernBioTabCompat.TryDrawSwitch(hosted);
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Modern Bio switch failed: " + ex.Message, 5351403);
			return true;
		}
	}
}

[HarmonyPatch]
internal static class Patch_BioTabPlus_HeaderActions
{
	private static bool Prepare()
	{
		return ModCompat.IsActive(ModernBioTabCompat.PackageId) && ModCompat.IsActive(BioTabPlusCompat.PackageId) && TargetMethod() != null;
	}

	private static MethodBase? TargetMethod()
	{
		Type? type = ModCompat.FindType(ModernBioTabCompat.CardType);
		return (type == null) ? null : AccessTools.Method(type, "DrawHeaderActions");
	}

	private static void Postfix(Rect rect)
	{
		try
		{
			ModernBioTabCompat.DrawPlusIcon(rect, BioTabPlusCompat.RevertHidden);
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Modern Bio icon on Bio Tab+ failed: " + ex.Message, 5351404);
		}
	}
}
