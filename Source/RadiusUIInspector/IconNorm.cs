using System;
using System.Collections.Generic;
using RadiusUI.Framework;
using UnityEngine;

namespace RadiusUIInspector;

internal static class IconNorm
{
	private static Dictionary<string, Rect>? baked;

	private static readonly Dictionary<Texture2D, Rect> inkMemo = new Dictionary<Texture2D, Rect>();

	private static readonly HashSet<Texture2D> unreadable = new HashSet<Texture2D>();

	private const byte AlphaCut = 12;

	private static Dictionary<string, Rect> Baked
	{
		get
		{
			if (baked != null)
			{
				return baked;
			}
			baked = new Dictionary<string, Rect>(272);
			for (int i = 0; i < IconInkData.Packed.Length; i++)
			{
				string[] array = IconInkData.Packed[i].Split(new char[1] { ';' });
				foreach (string text in array)
				{
					if (text.Length == 0)
					{
						continue;
					}
					int num = text.IndexOf('=');
					if (num > 0)
					{
						string[] array2 = text.Substring(num + 1).Split(new char[1] { ',' });
						if (array2.Length == 4 && float.TryParse(array2[0], out var result) && float.TryParse(array2[1], out var result2) && float.TryParse(array2[2], out var result3) && float.TryParse(array2[3], out var result4))
						{
							baked[text.Substring(0, num)] = new Rect(result, result2, result3, result4);
						}
					}
				}
			}
			return baked;
		}
	}

	public static void Draw(string path, Rect box, Color tint)
	{
		RadiusIcon radiusIcon = IconSet.Get(path);
		Texture2D texture = radiusIcon.Texture;
		if (!(texture == null))
		{
			if (Baked.TryGetValue(path, out var value) || TryInk(texture, out value))
			{
				DrawInk(texture, value, box, tint);
			}
			else
			{
				radiusIcon.Draw(box, tint);
			}
		}
	}

	public static void DrawTexture(Texture2D? tex, Rect box, Color tint)
	{
		if (!(tex == null))
		{
			if (!TryInk(tex, out var ink))
			{
				Color color = GUI.color;
				GUI.color = tint;
				GUI.DrawTexture(box, tex, ScaleMode.ScaleToFit);
				GUI.color = color;
			}
			else
			{
				DrawInk(tex, ink, box, tint);
			}
		}
	}

	private static void DrawInk(Texture2D tex, Rect ink, Rect box, Color tint)
	{
		if (FrameGate.Drawing)
		{
			float num = ink.width * (float)tex.width;
			float num2 = ink.height * (float)tex.height;
			if (!(num <= 0f) && !(num2 <= 0f))
			{
				float num3 = Mathf.Min(box.width, box.height);
				float num4 = Mathf.Sqrt(num * num2);
				float num5 = ((num4 > 0f) ? (num3 / num4) : 1f);
				num5 = Mathf.Min(num5, box.width / num, box.height / num2);
				float num6 = num * num5;
				float num7 = num2 * num5;
				Rect position = new Rect(box.x + (box.width - num6) * 0.5f, box.y + (box.height - num7) * 0.5f, num6, num7);
				Color color = GUI.color;
				GUI.color = tint;
				GUI.DrawTextureWithTexCoords(position, tex, ink);
				GUI.color = color;
			}
		}
	}

	private static bool TryInk(Texture2D tex, out Rect ink)
	{
		if (inkMemo.TryGetValue(tex, out ink))
		{
			return true;
		}
		if (unreadable.Contains(tex))
		{
			return false;
		}
		try
		{
			int width = tex.width;
			int height = tex.height;
			if (width <= 0 || height <= 0)
			{
				unreadable.Add(tex);
				return false;
			}
			Color32[] pixels = tex.GetPixels32();
			int num = width;
			int num2 = height;
			int num3 = -1;
			int num4 = -1;
			for (int i = 0; i < height; i++)
			{
				int num5 = i * width;
				for (int j = 0; j < width; j++)
				{
					if (pixels[num5 + j].a > 12)
					{
						if (j < num)
						{
							num = j;
						}
						if (j > num3)
						{
							num3 = j;
						}
						if (i < num2)
						{
							num2 = i;
						}
						if (i > num4)
						{
							num4 = i;
						}
					}
				}
			}
			if (num3 < 0)
			{
				unreadable.Add(tex);
				return false;
			}
			ink = new Rect((float)num / (float)width, (float)num2 / (float)height, (float)(num3 - num + 1) / (float)width, (float)(num4 - num2 + 1) / (float)height);
			inkMemo[tex] = ink;
			return true;
		}
		catch (Exception)
		{
			if (TryInkViaBlit(tex, out ink))
			{
				inkMemo[tex] = ink;
				return true;
			}
			unreadable.Add(tex);
			ink = default(Rect);
			return false;
		}
	}

	private static bool TryInkViaBlit(Texture2D tex, out Rect ink)
	{
		ink = default(Rect);
		if (tex.width > 512 || tex.height > 512)
		{
			return false;
		}
		RenderTexture renderTexture = null;
		RenderTexture active = RenderTexture.active;
		Texture2D texture2D = null;
		try
		{
			renderTexture = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			Graphics.Blit(tex, renderTexture);
			RenderTexture.active = renderTexture;
			texture2D = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, mipChain: false);
			texture2D.ReadPixels(new Rect(0f, 0f, tex.width, tex.height), 0, 0);
			texture2D.Apply(updateMipmaps: false);
			Color32[] pixels = texture2D.GetPixels32();
			int width = tex.width;
			int height = tex.height;
			int num = width;
			int num2 = height;
			int num3 = -1;
			int num4 = -1;
			for (int i = 0; i < height; i++)
			{
				int num5 = i * width;
				for (int j = 0; j < width; j++)
				{
					if (pixels[num5 + j].a > 12)
					{
						if (j < num)
						{
							num = j;
						}
						if (j > num3)
						{
							num3 = j;
						}
						if (i < num2)
						{
							num2 = i;
						}
						if (i > num4)
						{
							num4 = i;
						}
					}
				}
			}
			if (num3 < 0)
			{
				return false;
			}
			ink = new Rect((float)num / (float)width, (float)num2 / (float)height, (float)(num3 - num + 1) / (float)width, (float)(num4 - num2 + 1) / (float)height);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
		finally
		{
			RenderTexture.active = active;
			if (renderTexture != null)
			{
				RenderTexture.ReleaseTemporary(renderTexture);
			}
			if (texture2D != null)
			{
				UnityEngine.Object.Destroy(texture2D);
			}
		}
	}
}
