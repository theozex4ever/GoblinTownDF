namespace Goblins.Sim;

/// <summary>The Pile's own rule. A Pile is a Room flagged on Floor tiles (see <see cref="RoomRules"/>); the flag is what stores a tile's units.</summary>
public static class PileRules
{
	/// <summary>True when any material on the tile exceeds a Pile's capacity, so the tile cannot become a Pile.</summary>
	public static bool IsOverCapacity(SimState s, int tile)
	{
		for (var material = Material.Dirt; (int)material <= SimState.CountedMaterials; material++)
			if (s.ItemCount(tile, material) > SimState.PileCapacity)
				return true;
		return false;
	}
}
