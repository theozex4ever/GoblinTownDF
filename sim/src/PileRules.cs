namespace Goblins.Sim;

/// <summary>Painting and removing Piles. A Pile is a flag on Floor tiles; the flag is what stores a tile's units.</summary>
public static class PileRules
{
	public static bool CanPaint(TileKind tile) => tile == TileKind.Floor;

	/// <summary>Flags every Floor tile in the rectangle. Units already on those tiles become stored.</summary>
	public static void Paint(SimState s, PaintPile command)
	{
		if (!JobSystem.TryClip(s, command.Z, command.X0, command.Y0, command.X1, command.Y1, out var x0, out var y0, out var x1, out var y1))
			return;
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
		{
			var tile = s.TileIndex(x, y, command.Z);
			if (CanPaint(s.KindAt(tile)))
				s.Piles[tile] = 1;
		}
	}

	/// <summary>Clears the flag on every Pile tile in the rectangle. Their counts stay, now loose.</summary>
	public static void Remove(SimState s, RemovePile command)
	{
		if (!JobSystem.TryClip(s, command.Z, command.X0, command.Y0, command.X1, command.Y1, out var x0, out var y0, out var x1, out var y1))
			return;
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
			s.Piles[s.TileIndex(x, y, command.Z)] = 0;
	}
}
