namespace Goblins.Sim;

/// <summary>
/// What a Grub Pit does. A Tend Job is offered on a Grub Pit tile that is not spawning and holds no loose Grub; when a goblin
/// finishes it the tile spawns <see cref="ProductionRules.GrubPerTend"/> Grub, one per <see cref="ProductionRules.GrubSpawnTicks"/>
/// ticks. The Grub lands loose on the tile, so it is a Haul like any loose unit and a Craving can eat it where it lies.
/// The goblin's side of a Tend (Approaching, Tending) lives in <see cref="GoblinTasks"/> and <see cref="JobSystem"/>.
/// </summary>
public static class FarmSystem
{
	/// <summary>True for a Grub Pit tile that is not spawning: a goblin may be working it, whatever lies on it.</summary>
	public static bool IsTendTarget(SimState s, int tile) =>
		s.Rooms[tile] == (byte)RoomKind.GrubPit && s.PitUnspawned[tile] == 0;

	/// <summary>True when a Tend Job exists on the tile: a Grub Pit tile that is not spawning and holds no unhauled Grub.</summary>
	public static bool IsTendOffered(SimState s, int tile) =>
		IsTendTarget(s, tile) && s.LooseCount(tile, Material.Grub) == 0;

	/// <summary>Starts a finished Tend's yield: the tile spawns its first Grub after a full <see cref="ProductionRules.GrubSpawnTicks"/>.</summary>
	public static void StartSpawning(SimState s, int tile)
	{
		s.PitUnspawned[tile] = ProductionRules.GrubPerTend;
		s.PitSpawnTicks[tile] = 0;
	}

	/// <summary>
	/// Runs once per tick: every spawning tile counts a tick and, on the <see cref="ProductionRules.GrubSpawnTicks"/>th, drops one Grub
	/// loose. Only the spawned tile's haul flag is touched, so the claim loop notices the new unit without a rebuild.
	/// </summary>
	public static void Grow(SimState s)
	{
		for (var tile = 0; tile < s.PitUnspawned.Length; tile++)
		{
			if (s.PitUnspawned[tile] == 0 || ++s.PitSpawnTicks[tile] < ProductionRules.GrubSpawnTicks)
				continue;

			s.PitSpawnTicks[tile] = 0;
			s.PitUnspawned[tile]--;
			s.AddItems(tile, Material.Grub, 1);
			// Reachability is derived: a tile some goblin can path to is a Haul as soon as it holds a loose unit.
			if (s.HaulRegion[tile] != 0)
				s.HaulReachable[tile] = 1;
		}
	}
}
