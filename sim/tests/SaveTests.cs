using System.Text.Json;
using System.Text.Json.Nodes;
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

	[Fact]
	public void A_save_taken_between_ticks_keeps_the_same_tick_timing()
	{
		var sim = Simulation.NewWorld(seed: 42);
		sim.Advance(0.05);
		var restored = SimJson.Deserialize(SimJson.Serialize(sim));

		Assert.Equal(sim.Advance(0.05), restored.Advance(0.05));
		Assert.Equal(1, restored.State.Tick);
	}

	[Theory]
	[InlineData("Speed", "1000")]
	[InlineData("Speed", "-1")]
	[InlineData("Speed", "0")]
	[InlineData("GoblinCount", "31")]
	[InlineData("GoblinCount", "-1")]
	[InlineData("Width", "33")]
	[InlineData("Depth", "0")]
	[InlineData("TickDebt", "-5")]
	[InlineData("TickDebt", "1e9")]
	[InlineData("Rng", "null")]
	[InlineData("TileKinds", "null")]
	public void A_save_with_impossible_state_is_rejected(string property, string value)
	{
		var json = Corrupt(SimJson.Serialize(Simulation.NewWorld(seed: 42)), property, value);

		Assert.Throws<JsonException>(() => SimJson.Deserialize(json));
	}

	[Fact]
	public void A_goblin_off_the_map_is_rejected()
	{
		var json = SimJson.Serialize(Simulation.NewWorld(seed: 42));
		var node = JsonNode.Parse(json)!;
		node["GoblinZ"]![0] = 3;

		Assert.Throws<JsonException>(() => SimJson.Deserialize(node.ToJsonString()));
	}

	[Fact]
	public void A_tile_with_an_unknown_kind_is_rejected()
	{
		var sim = Simulation.NewWorld(seed: 42);
		sim.State.TileKinds[0] = 200;

		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	static string Corrupt(string json, string property, string value)
	{
		var node = JsonNode.Parse(json)!;
		node[property] = JsonNode.Parse(value);
		return node.ToJsonString();
	}

	static void AssertIdentical(Simulation expected, Simulation actual)
	{
		var e = expected.State;
		var a = actual.State;
		Assert.Equal(e.Seed, a.Seed);
		Assert.Equal(e.Tick, a.Tick);
		Assert.Equal(e.Speed, a.Speed);
		Assert.Equal(e.Paused, a.Paused);
		Assert.Equal(e.TickDebt, a.TickDebt);
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
