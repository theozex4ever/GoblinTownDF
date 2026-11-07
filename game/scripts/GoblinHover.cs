using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>Finds the goblin under the cursor for the Inspector. Reads sim state; never writes it.</summary>
public static class GoblinHover
{
	/// <summary>The lowest-numbered goblin standing on the tile, or -1 when none does.</summary>
	public static int GoblinAt(SimState s, int tile)
	{
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]) == tile)
				return g;
		return -1;
	}
}
