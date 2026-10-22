namespace Goblins.Sim;

/// <summary>What each tile kind allows. Stairs also link to Stairs directly above or below them.</summary>
public static class TileRules
{
	public static bool IsWalkable(TileKind kind) => kind is TileKind.Floor or TileKind.Stairs;

	/// <summary>Whether a tile of <paramref name="tile"/> kind can take the designation. Finished work is not designated again.</summary>
	public static bool CanDesignate(DesignationKind designation, TileKind tile) => designation switch
	{
		DesignationKind.Dig => tile == TileKind.Solid,
		DesignationKind.DigStairs => tile is TileKind.Solid or TileKind.Floor,
		_ => false,
	};

	/// <summary>What a tile becomes once the designation is dug out.</summary>
	public static TileKind DugKind(DesignationKind designation) =>
		designation == DesignationKind.Dig ? TileKind.Floor : TileKind.Stairs;
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
