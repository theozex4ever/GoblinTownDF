using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

public class GoblinTasksTests
{
	// NewWorld puts goblin 0 at (16, 16) on the dirt-floor surface. Dig stairs can be designated on Floor.
	static Simulation WorldWithDesignation(int x, int y, out int tile)
	{
		var sim = Simulation.NewWorld(seed: 7);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, x, y, x, y));
		tile = sim.State.TileIndex(x, y, 0);
		return sim;
	}

	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	[Fact]
	public void BeginJob_walks_to_a_job_the_goblin_is_not_yet_beside()
	{
		var sim = WorldWithDesignation(20, 16, out var tile);
		var s = sim.State;

		GoblinTasks.BeginJob(s, 0, tile, digging: false);

		Assert.Equal(GoblinTask.Walking, TaskOf(s, 0));
		Assert.Equal(tile, s.GoblinJobTile[0]);
		Assert.Equal(0, s.GoblinStepTicks[0]);
		Assert.Equal(-1, s.GoblinWanderTile[0]);
		Assert.Equal(0, s.GoblinWanderTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void BeginJob_digs_at_once_when_the_goblin_already_stands_where_it_can_dig()
	{
		var sim = WorldWithDesignation(17, 16, out var tile);
		var s = sim.State;

		GoblinTasks.BeginJob(s, 0, tile, digging: true);

		Assert.Equal(GoblinTask.Digging, TaskOf(s, 0));
		Assert.Equal(tile, s.GoblinJobTile[0]);
		Assert.Equal(-1, s.GoblinWanderTile[0]);
		Assert.Equal(0, s.GoblinWanderTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void BeginJob_takes_a_wandering_goblin_off_its_wander()
	{
		var sim = WorldWithDesignation(20, 16, out var tile);
		var s = sim.State;
		GoblinTasks.BeginWander(s, 0, s.TileIndex(18, 18, 0));
		s.GoblinStepTicks[0] = 2;

		GoblinTasks.BeginJob(s, 0, tile, digging: false);

		Assert.Equal(GoblinTask.Walking, TaskOf(s, 0));
		Assert.Equal(-1, s.GoblinWanderTile[0]);
		Assert.Equal(0, s.GoblinStepTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void StartDigging_turns_a_walk_into_a_dig_on_arrival()
	{
		var sim = WorldWithDesignation(17, 16, out var tile);
		var s = sim.State;
		GoblinTasks.BeginJob(s, 0, tile, digging: false);
		s.GoblinStepTicks[0] = 2;

		GoblinTasks.StartDigging(s, 0);

		Assert.Equal(GoblinTask.Digging, TaskOf(s, 0));
		Assert.Equal(tile, s.GoblinJobTile[0]);
		Assert.Equal(0, s.GoblinStepTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void BeginWander_sets_the_target_and_starts_a_fresh_walk()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		var target = s.TileIndex(18, 14, 0);

		GoblinTasks.BeginWander(s, 0, target);

		Assert.Equal(GoblinTask.Wandering, TaskOf(s, 0));
		Assert.Equal(target, s.GoblinWanderTile[0]);
		Assert.Equal(0, s.GoblinWanderTicks[0]);
		Assert.Equal(0, s.GoblinStepTicks[0]);
		Assert.Equal(-1, s.GoblinJobTile[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void Release_returns_a_digging_goblin_to_idle_with_nothing_held()
	{
		var sim = WorldWithDesignation(17, 16, out var tile);
		var s = sim.State;
		GoblinTasks.BeginJob(s, 0, tile, digging: true);

		GoblinTasks.Release(s, 0);

		AssertIdle(s);
	}

	[Fact]
	public void Release_returns_a_wandering_goblin_to_idle_with_nothing_held()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		GoblinTasks.BeginWander(s, 0, s.TileIndex(18, 14, 0));
		s.GoblinStepTicks[0] = 1;

		GoblinTasks.Release(s, 0);

		AssertIdle(s);
	}

	static void AssertIdle(SimState s)
	{
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(-1, s.GoblinJobTile[0]);
		Assert.Equal(-1, s.GoblinWanderTile[0]);
		Assert.Equal(0, s.GoblinWanderTicks[0]);
		Assert.Equal(0, s.GoblinStepTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void CountDownPause_draws_a_pause_of_3_to_6_seconds_then_runs_out_exactly_once()
	{
		var s = Simulation.NewWorld(seed: 7).State;

		var finished = GoblinTasks.CountDownPause(s, 0);
		var drawn = s.GoblinWanderTicks[0] + 1;
		Assert.InRange(drawn, GoblinTasks.MinWanderPauseTicks, GoblinTasks.MaxWanderPauseTicks);
		Assert.False(finished);
		Assert.Null(GoblinTasks.FindViolation(s));

		for (var tick = 2; tick < drawn; tick++)
		{
			Assert.False(GoblinTasks.CountDownPause(s, 0));
			Assert.Null(GoblinTasks.FindViolation(s));
		}

		Assert.True(GoblinTasks.CountDownPause(s, 0));
		Assert.Equal(0, s.GoblinWanderTicks[0]);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void CountDownPause_draws_from_the_sim_rng_so_a_seed_replays_the_same_pause()
	{
		var a = Simulation.NewWorld(seed: 99).State;
		var b = Simulation.NewWorld(seed: 99).State;

		GoblinTasks.CountDownPause(a, 0);
		GoblinTasks.CountDownPause(b, 0);

		Assert.Equal(a.GoblinWanderTicks[0], b.GoblinWanderTicks[0]);
	}

	[Fact]
	public void FindViolation_flags_a_job_tile_that_is_not_designated()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		s.GoblinTask[0] = (byte)GoblinTask.Walking;
		s.GoblinJobTile[0] = s.TileIndex(20, 16, 0);

		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_flags_a_wander_target_on_the_goblins_own_tile()
	{
		var s = Simulation.NewWorld(seed: 7).State;

		GoblinTasks.BeginWander(s, 0, s.TileIndex(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]));

		Assert.NotNull(GoblinTasks.FindViolation(s));
	}
}
