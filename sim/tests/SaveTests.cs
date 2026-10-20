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

	[Fact]
	public void Designations_and_goblin_jobs_round_trip_through_json()
	{
		var sim = Simulation.NewWorld(seed: 42);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 14, 10, 16, 11));
		for (var i = 0; i < 25; i++)
			sim.Advance(0.1);

		var restored = SimJson.Deserialize(SimJson.Serialize(sim));

		AssertIdentical(sim, restored);
		Assert.Equal(sim.State.Designations, restored.State.Designations);
		Assert.Equal(sim.State.DigProgress, restored.State.DigProgress);
		Assert.Equal(sim.State.GoblinJobTile, restored.State.GoblinJobTile);
		Assert.Equal(sim.State.GoblinTask, restored.State.GoblinTask);
	}

	[Theory]
	[InlineData("Designations", "null")]
	[InlineData("DigProgress", "null")]
	[InlineData("GoblinTask", "null")]
	[InlineData("GoblinJobTile", "null")]
	[InlineData("GoblinStepTicks", "null")]
	public void A_save_missing_the_dig_state_is_rejected(string property, string value)
	{
		var json = Corrupt(SimJson.Serialize(Simulation.NewWorld(seed: 42)), property, value);

		Assert.Throws<JsonException>(() => SimJson.Deserialize(json));
	}

	[Fact]
	public void A_designation_of_unknown_kind_or_on_a_tile_that_cannot_take_it_is_rejected()
	{
		var unknown = Simulation.NewWorld(seed: 42);
		unknown.State.Designations[unknown.State.TileIndex(1, 1, 0)] = 200;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(unknown)));

		var onStairs = Simulation.NewWorld(seed: 42);
		var tile = onStairs.State.TileIndex(1, 1, 0);
		onStairs.State.TileKinds[tile] = (byte)TileKind.Stairs;
		onStairs.State.Designations[tile] = (byte)DesignationKind.DigStairs;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(onStairs)));
	}

	[Fact]
	public void A_goblin_job_that_points_nowhere_is_rejected()
	{
		var offMap = Simulation.NewWorld(seed: 42);
		offMap.State.GoblinTask[0] = (byte)GoblinTask.Digging;
		offMap.State.GoblinJobTile[0] = 999_999;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(offMap)));

		var undesignated = Simulation.NewWorld(seed: 42);
		undesignated.State.GoblinTask[0] = (byte)GoblinTask.Walking;
		undesignated.State.GoblinJobTile[0] = undesignated.State.TileIndex(1, 1, 0);
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(undesignated)));

		var idleWithJob = Simulation.NewWorld(seed: 42);
		idleWithJob.State.GoblinJobTile[0] = idleWithJob.State.TileIndex(1, 1, 0);
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(idleWithJob)));

		var unknownTask = Simulation.NewWorld(seed: 42);
		unknownTask.State.GoblinTask[0] = 9;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(unknownTask)));
	}

	[Fact]
	public void Dig_progress_that_overruns_the_dig_time_or_has_no_designation_is_rejected()
	{
		var overrun = Simulation.NewWorld(seed: 42);
		var tile = overrun.State.TileIndex(1, 1, 0);
		overrun.State.Designations[tile] = (byte)DesignationKind.DigStairs;
		overrun.State.DigProgress[tile] = 255;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(overrun)));

		var orphan = Simulation.NewWorld(seed: 42);
		orphan.State.DigProgress[tile] = 3;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(orphan)));

		var stepping = Simulation.NewWorld(seed: 42);
		stepping.State.GoblinStepTicks[0] = 200;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(stepping)));
	}

	[Theory]
	[InlineData(16, 16, 0, 0)] // far away
	[InlineData(1, 6, 0, 0)] // diagonal
	[InlineData(0, 5, 0, 0)] // on the tile itself
	[InlineData(31, 4, 0, 0)] // next to it in the tile array, across the row edge
	[InlineData(0, 5, 0, 1)] // on Floor above a tile on z1
	public void A_goblin_digging_from_where_it_cannot_dig_is_rejected(int x, int y, int z, int jobZ)
	{
		var sim = DiggingAt(x, y, z, jobZ);

		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Theory]
	[InlineData(1, 5, 0, 0)]
	[InlineData(0, 4, 0, 0)]
	[InlineData(0, 6, 0, 0)]
	public void A_goblin_digging_from_beside_its_tile_loads(int x, int y, int z, int jobZ)
	{
		var sim = DiggingAt(x, y, z, jobZ);

		AssertIdentical(sim, SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Fact]
	public void A_goblin_digging_from_Stairs_above_its_tile_loads()
	{
		var sim = DiggingAt(0, 5, 0, jobZ: 1);
		sim.State.TileKinds[sim.State.TileIndex(0, 5, 0)] = (byte)TileKind.Stairs;

		AssertIdentical(sim, SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	// Goblin 0 at (x, y, z), digging a designated (0, 5, jobZ).
	static Simulation DiggingAt(int x, int y, int z, int jobZ)
	{
		var sim = Simulation.NewWorld(seed: 42);
		var s = sim.State;
		var job = s.TileIndex(0, 5, jobZ);
		s.Designations[job] = (byte)DesignationKind.DigStairs;
		(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]) = (x, y, z);
		s.GoblinTask[0] = (byte)GoblinTask.Digging;
		s.GoblinJobTile[0] = job;
		return sim;
	}

	[Fact]
	public void Two_goblins_claiming_one_tile_are_rejected()
	{
		var sim = Simulation.NewWorld(seed: 42);
		var s = sim.State;
		var tile = s.TileIndex(1, 1, 0);
		s.Designations[tile] = (byte)DesignationKind.DigStairs;
		s.GoblinCount = 2;
		s.GoblinTask[0] = s.GoblinTask[1] = (byte)GoblinTask.Walking;
		s.GoblinJobTile[0] = s.GoblinJobTile[1] = tile;

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
		Assert.Equal(e.Designations, a.Designations);
		Assert.Equal(e.DigProgress, a.DigProgress);
		Assert.Equal(e.GoblinCount, a.GoblinCount);
		Assert.Equal(e.GoblinX, a.GoblinX);
		Assert.Equal(e.GoblinY, a.GoblinY);
		Assert.Equal(e.GoblinZ, a.GoblinZ);
		Assert.Equal(e.GoblinRole, a.GoblinRole);
		Assert.Equal(e.GoblinTask, a.GoblinTask);
		Assert.Equal(e.GoblinJobTile, a.GoblinJobTile);
		Assert.Equal(e.GoblinStepTicks, a.GoblinStepTicks);
		Assert.Equal(SimJson.Serialize(expected), SimJson.Serialize(actual));
	}
}
