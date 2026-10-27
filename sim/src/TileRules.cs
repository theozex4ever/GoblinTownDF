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

	/// <summary>
	/// Whether a goblin on <paramref name="stand"/> can dig <paramref name="tile"/>: from a walkable
	/// orthogonal neighbour on its level, or, for Dig stairs only, from Stairs directly above or below it.
	/// </summary>
	public static bool CanDigFrom(SimState s, int stand, int tile, DesignationKind designation)
	{
		var layer = s.Width * s.Height;
		var dx = Math.Abs(stand % s.Width - tile % s.Width);
		var dy = Math.Abs(stand / s.Width % s.Height - tile / s.Width % s.Height);
		var dz = Math.Abs(stand / layer - tile / layer);
		var kind = s.KindAt(stand);
		return dz == 0
			? dx + dy == 1 && IsWalkable(kind)
			: designation == DesignationKind.DigStairs && dz == 1 && dx == 0 && dy == 0 && kind == TileKind.Stairs;
	}
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
