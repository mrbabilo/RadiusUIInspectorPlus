using RadiusUI.Framework;
using Verse;

namespace RadiusUIInspector;

public sealed class MultiEntry : ISnapshot
{
	public Thing? Thing;

	public Pawn? Pawn;

	public string Name = "";

	public string Initial = "?";

	public string MoodStr = "";

	public bool HasMood;

	public int Untended;

	public string UntendedStr = "";

	public int NextRefresh { get; set; }
}
