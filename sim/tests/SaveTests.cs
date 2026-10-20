using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

public class SaveTests
{
	[Fact]
	public void A_fresh_world_round_trips_through_json()
	{
		var sim = Simulation.NewWorld(seed: 42);

		var json = SimJson.Serialize(sim);
		var restored = SimJson.Deserialize(json);

		AssertIdentical(sim, restored);
	}

	[Fact]
	public void A_world_after_commands_and_ticks_round_trips_through_json()
	{
		var sim = Simulation.NewWorld(seed: 42);
		sim.Apply(new SetSpeed(4));
		for (var frame = 0; frame < 4; frame++)
			sim.Advance(0.25);
		sim.Apply(new SetPaused(true));

		var restored = SimJson.Deserialize(SimJson.Serialize(sim));

		AssertIdentical(sim, restored);
		Assert.Equal(40, restored.State.Tick);
		Assert.Equal(4, restored.State.Speed);
		Assert.True(restored.State.Paused);
	}

	[Fact]
	public void A_restored_world_keeps_drawing_the_same_random_numbers()
	{
		var sim = Simulation.NewWorld(seed: 42);
		sim.State.Rng.NextInt(100);
		var restored = SimJson.Deserialize(SimJson.Serialize(sim));

		for (var i = 0; i < 10; i++)
			Assert.Equal(sim.State.Rng.NextInt(1000), restored.State.Rng.NextInt(1000));
	}

	static void AssertIdentical(Simulation expected, Simulation actual)
	{
		var e = expected.State;
		var a = actual.State;
		Assert.Equal(e.Seed, a.Seed);
		Assert.Equal(e.Tick, a.Tick);
		Assert.Equal(e.Speed, a.Speed);
		Assert.Equal(e.Paused, a.Paused);
		Assert.Equal(e.Rng.State, a.Rng.State);
		Assert.Equal(e.Width, a.Width);
		Assert.Equal(e.Height, a.Height);
		Assert.Equal(e.Depth, a.Depth);
		Assert.Equal(e.TileKinds, a.TileKinds);
		Assert.Equal(e.TileMaterials, a.TileMaterials);
		Assert.Equal(e.GoblinCount, a.GoblinCount);
		Assert.Equal(e.GoblinX, a.GoblinX);
		Assert.Equal(e.GoblinY, a.GoblinY);
		Assert.Equal(e.GoblinZ, a.GoblinZ);
		Assert.Equal(e.GoblinRole, a.GoblinRole);
		Assert.Equal(SimJson.Serialize(expected), SimJson.Serialize(actual));
	}
}
