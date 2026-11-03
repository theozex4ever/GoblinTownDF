namespace Goblins.Sim;

/// <summary>
/// Painting and removing Rooms. Every Room follows the Pile's rules: Floor tiles only, one Room per tile (painting over a
/// different Room skips the tile), a typed removal that takes only that Room, and Dig stairs clearing whatever Room it digs.
/// </summary>
public static class RoomRules
{
	public static bool CanPaint(TileKind tile) => tile == TileKind.Floor;

	/// <summary>
	/// Paints <paramref name="room"/> on every Floor tile in the rectangle that holds no Room yet; a tile that holds a different
	/// Room is skipped. A Pile also skips a tile holding more than <see cref="SimState.PileCapacity"/> of any material, which keeps
	/// its items loose; units already on a painted Pile tile become stored.
	/// </summary>
	public static void Paint(SimState s, RoomKind room, int z, int cx0, int cy0, int cx1, int cy1)
	{
		RequireRoom(room);
		if (!JobSystem.TryClip(s, z, cx0, cy0, cx1, cy1, out var x0, out var y0, out var x1, out var y1))
			return;
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
		{
			var tile = s.TileIndex(x, y, z);
			if (CanPaint(s.KindAt(tile)) && s.RoomAt(tile) == RoomKind.None && Accepts(s, room, tile))
				s.SetRoom(tile, room);
		}
		Settle(s);
	}

	/// <summary>Clears <paramref name="room"/> from every tile in the rectangle that holds it. A tile holding another Room is left alone, and so is its stock, now loose.</summary>
	public static void Remove(SimState s, RoomKind room, int z, int cx0, int cy0, int cx1, int cy1)
	{
		RequireRoom(room);
		if (!JobSystem.TryClip(s, z, cx0, cy0, cx1, cy1, out var x0, out var y0, out var x1, out var y1))
			return;
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
		{
			var tile = s.TileIndex(x, y, z);
			if (s.RoomAt(tile) == room)
				Clear(s, tile);
		}
		Settle(s);
	}

	/// <summary>Clears whatever Room a tile holds, for Dig stairs. The caller runs <see cref="Settle"/> afterwards.</summary>
	public static void Clear(SimState s, int tile) => s.SetRoom(tile, RoomKind.None);

	/// <summary>
	/// Brings the goblins' hauls and meals in line after a Pile appeared or went, or a unit of Grub or Grog was consumed or picked
	/// up: a goblin fetching a unit that is now stored is freed, a goblin dropping a unit on a tile that is no longer a Pile carries
	/// it on to the next nearest Pile, a goblin eating or drinking a unit that is gone is freed to look for another, and a goblin
	/// tending a tile that is no longer a Grub Pit free to tend is freed.
	/// Run after anything that clears or sets a Room (painting, removing, digging stairs on one) or takes a unit of Grub
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
				case GoblinTask.Approaching:
				case GoblinTask.Tending:
					// A Grub Pit tile cleared or repainted under a tender takes its Job with it, and the tender's progress.
					if (!FarmSystem.IsTendTarget(s, s.GoblinJobTile[g]))
						GoblinTasks.Release(s, g);
					break;
				case GoblinTask.Eating:
				case GoblinTask.Drinking:
					if (s.ItemCount(s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]), GoblinTasks.SupplyOf((GoblinTask)s.GoblinTask[g])) == 0)
						GoblinTasks.Release(s, g);
					break;
			}
		}
	}

	// Whether a tile can take the Room beyond being Floor with no other Room on it.
	static bool Accepts(SimState s, RoomKind room, int tile) => room != RoomKind.Pile || !PileRules.IsOverCapacity(s, tile);

	static void RequireRoom(RoomKind room)
	{
		if (!Enum.IsDefined(room) || room == RoomKind.None)
			throw new ArgumentOutOfRangeException(nameof(room), room, "A Room is a real kind, not None.");
	}
}
