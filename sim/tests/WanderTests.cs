using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

public class WanderTests
{
	// The goblin's (x, y, z) after each of `ticks` ticks.
	static List<(int X, int Y, int Z)> Path(int seed, int ticks)
	{
		var sim = Simulation.NewWorld(seed);
		var s = sim.State;
		var path = new List<(int, int, int)>();
		for (var i = 0; i < ticks; i++)
		{
			RunTicks(sim, 1);
			path.Add((s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]));
		}
		return path;
	}

	[Fact]
	public void With_no_designations_the_goblin_moves_over_time_and_only_ever_stands_on_walkable_tiles()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var visited = new HashSet<(int, int)>();
		for (var i = 0; i < 1000; i++)
		{
			RunTicks(sim, 1);
			Assert.True(TileRules.IsWalkable(s.KindAt(s.TileIndex(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]))));
			visited.Add((s.GoblinX[0], s.GoblinY[0]));
		}

		Assert.True(visited.Count > 10, $"The goblin only visited {visited.Count} tiles.");
	}

	[Fact]
	public void The_goblin_pauses_30_to_60_ticks_then_takes_one_step_per_3_ticks_with_no_diagonals()
	{
		for (var seed = 0; seed < 20; seed++)
		{
			var path = Path(seed, 200);
			var first = path.FindIndex(p => p != (16, 16, 0)) + 1;

			// The pause is 30–60 ticks, and the first step takes 3 more.
			Assert.InRange(first, 33, 63);
			for (var i = 1; i < path.Count; i++)
				Assert.True(Math.Abs(path[i].X - path[i - 1].X) + Math.Abs(path[i].Y - path[i - 1].Y) <= 1);
		}
	}

	[Fact]
	public void Each_wander_ends_between_1_and_4_tiles_from_where_it_began_on_the_same_level()
	{
		for (var seed = 0; seed < 20; seed++)
		{
			var sim = Simulation.NewWorld(seed);
			var s = sim.State;
			var from = (s.GoblinX[0], s.GoblinY[0]);
			var legs = 0;
			for (var i = 0; i < 600; i++)
			{
				var before = (GoblinTask)s.GoblinTask[0];
				RunTicks(sim, 1);
				if (before == GoblinTask.Wandering && (GoblinTask)s.GoblinTask[0] == GoblinTask.Idle)
				{
					var dx = Math.Abs(s.GoblinX[0] - from.Item1);
					var dy = Math.Abs(s.GoblinY[0] - from.Item2);
					Assert.InRange(Math.Max(dx, dy), 1, 4);
					Assert.Equal(0, s.GoblinZ[0]);
					from = (s.GoblinX[0], s.GoblinY[0]);
					legs++;
				}
			}
			Assert.True(legs >= 3, $"Seed {seed}: only {legs} wanders in 600 ticks.");
		}
	}

	[Fact]
	public void The_wander_path_is_identical_across_two_runs_from_the_same_seed_and_differs_for_another_seed()
	{
		var first = Path(seed: 7, ticks: 600);
		var second = Path(seed: 7, ticks: 600);
		var other = Path(seed: 8, ticks: 600);

		Assert.Equal(first, second);
		Assert.NotEqual(first, other);
		Assert.Contains(first, p => p != (16, 16, 0));
	}

	[Fact]
	public void Designating_a_reachable_tile_mid_wander_makes_the_goblin_take_the_job()
	{
		var sim = MidWander(seed: 3);
		var s = sim.State;
		var target = s.TileIndex(20, 20, 0);

		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 20, 20, 20, 20));
		RunTicks(sim, 1);

		Assert.Equal(target, s.GoblinJobTile[0]);
		Assert.NotEqual((byte)GoblinTask.Wandering, s.GoblinTask[0]);
		Assert.Equal(-1, s.GoblinWanderTile[0]);

		RunTicks(sim, 200);
		Assert.Equal(TileKind.Stairs, s.KindAt(target));
		Assert.Contains(new TileDug(20, 20, 0, Material.Dirt), sim.DrainEvents());
	}

	[Fact]
	public void A_goblin_that_finishes_a_job_pauses_and_wanders_again()
	{
		var sim = Simulation.NewWorld(seed: 3);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 15, 16, 15));
		RunTicks(sim, 20);
		Assert.Equal((byte)GoblinTask.Idle, s.GoblinTask[0]);
		var restingAt = (s.GoblinX[0], s.GoblinY[0]);

		RunTicks(sim, 70);

		Assert.NotEqual(restingAt, (s.GoblinX[0], s.GoblinY[0]));
	}

	[Fact]
	public void A_designation_it_cannot_reach_does_not_stop_the_goblin_wandering()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 1, 5, 5, 5, 5));

		RunTicks(sim, 200);

		Assert.Equal(-1, s.GoblinJobTile[0]);
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(s.TileIndex(5, 5, 1)));
		Assert.NotEqual((16, 16), (s.GoblinX[0], s.GoblinY[0]));
	}

	[Fact]
	public void A_wander_stays_on_the_goblins_level_even_when_Stairs_lead_elsewhere()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		// Stairs at (16, 16) on z0 and z1, with the goblin on z1, where every neighbour is Solid.
		s.TileKinds[s.TileIndex(16, 16, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[s.TileIndex(16, 16, 1)] = (byte)TileKind.Stairs;
		s.GoblinZ[0] = 1;

		RunTicks(sim, 600);

		Assert.Equal((16, 16, 1), (s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]));
	}

	[Fact]
	public void A_wander_never_takes_a_route_that_leaves_the_level()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		// A wall across z0 (x = 18) with Stairs through it to z1 and a z1 corridor round the wall:
		// the goblin at (16, 16) can only reach the far side by changing level, so it must never go there.
		for (var y = 0; y < s.Height; y++)
			s.TileKinds[s.TileIndex(18, y, 0)] = (byte)TileKind.Solid;
		s.TileKinds[s.TileIndex(17, 16, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[s.TileIndex(17, 16, 1)] = (byte)TileKind.Stairs;
		s.TileKinds[s.TileIndex(18, 16, 1)] = (byte)TileKind.Floor;
		s.TileKinds[s.TileIndex(19, 16, 1)] = (byte)TileKind.Stairs;
		s.TileKinds[s.TileIndex(19, 16, 0)] = (byte)TileKind.Stairs;

		var wandering = 0;
		for (var i = 0; i < 3000; i++)
		{
			RunTicks(sim, 1);
			Assert.Equal(0, s.GoblinZ[0]);
			Assert.True(s.GoblinX[0] <= 17, $"The goblin crossed the wall to x={s.GoblinX[0]}.");
			// A wander toward the far side could never finish on this level, so it must never start.
			wandering = (GoblinTask)s.GoblinTask[0] == GoblinTask.Wandering ? wandering + 1 : 0;
			Assert.True(wandering <= 60, "The goblin is stuck wandering toward a tile on another level.");
		}
	}

	// Runs until goblin 0 is wandering and returns that sim.
	static Simulation MidWander(int seed)
	{
		var sim = Simulation.NewWorld(seed);
		var s = sim.State;
		for (var i = 0; i < 300 && (GoblinTask)s.GoblinTask[0] != GoblinTask.Wandering; i++)
			RunTicks(sim, 1);
		Assert.Equal((byte)GoblinTask.Wandering, s.GoblinTask[0]);
		return sim;
	}

	[Fact]
	public void A_world_saved_mid_wander_wanders_on_exactly_as_the_original_does()
	{
		var original = MidWander(seed: 5);
		var restored = SimJson.Deserialize(SimJson.Serialize(original));

		RunTicks(original, 400);
		RunTicks(restored, 400);

		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));
	}

	[Fact]
	public void A_world_saved_during_the_pause_before_a_wander_loads_and_wanders_on_exactly_as_the_original_does()
	{
		var original = Simulation.NewWorld(seed: 5);
		RunTicks(original, 10);
		Assert.InRange(original.State.GoblinWanderTicks[0], 1, GoblinTasks.MaxWanderPauseTicks);
		var restored = SimJson.Deserialize(SimJson.Serialize(original));

		RunTicks(original, 400);
		RunTicks(restored, 400);

		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));
	}

	[Theory]
	[InlineData("off the map")]
	[InlineData("not walkable")]
	[InlineData("on another level")]
	[InlineData("the tile it stands on")]
	[InlineData("none at all")]
	public void A_wandering_goblin_with_a_target_it_could_not_have_picked_is_rejected(string why)
	{
		var sim = MidWander(seed: 5);
		var s = sim.State;
		var standing = s.TileIndex(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]);
		var target = why switch
		{
			"off the map" => s.TileKinds.Length,
			"not walkable" => s.TileIndex(0, 0, 0),
			"on another level" => s.TileIndex(s.GoblinX[0] + 1, s.GoblinY[0], 1),
			"the tile it stands on" => standing,
			_ => -1,
		};
		// Rock over the tile, so "not walkable" bites on the walkable surface.
		if (why == "not walkable")
			s.TileKinds[target] = (byte)TileKind.Solid;
		// The "another level" tile is made walkable, so only its level is wrong.
		if (why == "on another level")
			s.TileKinds[target] = (byte)TileKind.Floor;
		s.GoblinWanderTile[0] = target;

		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Fact]
	public void A_wandering_goblin_holding_a_job_or_a_pause_is_rejected()
	{
		var withJob = MidWander(seed: 5);
		withJob.State.Designations[withJob.State.TileIndex(1, 1, 0)] = (byte)DesignationKind.DigStairs;
		withJob.State.GoblinJobTile[0] = withJob.State.TileIndex(1, 1, 0);
		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(withJob)));

		var withPause = MidWander(seed: 5);
		withPause.State.GoblinWanderTicks[0] = 5;
		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(withPause)));
	}

	[Fact]
	public void An_idle_goblin_with_a_wander_target_or_a_pause_past_the_longest_is_rejected()
	{
		var withTarget = Simulation.NewWorld(seed: 5);
		withTarget.State.GoblinWanderTile[0] = withTarget.State.TileIndex(17, 16, 0);
		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(withTarget)));

		var tooLong = Simulation.NewWorld(seed: 5);
		tooLong.State.GoblinWanderTicks[0] = GoblinTasks.MaxWanderPauseTicks + 1;
		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(tooLong)));
	}

	[Theory]
	[InlineData("GoblinWanderTile")]
	[InlineData("GoblinWanderTicks")]
	public void A_save_missing_the_wander_state_is_rejected(string property)
	{
		var node = System.Text.Json.Nodes.JsonNode.Parse(SimJson.Serialize(Simulation.NewWorld(seed: 5)))!;
		node[property] = null;

		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(node.ToJsonString()));
	}
}
