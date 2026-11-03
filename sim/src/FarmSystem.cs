namespace Goblins.Sim;

/// <summary>
/// What a farm does. A Tend Job is offered on a Grub Pit or Fungus Farm tile that is not producing and holds no loose unit of its
/// crop; when a goblin finishes it the tile spawns <see cref="YieldPerTend"/> units, one per <see cref="SpawnTicks"/> ticks from the
/// end of the Tend: three Grub, 200 ticks apart, from a pit, and one Fungus after 300 ticks from a farm. The unit lands loose on the
/// tile, so it is a Haul like any loose unit. Grub there can be eaten by a Craving; Fungus is not eaten.
/// The goblin's side of a Tend (Approaching, Tending) lives in <see cref="GoblinTasks"/> and <see cref="JobSystem"/>.
/// </summary>
public static class FarmSystem
{
	/// <summary>True for a Room that is Tended.</summary>
	public static bool IsFarm(RoomKind room) => room is RoomKind.GrubPit or RoomKind.FungusFarm;

	/// <summary>The Material a farm grows, or None for a Room that is no farm.</summary>
	public static Material CropOf(RoomKind room) => room switch
	{
		RoomKind.GrubPit => Material.Grub,
		RoomKind.FungusFarm => Material.Fungus,
		_ => Material.None,
	};

	/// <summary>Units one Tend of a farm tile yields, 0 for a Room that is no farm.</summary>
	public static int YieldPerTend(RoomKind room) => room switch
	{
		RoomKind.GrubPit => ProductionRules.GrubPerTend,
		RoomKind.FungusFarm => ProductionRules.FungusPerTend,
		_ => 0,
	};

	/// <summary>Ticks from the end of a Tend to a farm's first unit, and between one unit and the next. 0 for a Room that is no farm.</summary>
	public static int SpawnTicks(RoomKind room) => room switch
	{
		RoomKind.GrubPit => ProductionRules.GrubSpawnTicks,
		RoomKind.FungusFarm => ProductionRules.FungusGrowTicks,
		_ => 0,
	};

	/// <summary>True for a farm tile that is not producing: a goblin may be working it, whatever lies on it.</summary>
	public static bool IsTendTarget(SimState s, int tile) =>
		IsFarm((RoomKind)s.Rooms[tile]) && s.FarmUnspawned[tile] == 0;

	/// <summary>True when a Tend Job exists on the tile: a farm tile that is not producing and holds no unhauled unit of its crop.</summary>
	public static bool IsTendOffered(SimState s, int tile) =>
		IsTendTarget(s, tile) && s.LooseCount(tile, CropOf((RoomKind)s.Rooms[tile])) == 0;

	/// <summary>Starts a finished Tend's yield: the tile spawns its first unit after a full <see cref="SpawnTicks"/>.</summary>
	public static void StartSpawning(SimState s, int tile)
	{
		s.FarmUnspawned[tile] = (byte)YieldPerTend((RoomKind)s.Rooms[tile]);
		s.FarmTicks[tile] = 0;
	}

	/// <summary>
	/// Runs once per tick: every producing tile counts a tick and, on the <see cref="SpawnTicks"/>th, drops one unit of its crop
	/// loose. Only the spawned tile's haul flag is touched, so the claim loop notices the new unit without a rebuild.
	/// </summary>
	public static void Grow(SimState s)
	{
		for (var tile = 0; tile < s.FarmUnspawned.Length; tile++)
		{
			if (s.FarmUnspawned[tile] == 0)
				continue;
			var room = (RoomKind)s.Rooms[tile];
			if (++s.FarmTicks[tile] < SpawnTicks(room))
				continue;

			s.FarmTicks[tile] = 0;
			s.FarmUnspawned[tile]--;
			s.AddItems(tile, CropOf(room), 1);
			// Reachability is derived: a tile some goblin can path to is a Haul as soon as it holds a loose unit.
			if (s.HaulRegion[tile] != 0)
				s.HaulReachable[tile] = 1;
		}
	}
}
