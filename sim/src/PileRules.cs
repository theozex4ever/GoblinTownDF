namespace Goblins.Sim;

/// <summary>What a Pile does to the units on its tiles. A Pile is a Room flagged on Floor tiles (see <see cref="RoomRules"/>); the flag is what stores a tile's units.</summary>
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

	/// <summary>
	/// Brings the goblins' hauls and meals in line after a Pile appeared or went, or a unit of Grub or Grog was consumed or picked
	/// up: a goblin fetching a unit that is now stored is freed, a goblin dropping a unit on a tile that is no longer a Pile carries
	/// it on to the next nearest Pile, and a goblin eating or drinking a unit that is gone is freed to look for another.
	/// Run after anything that clears or sets a Pile flag (painting, removing, digging stairs on a Pile) or takes a unit of Grub
	/// or Grog off a tile, so <see cref="GoblinTasks.FindViolation"/> holds between ticks.
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
				case GoblinTask.Eating:
				case GoblinTask.Drinking:
					if (s.ItemCount(s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]), GoblinTasks.SupplyOf((GoblinTask)s.GoblinTask[g])) == 0)
						GoblinTasks.Release(s, g);
					break;
			}
		}
	}
}
