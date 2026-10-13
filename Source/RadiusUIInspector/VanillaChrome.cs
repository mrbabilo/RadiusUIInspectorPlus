using System;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Verse.Steam;

namespace RadiusUIInspector;

public static class VanillaChrome
{
	public const float Height = 30f;

	public const float RunSeparator = 8f;

	public static float Draw(IInspectPane pane, float rightEdge, float y, float maxWidth)
	{
		float num = rightEdge;
		if (maxWidth < 72f)
		{
			maxWidth = 72f;
		}
		try
		{
			if (pane.ShouldShowSelectNextInCellButton)
			{
				Rect rect = new Rect(num - 24f, y, 24f, 24f);
				MouseoverSounds.DoRegion(rect);
				bool flag = Mouse.IsOver(rect);
				IconNorm.DrawTexture(TexButton.SelectOverlappingNext, rect.ContractedBy(4f), flag ? RadiusTheme.Accent : Palette.TextDim);
				if (Widgets.ButtonInvisible(rect))
				{
					pane.SelectNextInCell();
					InspectorCore.Poke();
				}
				if (SteamDeck.IsSteamDeckInNonKeyboardMode)
				{
					TooltipHandler.TipRegionByKey(rect, "SelectNextInSquareTipController");
				}
				else
				{
					TooltipHandler.TipRegionByKey(rect, "SelectNextInSquareTip", KeyBindingDefOf.SelectNextInCell.MainKeyLabel);
				}
				num -= 28f;
			}
			float lineEndWidth = 0f;
			// Vanilla lays its buttons out against the full pane row. A single thing's buttons end 24px short of the
			// right edge; a zone, plan or storage group's rename button sits flush right and steps 24px left only when
			// select-next shows. Pick the group's right edge so the last button ends on num in every case.
			bool singleThing = Find.Selector.NumSelected == 1 && Find.Selector.SingleSelectedThing != null;
			float num2 = num + ((singleThing || pane.ShouldShowSelectNextInCellButton) ? 24f : 0f);
			Widgets.BeginGroup(new Rect(num2 - maxWidth, y, maxWidth, 30f));
			try
			{
				pane.DoInspectPaneButtons(new Rect(0f, 0f, maxWidth, 30f), ref lineEndWidth);
			}
			finally
			{
				Widgets.EndGroup();
			}
			if (lineEndWidth > 0f)
			{
				lineEndWidth += 8f;
			}
			num -= lineEndWidth;
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Vanilla pane buttons failed: " + ex.Message, 5351401);
		}
		finally
		{
			GUI.color = Color.white;
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
		}
		return num;
	}

	public static void DrawReadout(IInspectPane pane, Rect rect)
	{
		try
		{
			pane.DoPaneContents(rect);
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Vanilla pane contents failed: " + ex.Message, 5351402);
		}
		finally
		{
			GUI.color = Color.white;
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			Text.WordWrap = true;
		}
	}
}
