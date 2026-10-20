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
		foreach (var kind in s.TileKinds!)
			Require(Enum.IsDefined((TileKind)kind), $"Unknown tile kind {kind}.");
		foreach (var material in s.TileMaterials!)
			Require(Enum.IsDefined((Material)material), $"Unknown material {material}.");

		Require(s.GoblinCount >= 0 && s.GoblinCount <= SimState.MaxGoblins, $"GoblinCount {s.GoblinCount} is outside 0..{SimState.MaxGoblins}.");
		Require(s.GoblinX is { Length: SimState.MaxGoblins }
			&& s.GoblinY is { Length: SimState.MaxGoblins }
			&& s.GoblinZ is { Length: SimState.MaxGoblins }
			&& s.GoblinRole is { Length: SimState.MaxGoblins },
			$"Goblin arrays must hold {SimState.MaxGoblins} entries.");
		for (var g = 0; g < s.GoblinCount; g++)
		{
			Require(s.GoblinX[g] >= 0 && s.GoblinX[g] < s.Width
				&& s.GoblinY[g] >= 0 && s.GoblinY[g] < s.Height
				&& s.GoblinZ[g] >= 0 && s.GoblinZ[g] < s.Depth,
				$"Goblin {g} is off the map.");
			Require(Enum.IsDefined((Role)s.GoblinRole[g]), $"Goblin {g} has unknown role {s.GoblinRole[g]}.");
		}
	}

	static void Require(bool condition, string message)
	{
		if (!condition)
			throw new JsonException($"Invalid save: {message}");
	}
}
