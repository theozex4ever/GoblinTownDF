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

	// --- The carry Tasks. Goblin 0 stands at (16, 16, 0); a loose dirt unit lies at (16, 17, 0).

	static SimState WorldWithLooseUnit(out int unit)
	{
		var s = Simulation.NewWorld(seed: 7).State;
		unit = s.TileIndex(16, 17, 0);
		s.AddItems(unit, Material.Dirt, 1);
		return s;
	}

	static void StandOn(SimState s, int g, int x, int y) => (s.GoblinX[g], s.GoblinY[g]) = (x, y);

	[Fact]
	public void BeginFetch_walks_to_a_loose_unit_the_goblin_is_not_yet_on()
	{
		var s = WorldWithLooseUnit(out var unit);
		s.GoblinStepTicks[0] = 2;

		GoblinTasks.BeginFetch(s, 0, unit, pickingUp: false);

		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 0));
		Assert.Equal(unit, s.GoblinJobTile[0]);
		Assert.Equal(0, s.GoblinStepTicks[0]);
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void BeginFetch_picks_up_at_once_when_the_goblin_stands_on_the_unit()
	{
		var s = WorldWithLooseUnit(out var unit);
		StandOn(s, 0, 16, 17);

		GoblinTasks.BeginFetch(s, 0, unit, pickingUp: true);

		Assert.Equal(GoblinTask.PickingUp, TaskOf(s, 0));
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void BeginFetch_takes_a_wandering_goblin_off_its_wander()
	{
		var s = WorldWithLooseUnit(out var unit);
		GoblinTasks.BeginWander(s, 0, s.TileIndex(18, 18, 0));

		GoblinTasks.BeginFetch(s, 0, unit, pickingUp: false);

		Assert.Equal(-1, s.GoblinWanderTile[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void StartPickingUp_turns_a_fetch_into_a_pick_up_on_arrival()
	{
		var s = WorldWithLooseUnit(out var unit);
		GoblinTasks.BeginFetch(s, 0, unit, pickingUp: false);
		StandOn(s, 0, 16, 17);
		s.GoblinStepTicks[0] = 2;

		GoblinTasks.StartPickingUp(s, 0);

		Assert.Equal(GoblinTask.PickingUp, TaskOf(s, 0));
		Assert.Equal(unit, s.GoblinJobTile[0]);
		Assert.Equal(0, s.GoblinStepTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void WorkTick_finishes_a_pick_up_or_drop_on_the_fifth_tick()
	{
		var s = WorldWithLooseUnit(out var unit);
		StandOn(s, 0, 16, 17);
		GoblinTasks.BeginFetch(s, 0, unit, pickingUp: true);

		for (var tick = 1; tick < GoblinTasks.HaulActionTicks; tick++)
		{
			Assert.False(GoblinTasks.WorkTick(s, 0));
			Assert.Null(GoblinTasks.FindViolation(s));
		}

		Assert.True(GoblinTasks.WorkTick(s, 0));
		Assert.Equal(5, GoblinTasks.HaulActionTicks);
	}

	[Fact]
	public void FinishPickUp_leaves_the_goblin_carrying_with_no_job_tile()
	{
		var s = WorldWithLooseUnit(out var unit);
		StandOn(s, 0, 16, 17);
		GoblinTasks.BeginFetch(s, 0, unit, pickingUp: true);
		GoblinTasks.WorkTick(s, 0);
		s.RemoveItems(unit, Material.Dirt, 1);

		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);

		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
		Assert.Equal(-1, s.GoblinJobTile[0]);
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Equal(0, s.GoblinStepTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void StartDropping_needs_a_Pile_under_the_goblin_and_ResumeCarrying_undoes_it()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		s.Piles[s.TileIndex(16, 16, 0)] = 1;
		GoblinTasks.FinishPickUp(s, 0, Material.Stone);

		GoblinTasks.StartDropping(s, 0);
		Assert.Equal(GoblinTask.Dropping, TaskOf(s, 0));
		Assert.Equal((byte)Material.Stone, s.GoblinCarry[0]);
		Assert.Null(GoblinTasks.FindViolation(s));

		GoblinTasks.WorkTick(s, 0);
		s.Piles[s.TileIndex(16, 16, 0)] = 0;
		Assert.NotNull(GoblinTasks.FindViolation(s));

		GoblinTasks.ResumeCarrying(s, 0);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void Release_drops_the_carry_fields_too()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);

		GoblinTasks.Release(s, 0);

		AssertIdle(s);
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(0, s.GoblinWorkTicks[0]);
	}

	[Fact]
	public void FindViolation_flags_a_fetch_to_a_tile_with_no_loose_unit()
	{
		var s = WorldWithLooseUnit(out var unit);
		GoblinTasks.BeginFetch(s, 0, unit, pickingUp: false);
		s.RemoveItems(unit, Material.Dirt, 1);

		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_flags_a_fetch_to_a_unit_that_is_stored()
	{
		var s = WorldWithLooseUnit(out var unit);
		GoblinTasks.BeginFetch(s, 0, unit, pickingUp: false);
		s.Piles[unit] = 1;

		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_flags_a_pick_up_away_from_the_unit()
	{
		var s = WorldWithLooseUnit(out var unit);
		GoblinTasks.BeginFetch(s, 0, unit, pickingUp: true);

		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_flags_two_goblins_claiming_one_loose_unit()
	{
		var s = WorldWithLooseUnit(out var unit);
		GoblinTasks.BeginFetch(s, 0, unit, pickingUp: false);
		GoblinTasks.BeginFetch(s, 1, unit, pickingUp: false);

		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_lets_a_dig_and_a_haul_claim_the_same_tile()
	{
		var sim = Simulation.NewWorld(seed: 7);
		var s = sim.State;
		var tile = s.TileIndex(16, 17, 0);
		s.AddItems(tile, Material.Dirt, 1);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 17, 16, 17));

		GoblinTasks.BeginJob(s, 0, tile, digging: false);
		GoblinTasks.BeginFetch(s, 1, tile, pickingUp: false);

		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_flags_a_carrying_goblin_with_nothing_carried()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		s.GoblinCarry[0] = (byte)Material.None;

		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_flags_a_carried_unit_on_a_goblin_that_is_not_carrying()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		s.GoblinCarry[0] = (byte)Material.Dirt;

		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_flags_an_unknown_carried_material()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		s.GoblinCarry[0] = 99;

		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_flags_work_ticks_on_a_task_that_does_not_work_and_ticks_past_the_limit()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		s.Piles[s.TileIndex(16, 16, 0)] = 1;
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		s.GoblinWorkTicks[0] = 1;
		Assert.NotNull(GoblinTasks.FindViolation(s));

		GoblinTasks.StartDropping(s, 0);
		s.GoblinWorkTicks[0] = (byte)(GoblinTasks.HaulActionTicks - 1);
		Assert.Null(GoblinTasks.FindViolation(s));
		s.GoblinWorkTicks[0] = (byte)GoblinTasks.HaulActionTicks;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}
}
