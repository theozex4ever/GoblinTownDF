namespace AThousandGoblinsDeep;

/// <summary>
/// The seven starting goblins' authored names, by goblin index. Presentation only: no sim state, no RNG draw, no save field, so a
/// goblin keeps its name across refreshes, movement and seeds.
/// </summary>
public static class GoblinNames
{
	public static readonly string[] Starting =
	[
		"Snaggle Wartnose",
		"Mother Grubb",
		"Third-Best Ugg",
		"Kevin",
		"Nubbin Splatt",
		"Gristle Mudfoot",
		"Old Bogwash",
	];

	/// <summary>The goblin's name; a goblin past the seven authored ones gets a plain number until later work names it.</summary>
	public static string For(int goblin) => goblin >= 0 && goblin < Starting.Length ? Starting[goblin] : $"Goblin {goblin + 1}";
}
