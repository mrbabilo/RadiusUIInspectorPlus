using RadiusUI.Framework;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public class RadiusWindowDrawing : IWindowDrawing
{
	private GUIStyle? empty;

	public GUIStyle EmptyStyle
	{
		get
		{
			if (empty == null)
			{
				empty = new GUIStyle();
				empty.name = "RadiusEmpty";
			}
			return empty;
		}
	}

	public void DoWindowBackground(Rect rect)
	{
		CardChrome.Card(rect);
	}

	public bool DoCloseButton(Rect rect, string text)
	{
		bool flag = Mouse.IsOver(rect);
		CardChrome.Rounded(rect, flag ? Palette.Surface3 : Palette.Surface2, 8f);
		RadiusFont.Label(rect, text, GameFont.Small, heading: false, flag ? Palette.Ink : Palette.TextDim, TextAnchor.MiddleCenter, wrap: false);
		return Widgets.ButtonInvisible(rect);
	}

	public bool DoClostButtonSmall(Rect rect)
	{
		Rect rect2 = new Rect(rect.xMax - 30f, rect.y + 6f, 24f, 24f);
		bool flag = Mouse.IsOver(rect2);
		if (flag)
		{
			CardChrome.Rounded(rect2, Palette.Surface3, 8f);
		}
		IconSet.Get("Action/Decline").Draw(rect2.ContractedBy(6f), flag ? Palette.Bad : Palette.TextDim);
		return Widgets.ButtonInvisible(rect2);
	}

	public void BeginGroup(Rect rect)
	{
		Widgets.BeginGroup(rect);
	}

	public void EndGroup()
	{
		Widgets.EndGroup();
	}

	public void DoGrayOut(Rect rect)
	{
		Widgets.DrawRectFast(rect, new Color(0f, 0f, 0f, 0.5f));
	}
}
