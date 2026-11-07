namespace AThousandGoblinsDeep;

/// <summary>The four incidents that earn a chat bubble.</summary>
public enum BubbleKind
{
	Shirk,
	Scuffle,
	Theft,
	LostHaul,
}

/// <summary>
/// Everything the chat bubbles say, in one place so it is easy to revise (or to swap for icons) without touching the Chaos rules: each kind's
/// symbol label, which the colour key shows, and its prewritten lines, in the goblin voice. A line may hold <c>{victim}</c> or <c>{supply}</c>.
/// Which line a bubble gets is a plain count, never a random draw, so copy has no effect on the sim's randomness.
/// </summary>
public static class BubbleCopy
{
	/// <summary>The caption for a kind's symbol.</summary>
	public static string Label(BubbleKind kind) => kind switch
	{
		BubbleKind.Shirk => "Knocking off early",
		BubbleKind.Scuffle => "A scuffle, started",
		BubbleKind.Theft => "Helping itself",
		_ => "A Haul, eaten",
	};

	static readonly string[] ShirkLines =
	[
		"Not shirking. Resting. Fiercely.",
		"The dig can wait. I cannot.",
		"I am on a very important sit.",
		"Work? Never heard of it.",
	];

	static readonly string[] ScuffleLines =
	[
		"Right, {victim}. Here is fine.",
		"{victim} knows what they did.",
		"Oi! {victim}! Yes, you!",
		"This is for the Grub, {victim}.",
	];

	static readonly string[] TheftLines =
	[
		"Just borrowing this {supply}. Forever.",
		"Finders keepers. Pile-ers weepers.",
		"This {supply} looked lonely.",
		"Nobody saw. Nobody ever does.",
	];

	static readonly string[] LostHaulLines =
	[
		"Someone ate my Haul. My Haul!",
		"I walked all this way for that?",
		"That was MY errand. Eaten.",
		"My Haul had a future.",
	];

	static string[] Lines(BubbleKind kind) => kind switch
	{
		BubbleKind.Shirk => ShirkLines,
		BubbleKind.Scuffle => ScuffleLines,
		BubbleKind.Theft => TheftLines,
		_ => LostHaulLines,
	};

	/// <summary>How many lines a kind has; <see cref="Line"/> cycles through them.</summary>
	public static int LineCount(BubbleKind kind) => Lines(kind).Length;

	/// <summary>The line for the <paramref name="sequence"/>th bubble of this kind (counted from 0, wrapping), with its blanks filled.</summary>
	public static string Line(BubbleKind kind, int sequence, string victim, string supply)
	{
		var lines = Lines(kind);
		return lines[((sequence % lines.Length) + lines.Length) % lines.Length].Replace("{victim}", victim).Replace("{supply}", supply);
	}
}
