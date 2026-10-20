namespace Goblins.Sim;

/// <summary>What each tile kind allows. Stairs also link to Stairs directly above or below them.</summary>
public static class TileRules
{
	public static bool IsWalkable(TileKind kind) => kind is TileKind.Floor or TileKind.Stairs;

	public static bool CanDigStairs(TileKind kind) => kind is TileKind.Solid or TileKind.Floor;
}

/// <summary>Per-material numbers. Dig logic reads them from here and never hard-codes them.</summary>
public static class MaterialRules
{
	/// <summary>Ticks to dig one tile of the material, or 0 if there is nothing to dig.</summary>
	public static int DigTicks(Material material) => material switch
	{
		Material.Dirt => 20,
		Material.Stone => 40,
		_ => 0,
	};
}
