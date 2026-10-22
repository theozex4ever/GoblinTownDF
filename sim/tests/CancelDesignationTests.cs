using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

public class CancelDesignationTests
{
	// One tick of wall time at 1×.
	const double OneTick = 0.1;

	static void RunTicks(Simulation sim, int ticks)
	{
		for (var i = 0; i < ticks; i++)
			Assert.Equal(1, sim.Advance(OneTick));
	}

	static Designate DigStairsAt(int x, int y, int z) => new(DesignationKind.DigStairs, z, x, y, x, y);

	static CancelDesignation CancelAt(int x, int y, int z) => new(z, x, y, x, y);

	static int CountDesignated(SimState s) => s.Designations.Count(d => d != (byte)DesignationKind.None);

	[Fact]
	public void Cancelling_a_designation_leaves_no_designation_no_job_and_the_tile_unchanged()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var target = s.TileIndex(16, 14, 0);
		var kindBefore = s.KindAt(target);
		var materialBefore = s.MaterialAt(target);
		sim.Apply(DigStairsAt(16, 14, 0));
		RunTicks(sim, 1);
		Assert.Equal(target, s.GoblinJobTile[0]);

		sim.Apply(CancelAt(16, 14, 0));

		Assert.Equal(DesignationKind.None, s.DesignationAt(target));
		Assert.Equal(0, s.DigProgress[target]);
		Assert.Equal(-1, s.GoblinJobTile[0]);
		Assert.Equal((byte)GoblinTask.Idle, s.GoblinTask[0]);
		Assert.Equal(kindBefore, s.KindAt(target));
		Assert.Equal(materialBefore, s.MaterialAt(target));

		// Nothing is left to claim, so the goblin stays idle and nothing is ever dug.
		RunTicks(sim, 60);
		Assert.Equal((byte)GoblinTask.Idle, s.GoblinTask[0]);
		Assert.Equal(kindBefore, s.KindAt(target));
		Assert.Empty(sim.DrainEvents());
	}

	[Fact]
	public void Cancelling_halfway_through_a_dig_throws_the_progress_away_and_emits_no_tile_dug_event()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		// (16, 15) is next to the Grunt at (16, 16), so it digs in place with no walk.
		var target = s.TileIndex(16, 15, 0);
		sim.Apply(DigStairsAt(16, 15, 0));
		RunTicks(sim, 10);
		Assert.Equal((byte)GoblinTask.Digging, s.GoblinTask[0]);
		Assert.True(s.DigProgress[target] > 0);

		sim.Apply(CancelAt(16, 15, 0));

		Assert.Equal(DesignationKind.None, s.DesignationAt(target));
		Assert.Equal(0, s.DigProgress[target]);
		Assert.Equal(TileKind.Floor, s.KindAt(target));
		Assert.Equal((byte)GoblinTask.Idle, s.GoblinTask[0]);
		Assert.Equal(-1, s.GoblinJobTile[0]);
		RunTicks(sim, 40);
		Assert.Equal(TileKind.Floor, s.KindAt(target));
		Assert.Empty(sim.DrainEvents());
	}

	[Fact]
	public void Designating_a_cancelled_tile_again_starts_from_zero_progress()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var target = s.TileIndex(16, 15, 0);
		sim.Apply(DigStairsAt(16, 15, 0));
		RunTicks(sim, 10);
		sim.Apply(CancelAt(16, 15, 0));

		sim.Apply(DigStairsAt(16, 15, 0));
		Assert.Equal(0, s.DigProgress[target]);

		// A fresh dig takes the full 20 ticks; kept progress would have finished it sooner.
		RunTicks(sim, 19);
		Assert.Equal(TileKind.Floor, s.KindAt(target));
		Assert.Empty(sim.DrainEvents());
		RunTicks(sim, 1);
		Assert.Equal(TileKind.Stairs, s.KindAt(target));
		Assert.Single(sim.DrainEvents());
	}

	[Fact]
	public void After_cancelling_the_tile_it_was_digging_the_goblin_moves_on_to_its_next_job()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var first = s.TileIndex(16, 15, 0);
		var next = s.TileIndex(16, 18, 0);
		sim.Apply(DigStairsAt(16, 15, 0));
		sim.Apply(DigStairsAt(16, 18, 0));
		RunTicks(sim, 10);
		Assert.Equal(first, s.GoblinJobTile[0]);

		sim.Apply(CancelAt(16, 15, 0));
		RunTicks(sim, 1);

		Assert.Equal(next, s.GoblinJobTile[0]);
		RunTicks(sim, 40);
		Assert.Equal(TileKind.Floor, s.KindAt(first));
		Assert.Equal(TileKind.Stairs, s.KindAt(next));
		Assert.Equal(new SimEvent[] { new TileDug(16, 18, 0, Material.Dirt) }, sim.DrainEvents());
	}

	[Fact]
	public void Cancelling_a_tile_the_goblin_is_still_walking_to_frees_the_goblin()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var target = s.TileIndex(16, 10, 0);
		sim.Apply(DigStairsAt(16, 10, 0));
		RunTicks(sim, 4);
		Assert.Equal((byte)GoblinTask.Walking, s.GoblinTask[0]);

		sim.Apply(CancelAt(16, 10, 0));

		Assert.Equal((byte)GoblinTask.Idle, s.GoblinTask[0]);
		Assert.Equal(-1, s.GoblinJobTile[0]);
		Assert.Equal(0, s.GoblinStepTicks[0]);
		Assert.Equal(DesignationKind.None, s.DesignationAt(target));
	}

	[Fact]
	public void Cancelling_a_tile_with_no_designation_does_nothing()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(DigStairsAt(16, 10, 0));
		RunTicks(sim, 5);
		sim.DrainEvents();
		var before = SimJson.Serialize(sim);

		sim.Apply(CancelAt(3, 3, 0));
		sim.Apply(CancelAt(16, 10, 1));

		Assert.Equal(before, SimJson.Serialize(sim));
		Assert.Empty(sim.DrainEvents());
	}

	[Fact]
	public void Cancelling_leaves_the_jobs_of_other_tiles_alone()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var mine = s.TileIndex(16, 15, 0);
		sim.Apply(DigStairsAt(16, 15, 0));
		sim.Apply(DigStairsAt(20, 20, 0));
		RunTicks(sim, 1);
		Assert.Equal(mine, s.GoblinJobTile[0]);

		sim.Apply(CancelAt(20, 20, 0));

		Assert.Equal(mine, s.GoblinJobTile[0]);
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(mine));
		Assert.Equal(1, CountDesignated(s));
	}

	[Fact]
	public void A_rectangle_cancels_every_designation_inside_it_whatever_order_the_corners_come_in()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 8, 4, 14, 9));

		sim.Apply(new CancelDesignation(0, 12, 7, 10, 5));

		for (var y = 0; y < s.Height; y++)
		for (var x = 0; x < s.Width; x++)
		{
			var designated = x is >= 8 and <= 14 && y is >= 4 and <= 9;
			var cancelled = x is >= 10 and <= 12 && y is >= 5 and <= 7;
			Assert.Equal(designated && !cancelled ? DesignationKind.DigStairs : DesignationKind.None, s.DesignationAt(s.TileIndex(x, y, 0)));
		}
	}

	[Fact]
	public void A_rectangle_cancels_only_the_level_it_names()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 5, 5, 6, 6));
		sim.Apply(new Designate(DesignationKind.DigStairs, 1, 5, 5, 6, 6));

		sim.Apply(new CancelDesignation(1, 0, 0, 31, 31));

		Assert.Equal(4, CountDesignated(s));
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(s.TileIndex(5, 5, 0)));
		Assert.Equal(DesignationKind.None, s.DesignationAt(s.TileIndex(5, 5, 1)));
	}

	[Fact]
	public void A_rectangle_partly_off_the_map_cancels_only_the_part_on_it()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 0, 0, 2, 2));

		sim.Apply(new CancelDesignation(0, -5, -5, 0, 0));
		Assert.Equal(8, CountDesignated(s));
		Assert.Equal(DesignationKind.None, s.DesignationAt(s.TileIndex(0, 0, 0)));

		sim.Apply(new CancelDesignation(0, int.MinValue, int.MaxValue, int.MaxValue, int.MinValue));
		Assert.Equal(0, CountDesignated(s));
	}

	[Theory]
	[InlineData(-10, 3, -2, 6)]
	[InlineData(32, 3, 40, 6)]
	[InlineData(3, -9, 6, -1)]
	[InlineData(3, 32, 6, 33)]
	[InlineData(int.MaxValue, int.MaxValue, int.MaxValue - 1, int.MaxValue)]
	[InlineData(int.MinValue, int.MinValue, int.MinValue, int.MinValue)]
	public void A_rectangle_wholly_off_the_map_cancels_nothing(int x0, int y0, int x1, int y1)
	{
		var sim = Simulation.NewWorld(seed: 1);
		// Designations on the edge rows and columns, where a bad clip would reach.
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 0, 0, 31, 31));

		sim.Apply(new CancelDesignation(0, x0, y0, x1, y1));

		Assert.Equal(32 * 32, CountDesignated(sim.State));
	}

	[Fact]
	public void Cancelling_on_a_level_off_the_map_throws()
	{
		var sim = Simulation.NewWorld(seed: 1);

		Assert.Throws<ArgumentOutOfRangeException>(() => sim.Apply(CancelAt(1, 1, 3)));
		Assert.Throws<ArgumentOutOfRangeException>(() => sim.Apply(CancelAt(1, 1, -1)));
	}

	[Fact]
	public void Replaying_a_seed_and_command_log_with_cancels_twice_gives_identical_state()
	{
		var log = new (int Tick, Command Command)[]
		{
			(0, DigStairsAt(16, 15, 0)),
			(0, new Designate(DesignationKind.DigStairs, 0, 20, 20, 22, 21)),
			(10, CancelAt(16, 15, 0)),
			(11, DigStairsAt(16, 15, 0)),
			(60, new CancelDesignation(0, 21, 20, 22, 21)),
			(100, new SetSpeed(4)),
		};

		var first = Replay(seed: 99, log, ticks: 400);
		var second = Replay(seed: 99, log, ticks: 400);

		Assert.Equal(SimJson.Serialize(first), SimJson.Serialize(second));
		Assert.Equal(TileKind.Stairs, first.State.KindAt(first.State.TileIndex(16, 15, 0)));
		Assert.Equal(TileKind.Stairs, first.State.KindAt(first.State.TileIndex(20, 20, 0)));
	}

	[Fact]
	public void A_world_saved_after_a_cancel_loads_and_digs_on_exactly_as_the_original_does()
	{
		var original = Simulation.NewWorld(seed: 5);
		original.Apply(DigStairsAt(16, 15, 0));
		RunTicks(original, 10);
		original.Apply(CancelAt(16, 15, 0));
		var restored = SimJson.Deserialize(SimJson.Serialize(original));

		original.Apply(DigStairsAt(16, 15, 0));
		restored.Apply(DigStairsAt(16, 15, 0));
		RunTicks(original, 25);
		RunTicks(restored, 25);

		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));
		Assert.Single(restored.DrainEvents());
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
