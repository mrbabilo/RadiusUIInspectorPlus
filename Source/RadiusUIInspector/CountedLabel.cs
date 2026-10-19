using Verse;

namespace RadiusUIInspector;

// A translated label with one or two counts, drawn every pass: a translation with arguments parses the whole string
// each time, so the text is rebuilt only when a count or the language changes.
internal sealed class CountedLabel
{
	private readonly string key;

	private int first = int.MinValue;

	private int second = int.MinValue;

	private LoadedLanguage? language;

	private string text = "";

	internal CountedLabel(string key)
	{
		this.key = key;
	}

	internal string For(int count)
	{
		if (Stale(count, 0))
		{
			text = key.Translate(count);
		}
		return text;
	}

	internal string For(int count, int total)
	{
		if (Stale(count, total))
		{
			text = key.Translate(count, total);
		}
		return text;
	}

	private bool Stale(int a, int b)
	{
		LoadedLanguage activeLanguage = LanguageDatabase.activeLanguage;
		if (a == first && b == second && activeLanguage == language)
		{
			return false;
		}
		first = a;
		second = b;
		language = activeLanguage;
		return true;
	}
}
