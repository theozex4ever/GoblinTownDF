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
	public void FinishDrop_stores_the_unit_on_the_Pile_under_the_goblin_and_clears_the_carry_fields()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		var tile = s.TileIndex(16, 16, 0);
		s.Piles[tile] = 1;
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		GoblinTasks.StartDropping(s, 0);

		GoblinTasks.FinishDrop(s, 0);

		AssertIdle(s);
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Equal(1, s.StoredCount(tile, Material.Dirt));
	}

	[Fact]
	public void FinishDrop_refuses_a_Pile_with_no_room_for_the_material_and_Release_refuses_a_goblin_holding_a_unit()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		var tile = s.TileIndex(16, 16, 0);
		s.Piles[tile] = 1;
		s.AddItems(tile, Material.Dirt, SimState.PileCapacity);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);

		Assert.Throws<InvalidOperationException>(() => GoblinTasks.FinishDrop(s, 0));
		Assert.Throws<InvalidOperationException>(() => GoblinTasks.Release(s, 0));
		Assert.Equal(SimState.PileCapacity, s.StoredCount(tile, Material.Dirt));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
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

	// ---- Seeking and Eating

	static SimState CravingWorld(out int grub)
	{
		var s = Simulation.NewWorld(seed: 7).State;
		s.GoblinGrub[0] = 20;
		grub = s.TileIndex(16, 16, 0);
		s.AddItems(grub, Material.Grub, 2);
		return s;
	}

	[Fact]
	public void BeginSeek_leaves_a_dig_and_keeps_its_progress()
	{
		var sim = WorldWithDesignation(17, 16, out var tile);
		var s = sim.State;
		s.GoblinGrub[0] = 20;
		GoblinTasks.BeginJob(s, 0, tile, digging: true);
		s.DigProgress[tile] = 7;

		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(-1, s.GoblinJobTile[0]);
		Assert.Equal(7, s.DigProgress[tile]);
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(tile));
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void BeginSeek_puts_a_carried_unit_down_on_the_tile_it_is_given_and_refuses_one_that_cannot_take_it()
	{
		var s = CravingWorld(out var grub);
		var here = s.TileIndex(16, 16, 0);
		var beside = s.TileIndex(15, 16, 0);
		s.Piles[beside] = 1;
		s.AddItems(beside, Material.Dirt, SimState.PileCapacity);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);

		Assert.Throws<InvalidOperationException>(() => GoblinTasks.BeginSeek(s, 0, beside, eating: false));
		Assert.Throws<InvalidOperationException>(() => GoblinTasks.BeginSeek(s, 0, s.Width * s.Height * s.Depth, eating: false));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
		Assert.Equal(SimState.PileCapacity, s.ItemCount(beside, Material.Dirt));

		GoblinTasks.BeginSeek(s, 0, here, eating: true);

		Assert.Equal(GoblinTask.Eating, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(here, Material.Dirt));
		Assert.Equal(grub, here);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void BeginSeek_takes_a_wandering_goblin_off_its_wander()
	{
		var s = CravingWorld(out _);
		GoblinTasks.BeginWander(s, 0, s.TileIndex(18, 18, 0));

		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(-1, s.GoblinWanderTile[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void StartEating_turns_the_walk_into_a_meal_and_FinishEating_eats_one_unit_and_adds_50()
	{
		var s = CravingWorld(out var grub);
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false);

		GoblinTasks.StartEating(s, 0);
		Assert.Equal(GoblinTask.Eating, TaskOf(s, 0));
		Assert.Null(GoblinTasks.FindViolation(s));
		for (var tick = 1; tick < NeedRules.EatTicks; tick++)
			Assert.False(GoblinTasks.EatTick(s, 0));
		Assert.Null(GoblinTasks.FindViolation(s));
		Assert.True(GoblinTasks.EatTick(s, 0));

		GoblinTasks.FinishEating(s, 0);

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(70, s.GoblinGrub[0]);
		Assert.Equal(1, s.ItemCount(grub, Material.Grub));
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FinishEating_refuses_a_tile_with_no_Grub()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		s.GoblinGrub[0] = 20;
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false);
		GoblinTasks.StartEating(s, 0);

		Assert.Throws<InvalidOperationException>(() => GoblinTasks.FinishEating(s, 0));
		Assert.Equal(20, s.GoblinGrub[0]);
	}

	[Fact]
	public void FindViolation_flags_an_eater_on_a_tile_without_Grub_and_a_seeker_or_eater_with_no_Craving()
	{
		var s = CravingWorld(out var grub);
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: true);
		Assert.Null(GoblinTasks.FindViolation(s));

		s.RemoveItems(grub, Material.Grub, 2);
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.AddItems(grub, Material.Grub, 1);
		Assert.Null(GoblinTasks.FindViolation(s));

		s.GoblinGrub[0] = (byte)NeedRules.CravingThreshold;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinGrub[0] = (byte)(NeedRules.CravingThreshold - 1);

		GoblinTasks.Release(s, 0);
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false);
		Assert.Null(GoblinTasks.FindViolation(s));
		s.GoblinGrub[0] = 80;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_flags_a_seeker_that_holds_a_job_or_a_wander_or_a_unit_and_work_ticks_on_a_seeker()
	{
		var s = CravingWorld(out _);
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false);
		Assert.Null(GoblinTasks.FindViolation(s));

		s.GoblinJobTile[0] = 5;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinJobTile[0] = -1;
		s.GoblinCarry[0] = (byte)Material.Dirt;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinCarry[0] = (byte)Material.None;
		s.GoblinWorkTicks[0] = 1;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_lets_a_meal_run_to_10_ticks_and_flags_the_11th()
	{
		var s = CravingWorld(out _);
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: true);

		s.GoblinWorkTicks[0] = (byte)(NeedRules.EatTicks - 1);
		Assert.Null(GoblinTasks.FindViolation(s));
		s.GoblinWorkTicks[0] = (byte)NeedRules.EatTicks;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	// ---- SeekingGrog and Drinking

	static SimState ThirstyWorld(out int grog)
	{
		var s = Simulation.NewWorld(seed: 7).State;
		s.GoblinGrog[0] = 20;
		grog = s.TileIndex(16, 16, 0);
		s.AddItems(grog, Material.Grog, 2);
		return s;
	}

	[Fact]
	public void BeginSeek_for_Grog_starts_SeekingGrog_or_Drinking_and_a_drink_adds_50_and_takes_one_unit()
	{
		var s = ThirstyWorld(out var grog);
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false, Material.Grog);
		Assert.Equal(GoblinTask.SeekingGrog, TaskOf(s, 0));
		Assert.Null(GoblinTasks.FindViolation(s));

		GoblinTasks.StartDrinking(s, 0);
		Assert.Equal(GoblinTask.Drinking, TaskOf(s, 0));
		for (var tick = 1; tick < NeedRules.EatTicks; tick++)
			Assert.False(GoblinTasks.EatTick(s, 0));
		Assert.Null(GoblinTasks.FindViolation(s));
		Assert.True(GoblinTasks.EatTick(s, 0));
		GoblinTasks.FinishDrinking(s, 0);

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(70, s.GoblinGrog[0]);
		Assert.Equal(1, s.ItemCount(grog, Material.Grog));
		Assert.Equal(s.GoblinGrub[0], Simulation.NewWorld(seed: 7).State.GoblinGrub[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void BeginSeek_refuses_a_goblin_already_satisfying_a_Need_and_a_supply_that_is_no_Need()
	{
		var s = ThirstyWorld(out _);
		s.GoblinGrub[0] = 20;

		Assert.Throws<ArgumentOutOfRangeException>(() => GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false, Material.Dirt));

		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false, Material.Grog);
		Assert.Throws<InvalidOperationException>(() => GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false, Material.Grub));
		GoblinTasks.StartDrinking(s, 0);
		Assert.Throws<InvalidOperationException>(() => GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false));
		Assert.Equal(GoblinTask.Drinking, TaskOf(s, 0));
	}

	[Fact]
	public void FinishDrinking_refuses_a_tile_with_no_Grog_and_Grub_underfoot_is_no_drink()
	{
		var s = Simulation.NewWorld(seed: 7).State;
		s.GoblinGrog[0] = 20;
		s.AddItems(s.TileIndex(16, 16, 0), Material.Grub, 1);
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false, Material.Grog);
		GoblinTasks.StartDrinking(s, 0);

		Assert.Throws<InvalidOperationException>(() => GoblinTasks.FinishDrinking(s, 0));
		Assert.Equal(20, s.GoblinGrog[0]);
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_judges_a_drinker_by_its_Grog_meter_and_the_Grog_underfoot_not_Grub()
	{
		var s = ThirstyWorld(out var grog);
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: true, Material.Grog);
		Assert.Null(GoblinTasks.FindViolation(s));

		// A Craving for Grub does not make a drinker's Craving.
		s.GoblinGrog[0] = (byte)NeedRules.CravingThreshold;
		s.GoblinGrub[0] = 5;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinGrog[0] = 20;
		Assert.Null(GoblinTasks.FindViolation(s));

		s.RemoveItems(grog, Material.Grog, 2);
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.AddItems(grog, Material.Grog, 1);
		Assert.Null(GoblinTasks.FindViolation(s));

		s.GoblinWorkTicks[0] = (byte)(NeedRules.EatTicks - 1);
		Assert.Null(GoblinTasks.FindViolation(s));
		s.GoblinWorkTicks[0] = (byte)NeedRules.EatTicks;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinWorkTicks[0] = 0;

		GoblinTasks.Release(s, 0);
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false, Material.Grog);
		Assert.Null(GoblinTasks.FindViolation(s));
		s.GoblinGrog[0] = 80;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void BeginSeek_for_Grog_puts_a_carried_unit_down_first()
	{
		var s = ThirstyWorld(out var here);
		GoblinTasks.FinishPickUp(s, 0, Material.Stone);

		GoblinTasks.BeginSeek(s, 0, here, eating: true, Material.Grog);

		Assert.Equal(GoblinTask.Drinking, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(here, Material.Stone));
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	// Sleep sits at 100 here, so only Grub and Grog crave; SleepTests covers the three-way tie-break.
	[Theory]
	[InlineData(10, 30, Need.Grub)]
	[InlineData(30, 10, Need.Grog)]
	[InlineData(20, 20, Need.Grub)]
	[InlineData(29, 30, Need.Grub)]
	[InlineData(30, 29, Need.Grog)]
	[InlineData(30, 30, Need.None)]
	[InlineData(100, 0, Need.Grog)]
	public void The_leading_Craving_is_the_lowest_meter_below_30_with_Grub_on_a_tie(int grub, int grog, Need expected)
	{
		Assert.Equal(expected, NeedRules.LeadingCraving(grub, grog, sleep: 100));
	}
}
