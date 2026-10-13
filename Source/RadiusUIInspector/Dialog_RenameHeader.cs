using System;
using RadiusUI.Framework;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public class Dialog_RenameHeader : Window
{
	private readonly Action<string>? onCommit;

	private readonly string title;

	private readonly string fallback;

	private string text;

	private int openFrame;

	private bool focused;

	private static GUIStyle? fieldStyle;

	public override Vector2 InitialSize => new Vector2(340f, 148f);

	private static GUIStyle FieldStyle
	{
		get
		{
			if (fieldStyle == null)
			{
				fieldStyle = new GUIStyle(Text.CurTextFieldStyle);
				fieldStyle.normal.background = null;
				fieldStyle.focused.background = null;
				fieldStyle.active.background = null;
				fieldStyle.hover.background = null;
				fieldStyle.normal.textColor = Palette.Ink;
				fieldStyle.focused.textColor = Palette.Ink;
				fieldStyle.alignment = TextAnchor.MiddleLeft;
				fieldStyle.padding = new RectOffset(9, 9, 0, 0);
			}
			return fieldStyle;
		}
	}

	public Dialog_RenameHeader(string current, Action<string> onCommit, string? title = null, string? fallback = null)
		: base(new RadiusWindowDrawing())
	{
		text = current;
		this.onCommit = onCommit;
		this.title = title ?? "RUI.Inspector.RenameHeader".Translate();
		this.fallback = fallback ?? "RUI.Inspector.WHeader".Translate();
		Chrome();
	}

	private void Chrome()
	{
		doCloseX = true;
		absorbInputAroundWindow = true;
		closeOnClickedOutside = true;
		closeOnAccept = false;
		forcePause = false;
	}

	public override void PostOpen()
	{
		base.PostOpen();
		openFrame = Time.frameCount;
	}

	public override void DoWindowContents(Rect inRect)
	{
		RadiusFont.Label(new Rect(0f, 0f, inRect.width, 24f), title, GameFont.Small, heading: true, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		Rect rect = new Rect(0f, 30f, inRect.width, 32f);
		CardChrome.Rounded(rect, Palette.Surface2, 8f);
		CardChrome.Outline(rect, Palette.Border, 1f, 8f);
		Color selectionColor = GUI.skin.settings.selectionColor;
		Color cursorColor = GUI.skin.settings.cursorColor;
		GUI.skin.settings.selectionColor = RadiusTheme.Accent * new Color(1f, 1f, 1f, 0.45f);
		GUI.skin.settings.cursorColor = RadiusTheme.Accent;
		GUI.SetNextControlName("rui_hdr_rename");
		text = GUI.TextField(rect, text, 40, FieldStyle);
		GUI.skin.settings.selectionColor = selectionColor;
		GUI.skin.settings.cursorColor = cursorColor;
		if (!focused && Time.frameCount > openFrame)
		{
			GUI.FocusControl("rui_hdr_rename");
			focused = true;
		}
		bool flag = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
		Rect rect2 = new Rect(inRect.width - 100f, inRect.height - 32f, 100f, 30f);
		bool flag2 = Mouse.IsOver(rect2);
		CardChrome.Rounded(rect2, flag2 ? RadiusTheme.Accent : Palette.Surface2, 8f);
		RadiusFont.Label(rect2, "OK".Translate(), GameFont.Small, flag2, flag2 ? Palette.InkOnAccent : Palette.Ink, TextAnchor.MiddleCenter, wrap: false);
		if (flag || Widgets.ButtonInvisible(rect2))
		{
			Commit();
			if (flag)
			{
				Event.current.Use();
			}
			Close();
		}
		Text.Font = GameFont.Small;
		Text.Anchor = TextAnchor.UpperLeft;
		GUI.color = Color.white;
	}

	private void Commit()
	{
		string text = this.text.Replace("|", "/").Trim();
		if (text.Length == 0)
		{
			text = fallback;
		}
		onCommit?.Invoke(text);
	}
}
