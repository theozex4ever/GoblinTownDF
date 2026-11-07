using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>
/// What a Pile tile says about its stock. A tile is 18 px, so it draws only <see cref="Shown"/> counts; the Inspector
/// is where every count is read. Reads sim state; never writes it.
/// </summary>
public static class PileCounts
{
	/// <summary>How many counts a Pile tile draws.</summary>
	public const int Shown = 2;

	/// <summary>
	/// The materials a Pile tile draws, best first. A full material (at the Pile's capacity) ranks ahead of any that is not,
	/// then the larger count ranks first, and equal counts keep Material order. Materials with nothing stored are never
	/// drawn, so a slot with none left is <see cref="Material.None"/>.
	/// </summary>
	public static void Pick(SimState s, int tile, out Material first, out Material second)
	{
		first = second = Material.None;
		var firstCount = 0;
		var secondCount = 0;
		for (var material = Material.Dirt; (int)material <= SimState.CountedMaterials; material++)
		{
			var count = s.StoredCount(tile, material);
			if (count <= 0)
				continue;
			if (Outranks(count, firstCount))
			{
				(second, secondCount) = (first, firstCount);
				(first, firstCount) = (material, count);
			}
			else if (Outranks(count, secondCount))
				(second, secondCount) = (material, count);
		}
	}

	// Strictly better only, so a tie keeps the material met first. A count of 0 is outranked by every real count.
	static bool Outranks(int count, int other)
	{
		var full = count >= SimState.PileCapacity;
		var otherFull = other >= SimState.PileCapacity;
		return full != otherFull ? full : count > other;
	}
}
