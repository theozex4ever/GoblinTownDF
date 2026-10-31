namespace AThousandGoblinsDeep;

/// <summary>
/// The text a Pile tile shows for one material's stored count. Piles have no cap, so a big count is
/// abbreviated to at most three characters to stay inside its cell.
/// </summary>
public static class CountLabel
{
	/// <summary>1 to 999 as written, 1000 to 99999 as thousands rounded down ("1k" to "99k"), and 100000 or more capped at "99k" (meaning 99k or more). Zero or less is empty.</summary>
	public static string Format(int count) => count switch
	{
		<= 0 => "",
		< 1000 => count.ToString(),
		< 100_000 => $"{count / 1000}k",
		_ => "99k",
	};
}
