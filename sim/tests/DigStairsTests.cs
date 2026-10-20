using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

public class DigStairsTests
{
	// One tick of wall time at 1×.
	const double OneTick = 0.1;

	static void RunTicks(Simulation sim, int ticks)
	{
		for (var i = 0; i < ticks; i++)
			Assert.Equal(1, sim.Advance(OneTick));
	}

	static Designate DigStairsAt(int x, int y, int z) => new(DesignationKind.DigStairs, z, x, y, x, y);

	[Fact]
	public void A_dug_stairs_tile_is_Stairs_after_the_walk_and_20_dig_ticks_with_one_tile_dug_event()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		// The Grunt starts at (16, 16); (16, 14) is two tiles away, so one step reaches a neighbour.
		sim.Apply(DigStairsAt(16, 14, 0));
		var target = s.TileIndex(16, 14, 0);

		RunTicks(sim, 3 + 20 - 1);
		Assert.Equal(TileKind.Floor, s.KindAt(target));
		Assert.Empty(sim.DrainEvents());

		RunTicks(sim, 1);
		Assert.Equal(TileKind.Stairs, s.KindAt(target));
		Assert.Equal(new SimEvent[] { new TileDug(16, 14, 0, Material.Dirt) }, sim.DrainEvents());
	}

	[Fact]
	public void Digging_in_place_takes_exactly_the_20_dig_ticks()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(DigStairsAt(16, 15, 0));

		RunTicks(sim, 19);
		Assert.Equal(TileKind.Floor, s.KindAt(s.TileIndex(16, 15, 0)));
		RunTicks(sim, 1);
		Assert.Equal(TileKind.Stairs, s.KindAt(s.TileIndex(16, 15, 0)));
	}

	[Fact]
	public void Dig_time_comes_from_the_material()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var target = s.TileIndex(16, 15, 0);
		s.TileMaterials[target] = (byte)Material.Stone;
		sim.Apply(DigStairsAt(16, 15, 0));

		RunTicks(sim, MaterialRules.DigTicks(Material.Stone) - 1);
		Assert.Equal(TileKind.Floor, s.KindAt(target));
		RunTicks(sim, 1);
		Assert.Equal(TileKind.Stairs, s.KindAt(target));
		Assert.Equal(new SimEvent[] { new TileDug(16, 15, 0, Material.Stone) }, sim.DrainEvents());
		Assert.NotEqual(MaterialRules.DigTicks(Material.Dirt), MaterialRules.DigTicks(Material.Stone));
	}

	[Fact]
	public void Designating_a_tile_that_is_already_Stairs_is_ignored()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var target = s.TileIndex(16, 15, 0);
		s.TileKinds[target] = (byte)TileKind.Stairs;

		sim.Apply(DigStairsAt(16, 15, 0));

		Assert.Equal(DesignationKind.None, s.DesignationAt(target));
		RunTicks(sim, 30);
		Assert.Empty(sim.DrainEvents());
	}

	[Fact]
	public void A_tile_with_nothing_to_dig_cannot_be_designated()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var target = s.TileIndex(16, 15, 0);
		s.TileMaterials[target] = (byte)Material.None;

		sim.Apply(DigStairsAt(16, 15, 0));

		Assert.Equal(DesignationKind.None, s.DesignationAt(target));
	}

	[Fact]
	public void A_rectangle_designates_every_tile_inside_it_whatever_order_the_corners_come_in()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;

		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 12, 7, 10, 5));

		for (var y = 0; y < s.Height; y++)
		for (var x = 0; x < s.Width; x++)
		{
			var inside = x is >= 10 and <= 12 && y is >= 5 and <= 7;
			Assert.Equal(inside ? DesignationKind.DigStairs : DesignationKind.None, s.DesignationAt(s.TileIndex(x, y, 0)));
		}
	}

	[Fact]
	public void A_rectangle_partly_off_the_map_designates_only_the_part_on_it()
	{
		var corner = Simulation.NewWorld(seed: 1);
		corner.Apply(new Designate(DesignationKind.DigStairs, 0, -5, -5, 0, 0));
		Assert.Equal(1, CountDesignated(corner.State));
		Assert.Equal(DesignationKind.DigStairs, corner.State.DesignationAt(corner.State.TileIndex(0, 0, 0)));

		var everything = Simulation.NewWorld(seed: 1);
		everything.Apply(new Designate(DesignationKind.DigStairs, 0, int.MinValue, int.MaxValue, int.MaxValue, int.MinValue));
		Assert.Equal(32 * 32, CountDesignated(everything.State));
	}

	[Theory]
	[InlineData(-10, 3, -2, 6)]
	[InlineData(32, 3, 40, 6)]
	[InlineData(3, -9, 6, -1)]
	[InlineData(3, 32, 6, 33)]
	[InlineData(int.MaxValue, int.MaxValue, int.MaxValue - 1, int.MaxValue)]
	[InlineData(int.MinValue, int.MinValue, int.MinValue, int.MinValue)]
	public void A_rectangle_wholly_off_the_map_designates_nothing(int x0, int y0, int x1, int y1)
	{
		var sim = Simulation.NewWorld(seed: 1);

		sim.Apply(new Designate(DesignationKind.DigStairs, 0, x0, y0, x1, y1));

		Assert.Equal(0, CountDesignated(sim.State));
	}

	static int CountDesignated(SimState s)
	{
		var count = 0;
		for (var tile = 0; tile < s.Designations.Length; tile++)
			if (s.DesignationAt(tile) != DesignationKind.None)
				count++;
		return count;
	}

	[Fact]
	public void Designating_off_the_map_levels_or_with_no_kind_throws()
	{
		var sim = Simulation.NewWorld(seed: 1);

		Assert.Throws<ArgumentOutOfRangeException>(() => sim.Apply(DigStairsAt(1, 1, 3)));
		Assert.Throws<ArgumentOutOfRangeException>(() => sim.Apply(DigStairsAt(1, 1, -1)));
		Assert.Throws<ArgumentOutOfRangeException>(() => sim.Apply(new Designate(DesignationKind.None, 0, 1, 1, 1, 1)));
	}

	[Fact]
	public void The_goblin_takes_the_nearest_designation_by_path_length_and_ties_go_to_the_lowest_tile_index()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		// The north and east targets are each one step from a standing tile, so they tie and the lower
		// tile index (north) goes first. The far one comes last.
		sim.Apply(DigStairsAt(16, 14, 0));
		sim.Apply(DigStairsAt(18, 16, 0));
		sim.Apply(DigStairsAt(16, 2, 0));

		RunTicks(sim, 1);
		Assert.Equal(s.TileIndex(16, 14, 0), s.GoblinJobTile[0]);

		RunTicks(sim, 22);
		sim.DrainEvents();
		RunTicks(sim, 1);
		Assert.Equal(s.TileIndex(18, 16, 0), s.GoblinJobTile[0]);

		// Two steps plus 20 dig ticks finish the east target on tick 49; the far target is claimed on tick 50.
		RunTicks(sim, 26);
		Assert.Equal(s.TileIndex(16, 2, 0), s.GoblinJobTile[0]);
	}

	[Fact]
	public void Two_goblins_never_share_a_designation()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		// (16, 14) is one step from both goblins, so both want it first. The far one is five steps from goblin 1.
		s.GoblinCount = 2;
		(s.GoblinX[1], s.GoblinY[1]) = (16, 12);
		sim.Apply(DigStairsAt(16, 14, 0));
		sim.Apply(DigStairsAt(20, 14, 0));

		var dug = new List<(long Tick, SimEvent Event)>();
		for (var i = 0; i < 40; i++)
		{
			RunTicks(sim, 1);
			foreach (var e in sim.DrainEvents())
				dug.Add((s.Tick, e));
		}

		// Goblin 0 digs the near one alone: one step, 20 dig ticks. Goblin 1 takes the far one: five steps, 20 dig ticks.
		Assert.Equal(new (long, SimEvent)[]
		{
			(3 + 20, new TileDug(16, 14, 0, Material.Dirt)),
			(5 * 3 + 20, new TileDug(20, 14, 0, Material.Dirt)),
		}, dug);
	}

	[Fact]
	public void The_goblin_ends_up_beside_the_tile_it_dug_and_takes_no_diagonal_steps()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(DigStairsAt(20, 20, 0));

		var lastX = s.GoblinX[0];
		var lastY = s.GoblinY[0];
		do
		{
			RunTicks(sim, 1);
			var step = Math.Abs(s.GoblinX[0] - lastX) + Math.Abs(s.GoblinY[0] - lastY);
			Assert.True(step <= 1, "The goblin stepped diagonally.");
			lastX = s.GoblinX[0];
			lastY = s.GoblinY[0];
		} while (s.GoblinTask[0] != (byte)GoblinTask.Idle);

		// A neighbour of (20, 20) is 7 orthogonal steps from (16, 16): 7 × 3 walk ticks, then 20 dig ticks.
		Assert.Equal(7 * 3 + 20, s.Tick);
		Assert.Equal(1, Math.Abs(s.GoblinX[0] - 20) + Math.Abs(s.GoblinY[0] - 20));
	}

	[Fact]
	public void A_tile_on_the_map_edge_is_dug_from_a_real_neighbour_not_from_across_the_map()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		// (31, 4) is next to (0, 5) in the tile array, but 31 steps from either neighbour of it.
		(s.GoblinX[0], s.GoblinY[0]) = (31, 4);
		sim.Apply(DigStairsAt(0, 5, 0));

		RunTicks(sim, 31 * 3 + 20 - 1);
		Assert.Equal(TileKind.Floor, s.KindAt(s.TileIndex(0, 5, 0)));
		RunTicks(sim, 1);
		Assert.Equal(TileKind.Stairs, s.KindAt(s.TileIndex(0, 5, 0)));
		Assert.Equal(1, Math.Abs(s.GoblinX[0] - 0) + Math.Abs(s.GoblinY[0] - 5));
	}

	[Fact]
	public void A_tile_on_the_top_row_of_a_level_cannot_be_dug_from_the_level_above()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		// (5, 0, 1) follows (5, 31, 0) in the tile array; z0 Floor there is not a place to dig it from.
		sim.Apply(DigStairsAt(5, 0, 1));

		RunTicks(sim, 200);

		Assert.Equal(TileKind.Solid, s.KindAt(s.TileIndex(5, 0, 1)));
		Assert.Empty(sim.DrainEvents());
	}

	[Fact]
	public void The_tile_being_dug_shows_its_progress()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var target = s.TileIndex(16, 15, 0);
		sim.Apply(DigStairsAt(16, 15, 0));

		RunTicks(sim, 5);

		Assert.Equal(5, s.DigProgress[target]);
	}

	[Fact]
	public void Stairs_can_be_dug_into_solid_from_stairs_directly_above()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(DigStairsAt(16, 15, 0));
		RunTicks(sim, 20);
		sim.DrainEvents();

		sim.Apply(DigStairsAt(16, 15, 1));
		// 1 step onto the z0 Stairs, then 20 dig ticks; the dig comes from above, not beside.
		RunTicks(sim, 3 + 20);

		Assert.Equal(TileKind.Stairs, s.KindAt(s.TileIndex(16, 15, 1)));
		Assert.Equal(new SimEvent[] { new TileDug(16, 15, 1, Material.Dirt) }, sim.DrainEvents());
		Assert.Equal((16, 15, 0), (s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]));
	}

	[Fact]
	public void A_designation_with_no_way_to_reach_it_waits_and_the_goblin_stays_idle()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(DigStairsAt(5, 5, 1));

		RunTicks(sim, 50);

		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(s.TileIndex(5, 5, 1)));
		Assert.Equal((byte)GoblinTask.Idle, s.GoblinTask[0]);
		Assert.Empty(sim.DrainEvents());
	}

	[Fact]
	public void Replaying_a_seed_and_command_log_twice_gives_identical_state()
	{
		var log = new (int Tick, Command Command)[]
		{
			(0, DigStairsAt(16, 14, 0)),
			(5, new Designate(DesignationKind.DigStairs, 0, 20, 20, 22, 21)),
			(40, new SetSpeed(4)),
			(41, DigStairsAt(16, 14, 1)),
			(200, new SetPaused(true)),
		};

		var first = Replay(seed: 99, log, ticks: 400);
		var second = Replay(seed: 99, log, ticks: 400);

		Assert.Equal(SimJson.Serialize(first), SimJson.Serialize(second));
		Assert.Contains(first.State.TileKinds, kind => kind == (byte)TileKind.Stairs);
	}

	[Fact]
	public void A_world_saved_mid_dig_finishes_the_dig_exactly_as_the_original_does()
	{
		var original = Simulation.NewWorld(seed: 5);
		original.Apply(DigStairsAt(16, 14, 0));
		RunTicks(original, 10);
		var restored = SimJson.Deserialize(SimJson.Serialize(original));

		RunTicks(original, 13);
		RunTicks(restored, 13);

		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));
		Assert.Single(restored.DrainEvents());
	}

	[Fact]
	public void A_world_saved_after_a_dig_loads_and_digs_on_exactly_as_the_original_does()
	{
		var original = Simulation.NewWorld(seed: 5);
		original.Apply(DigStairsAt(16, 15, 0));
		RunTicks(original, 20);
		Assert.Equal(TileKind.Stairs, original.State.KindAt(original.State.TileIndex(16, 15, 0)));

		var restored = SimJson.Deserialize(SimJson.Serialize(original));
		original.Apply(DigStairsAt(16, 13, 0));
		restored.Apply(DigStairsAt(16, 13, 0));
		RunTicks(original, 30);
		RunTicks(restored, 30);

		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));
		SimJson.Deserialize(SimJson.Serialize(restored));
	}

	static Simulation Replay(int seed, (int Tick, Command Command)[] log, int ticks)
	{
		var sim = Simulation.NewWorld(seed);
		var next = 0;
		for (var tick = 0; tick < ticks; tick++)
		{
			while (next < log.Length && log[next].Tick == tick)
				sim.Apply(log[next++].Command);
			sim.Advance(OneTick);
		}
		return sim;
	}
}
