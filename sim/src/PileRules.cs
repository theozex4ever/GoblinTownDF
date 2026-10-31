namespace Goblins.Sim;

/// <summary>Painting and removing Piles. A Pile is a flag on Floor tiles; the flag is what stores a tile's units.</summary>
public static class PileRules
{
	public static bool CanPaint(TileKind tile) => tile == TileKind.Floor;

	/// <summary>
	/// Flags every Floor tile in the rectangle whose stock fits a Pile; units already on those tiles become stored. A tile
	/// holding more than <see cref="SimState.PileCapacity"/> of any material is skipped whole and keeps its items loose.
	/// </summary>
	public static void Paint(SimState s, PaintPile command)
	{
		if (!JobSystem.TryClip(s, command.Z, command.X0, command.Y0, command.X1, command.Y1, out var x0, out var y0, out var x1, out var y1))
			return;
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
		{
			var tile = s.TileIndex(x, y, command.Z);
			if (CanPaint(s.KindAt(tile)) && !IsOverCapacity(s, tile))
				s.Piles[tile] = 1;
		}
		Settle(s);
	}

	/// <summary>True when any material on the tile exceeds a Pile's capacity, so the tile cannot become a Pile.</summary>
	public static bool IsOverCapacity(SimState s, int tile)
	{
		for (var material = Material.Dirt; (int)material <= SimState.CountedMaterials; material++)
			if (s.ItemCount(tile, material) > SimState.PileCapacity)
				return true;
		return false;
	}

	/// <summary>Clears the flag on every Pile tile in the rectangle. Their counts stay, now loose.</summary>
	public static void Remove(SimState s, RemovePile command)
	{
		if (!JobSystem.TryClip(s, command.Z, command.X0, command.Y0, command.X1, command.Y1, out var x0, out var y0, out var x1, out var y1))
			return;
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
			s.Piles[s.TileIndex(x, y, command.Z)] = 0;
		Settle(s);
	}

	/// <summary>
	/// Brings the goblins' hauls in line after a Pile appeared or went: a goblin fetching a unit that is now stored is
	/// freed, and a goblin dropping a unit on a tile that is no longer a Pile carries it on to the next nearest Pile.
	/// Run after anything that clears or sets a Pile flag (painting, removing, digging stairs on a Pile), so <see cref="GoblinTasks.FindViolation"/> holds between ticks.
	/// </summary>
	public static void Settle(SimState s)
	{
		for (var g = 0; g < s.GoblinCount; g++)
		{
			switch ((GoblinTask)s.GoblinTask[g])
			{
				case GoblinTask.Fetching:
				case GoblinTask.PickingUp:
					// A tile claimed by more goblins than it has loose units frees its highest-numbered claimants.
					if (GoblinTasks.FetchClaims(s, s.GoblinJobTile[g], g) >= s.LooseTotal(s.GoblinJobTile[g]))
						GoblinTasks.Release(s, g);
					break;
				case GoblinTask.Dropping:
					if (!s.IsPile(s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g])))
						GoblinTasks.ResumeCarrying(s, g);
					break;
			}
		}
	}
}
