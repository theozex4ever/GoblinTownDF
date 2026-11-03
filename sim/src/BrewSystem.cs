namespace Goblins.Sim;

/// <summary>
/// What a Brewery does. A Brew Job is offered on a tile holding Fungus, a Pile tile or loose, to a goblin who can reach it, a free Brewery tile and
/// a Pile with room for the Grog: the goblin Gathers to the Fungus, Takes one unit (5 ticks), Brings it to the nearest free Brewery tile and Brews it
/// for <see cref="ProductionRules.BrewTicks"/> working ticks into one Grog, which lands loose on that tile to be hauled like anything a craft
/// station makes. A Brewery tile has one brewer at a time, and a Fungus unit one claimant, a brewer or a hauler.
/// The goblin's side of a Brew (the four Tasks) lives in <see cref="GoblinTasks"/> and <see cref="JobSystem"/>.
/// </summary>
public static class BrewSystem
{
	/// <summary>
	/// True for a Brewery tile whose Brews wait: no Pile its goblins can reach has room for one more Grog. The view greys it out, and
	/// it brews again once room appears (a Pile painted, Grog drunk or hauled off, the tile made reachable).
	/// </summary>
	public static bool IsWaiting(SimState s, int tile) =>
		s.RoomAt(tile) == RoomKind.Brewery && !s.HasStorageRoom(tile, Material.Grog);
}
