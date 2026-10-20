using System.Text.Json;

namespace Goblins.Sim;

/// <summary>Saves: the whole sim state as JSON. A save plus the command log replays a run (ADR 0003).</summary>
public static class SimJson
{
	public static string Serialize(Simulation sim) => JsonSerializer.Serialize(sim.State);

	public static Simulation Deserialize(string json)
	{
		var state = JsonSerializer.Deserialize<SimState>(json)
			?? throw new JsonException("Sim state was null.");
		return new Simulation(state);
	}
}
