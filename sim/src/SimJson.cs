using System.Text.Json;

namespace Goblins.Sim;

/// <summary>Saves: the whole sim state as JSON. A save plus the command log replays a run (ADR 0003).</summary>
public static class SimJson
{
	public static string Serialize(Simulation sim) => JsonSerializer.Serialize(sim.State);

	/// <summary>Throws <see cref="JsonException"/> for a save no sim could have written.</summary>
	public static Simulation Deserialize(string json)
	{
		var state = JsonSerializer.Deserialize<SimState>(json)
			?? throw new JsonException("Sim state was null.");
		Validate(state);
		return new Simulation(state);
	}

	static void Validate(SimState s)
	{
		Require(s.Tick >= 0, "Tick is negative.");
		Require(Simulation.IsValidSpeed(s.Speed), $"Speed {s.Speed} is not 1, 2 or 4.");
		Require(s.TickDebt >= 0 && s.TickDebt < 1, $"TickDebt {s.TickDebt} is outside [0, 1).");
		Require(s.Rng is not null, "Rng is missing.");

		Require(s.Width > 0 && s.Height > 0 && s.Depth > 0, "Map size must be positive.");
		var tiles = (long)s.Width * s.Height * s.Depth;
		Require(s.TileKinds is not null && s.TileKinds.LongLength == tiles, "TileKinds does not match the map size.");
		Require(s.TileMaterials is not null && s.TileMaterials.LongLength == tiles, "TileMaterials does not match the map size.");
		Require(s.Designations is not null && s.Designations.LongLength == tiles, "Designations does not match the map size.");
		Require(s.DigProgress is not null && s.DigProgress.LongLength == tiles, "DigProgress does not match the map size.");
		Require(s.TileItems is not null && s.TileItems.LongLength == tiles * SimState.CountedMaterials, "TileItems does not match the map size.");
		Require(s.Piles is not null && s.Piles.LongLength == tiles, "Piles does not match the map size.");
		for (var tile = 0; tile < s.Piles!.Length; tile++)
		{
			Require(s.Piles[tile] <= 1, $"Tile {tile} has Pile flag {s.Piles[tile]}.");
			Require(s.Piles[tile] == 0 || s.KindAt(tile) == TileKind.Floor, $"Tile {tile} is a Pile but not Floor.");
		}
		Require(s.Rooms is not null && s.Rooms.LongLength == tiles, "Rooms does not match the map size.");
		Require(s.FarmUnspawned is not null && s.FarmUnspawned.LongLength == tiles, "FarmUnspawned does not match the map size.");
		Require(s.FarmTicks is not null && s.FarmTicks.LongLength == tiles, "FarmTicks does not match the map size.");
		// Rooms holds every Room but the Pile, which Piles keeps; a tile holds one Room.
		for (var tile = 0; tile < s.Rooms!.Length; tile++)
		{
			var room = (RoomKind)s.Rooms[tile];
			Require(room is RoomKind.None or RoomKind.GrubPit or RoomKind.SleepingHeap or RoomKind.FungusFarm or RoomKind.Brewery, $"Tile {tile} has unknown Room {s.Rooms[tile]}.");
			Require(room != RoomKind.FungusFarm || tile / (s.Width * s.Height) >= ProductionRules.FungusFarmMinZ, $"Tile {tile} is a {room} on the Surface.");
			Require(room == RoomKind.None || s.KindAt(tile) == TileKind.Floor, $"Tile {tile} is a {room} but not Floor.");
			Require(room == RoomKind.None || s.Piles[tile] == 0, $"Tile {tile} is both a Pile and a {room}.");
			Require(s.FarmUnspawned![tile] <= FarmSystem.YieldPerTend(room), $"Tile {tile} has {s.FarmUnspawned[tile]} units to spawn, past the {FarmSystem.YieldPerTend(room)} a {room} yields.");
			Require(s.FarmTicks![tile] == 0 || s.FarmTicks[tile] < FarmSystem.SpawnTicks(room), $"Tile {tile} has spawn ticks {s.FarmTicks[tile]}, past the {FarmSystem.SpawnTicks(room)} of a {room}.");
			Require(s.FarmTicks[tile] == 0 || s.FarmUnspawned[tile] > 0, $"Tile {tile} counts spawn ticks with nothing to spawn.");
		}
		foreach (var count in s.TileItems!)
			Require(count >= 0, $"Item count {count} is negative.");
		// No save is migrated: a Pile tile over capacity is rejected, while loose items are uncapped.
		for (var tile = 0; tile < s.Piles.Length; tile++)
			Require(s.Piles[tile] == 0 || !PileRules.IsOverCapacity(s, tile), $"Pile tile {tile} holds more than {SimState.PileCapacity} units of a material.");
		foreach (var kind in s.TileKinds!)
			Require(Enum.IsDefined((TileKind)kind), $"Unknown tile kind {kind}.");
		foreach (var material in s.TileMaterials!)
			Require(Enum.IsDefined((Material)material), $"Unknown material {material}.");

		for (var tile = 0; tile < s.Designations!.Length; tile++)
		{
			var designation = s.Designations[tile];
			Require(Enum.IsDefined((DesignationKind)designation), $"Unknown designation {designation}.");
			if (designation != (byte)DesignationKind.None)
			{
				var digTicks = MaterialRules.DigTicks(s.MaterialAt(tile));
				Require(TileRules.CanDesignate((DesignationKind)designation, s.KindAt(tile)) && digTicks > 0,
					$"Tile {tile} is designated but cannot be dug.");
				Require(s.DigProgress![tile] < digTicks, $"Tile {tile} has dig progress {s.DigProgress![tile]}, past its dig time.");
			}
			else
				Require(s.DigProgress![tile] == 0, $"Tile {tile} has dig progress but no designation.");
		}

		Require(s.GoblinCount >= 0 && s.GoblinCount <= SimState.MaxGoblins, $"GoblinCount {s.GoblinCount} is outside 0..{SimState.MaxGoblins}.");
		Require(s.GoblinX is { Length: SimState.MaxGoblins }
			&& s.GoblinY is { Length: SimState.MaxGoblins }
			&& s.GoblinZ is { Length: SimState.MaxGoblins }
			&& s.GoblinRole is { Length: SimState.MaxGoblins }
			&& s.GoblinTask is { Length: SimState.MaxGoblins }
			&& s.GoblinJobTile is { Length: SimState.MaxGoblins }
			&& s.GoblinStepTicks is { Length: SimState.MaxGoblins }
			&& s.GoblinWanderTile is { Length: SimState.MaxGoblins }
			&& s.GoblinWanderTicks is { Length: SimState.MaxGoblins }
			&& s.GoblinCarry is { Length: SimState.MaxGoblins }
			&& s.GoblinWorkTicks is { Length: SimState.MaxGoblins }
			&& s.GoblinGrub is { Length: SimState.MaxGoblins }
			&& s.GoblinGrog is { Length: SimState.MaxGoblins }
			&& s.GoblinSleep is { Length: SimState.MaxGoblins }
			&& s.GoblinChaos is { Length: SimState.MaxGoblins }
			&& s.GoblinActCooldown is { Length: SimState.MaxGoblins },
			$"Goblin arrays must hold {SimState.MaxGoblins} entries.");
		for (var g = 0; g < s.GoblinCount; g++)
		{
			Require(s.GoblinX[g] >= 0 && s.GoblinX[g] < s.Width
				&& s.GoblinY[g] >= 0 && s.GoblinY[g] < s.Height
				&& s.GoblinZ[g] >= 0 && s.GoblinZ[g] < s.Depth,
				$"Goblin {g} is off the map.");
			Require(Enum.IsDefined((Role)s.GoblinRole[g]), $"Goblin {g} has unknown role {s.GoblinRole[g]}.");
			Require(s.GoblinGrub[g] <= NeedRules.MeterMax, $"Goblin {g} has Grub meter {s.GoblinGrub[g]}, past {NeedRules.MeterMax}.");
			Require(s.GoblinGrog[g] <= NeedRules.MeterMax, $"Goblin {g} has Grog meter {s.GoblinGrog[g]}, past {NeedRules.MeterMax}.");
			Require(s.GoblinSleep[g] <= NeedRules.MeterMax, $"Goblin {g} has Sleep meter {s.GoblinSleep[g]}, past {NeedRules.MeterMax}.");
			Require(s.GoblinChaos[g] <= ChaosRules.Max, $"Goblin {g} has Chaos {s.GoblinChaos[g]}, past {ChaosRules.Max}.");
			Require(s.GoblinActCooldown[g] <= ChaosRules.CooldownTicks, $"Goblin {g} has a cooldown of {s.GoblinActCooldown[g]}, past {ChaosRules.CooldownTicks}.");
		}

		// The structure is sound, so the Task rules can read it.
		if (GoblinTasks.FindViolation(s) is { } violation)
			Require(false, violation);
	}

	static void Require(bool condition, string message)
	{
		if (!condition)
			throw new JsonException($"Invalid save: {message}");
	}
}
