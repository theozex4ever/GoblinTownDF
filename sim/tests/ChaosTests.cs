using System.Text.Json;
using System.Text.Json.Nodes;
using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>P4's Chaos: how unmet Needs, a wait for storage and a lost Haul raise it, how a content goblin lets it fade, and shirking, the first way it acts out.</summary>
public class ChaosTests
{
	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	// Goblin 0 alone at (16, 16, 0) with the given meters, nothing to eat, drink or haul anywhere, and no urge to act out for the next 300 ticks.
	static Simulation Lone(int grub = 100, int grog = 100, int sleep = 100, int chaos = 0, bool quiet = true)
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		s.GoblinGrub[0] = (byte)grub;
		s.GoblinGrog[0] = (byte)grog;
		s.GoblinSleep[0] = (byte)sleep;
		s.GoblinChaos[0] = (byte)chaos;
		if (quiet)
			s.GoblinActCooldown[0] = ChaosRules.CooldownTicks;
		return sim;
	}

	static int At(SimState s, int x, int y, int z = 0) => s.TileIndex(x, y, z);

	static void Put(Simulation sim, Material material, int x, int y, int units = 1, int z = 0)
	{
		sim.State.AddItems(sim.State.TileIndex(x, y, z), material, units);
		sim.RecheckReachability();
	}

	static void Paint(Simulation sim, int x, int y) => sim.Apply(new PaintPile(0, x, y, x, y));

	static void Place(SimState s, int g, int x, int y)
	{
		s.GoblinX[g] = x;
		s.GoblinY[g] = y;
	}

	// ---- Starting values

	[Fact]
	public void Every_goblin_starts_calm_with_no_cooldown_whatever_the_seed()
	{
		for (var seed = 0; seed < 20; seed++)
		{
			var s = Simulation.NewWorld(seed).State;
			Assert.Equal(SimState.MaxGoblins, s.GoblinChaos.Length);
			Assert.Equal(SimState.MaxGoblins, s.GoblinActCooldown.Length);
			for (var g = 0; g < s.GoblinCount; g++)
			{
				Assert.Equal(0, s.GoblinChaos[g]);
				Assert.Equal(0, s.GoblinActCooldown[g]);
			}
		}
	}

	[Fact]
	public void The_starting_meters_do_not_move_because_Chaos_draws_nothing_from_the_Rng()
	{
		var s = Simulation.NewWorld(seed: 3).State;
		var rng = new SimRandom(3);
		for (var meters = 0; meters < 3; meters++)
			for (var g = 0; g < s.GoblinCount; g++)
				rng.NextInt(31);

		Assert.Equal(rng.NextInt(1000), s.Rng.NextInt(1000));
	}

	// ---- Unmet Needs

	[Fact]
	public void A_Need_below_30_adds_1_Chaos_on_every_10th_sim_tick_and_none_in_between_with_no_supply_anywhere()
	{
		var sim = Lone(grog: 10);
		var s = sim.State;

		RunTicks(sim, 9);
		Assert.Equal(0, s.GoblinChaos[0]);
		RunTicks(sim, 1);
		Assert.Equal(1, s.GoblinChaos[0]);
		RunTicks(sim, 9);
		Assert.Equal(1, s.GoblinChaos[0]);
		RunTicks(sim, 1);
		Assert.Equal(2, s.GoblinChaos[0]);
		RunTicks(sim, 80);
		Assert.Equal(10, s.GoblinChaos[0]);
	}

	[Fact]
	public void The_interval_is_the_sim_clock_so_a_Need_that_falls_below_30_between_intervals_waits_for_the_next_one()
	{
		var sim = Lone(grog: 100);
		var s = sim.State;
		RunTicks(sim, 14);
		s.GoblinGrog[0] = 29;

		RunTicks(sim, 5);
		Assert.Equal(0, s.GoblinChaos[0]);
		RunTicks(sim, 1);
		Assert.Equal(1, s.GoblinChaos[0]);
	}

	[Fact]
	public void A_meter_of_exactly_30_is_no_Craving_and_29_is()
	{
		var calm = Lone(grog: 30, chaos: 5);
		RunTicks(calm, 30);
		Assert.Equal(2, calm.State.GoblinChaos[0]);

		var low = Lone(grog: 29, chaos: 5);
		RunTicks(low, 30);
		Assert.Equal(8, low.State.GoblinChaos[0]);
	}

	[Fact]
	public void Each_unmet_Need_adds_its_own_point_so_they_stack()
	{
		var sim = Lone(grub: 10, grog: 10, sleep: 100);

		RunTicks(sim, 10);
		Assert.Equal(2, sim.State.GoblinChaos[0]);

		var three = Lone(grub: 10, grog: 10, sleep: 10, quiet: true);
		// Sleep is always satisfiable, so the goblin lies down at once; the two it cannot meet still add.
		RunTicks(three, 10);
		Assert.Equal(2, three.State.GoblinChaos[0]);
	}

	[Fact]
	public void A_Need_adds_Chaos_while_its_supply_is_reachable_and_the_goblin_is_still_walking_to_it()
	{
		var sim = Lone(grog: 10);
		Put(sim, Material.Grog, 16, 26);
		var s = sim.State;

		RunTicks(sim, 20);

		Assert.Equal(GoblinTask.SeekingGrog, TaskOf(s, 0));
		Assert.Equal(2, s.GoblinChaos[0]);
	}

	[Fact]
	public void The_Need_being_restored_by_eating_or_drinking_adds_nothing_but_any_other_low_Need_still_does()
	{
		var sim = Lone(grub: 20, grog: 5, chaos: 10);
		Put(sim, Material.Grog, 16, 16);
		var s = sim.State;

		RunTicks(sim, 9);
		Assert.Equal(GoblinTask.Drinking, TaskOf(s, 0));
		RunTicks(sim, 1);

		// The drink ends on this tick, after the interval has run: Grog added nothing, Grub (nothing to eat) added 1.
		Assert.Equal(11, s.GoblinChaos[0]);
		Assert.Equal(55, s.GoblinGrog[0]);
	}

	[Fact]
	public void A_goblin_restoring_a_Need_neither_gains_from_it_nor_recovers_while_it_is_still_below_30()
	{
		var sim = Lone(grog: 5, chaos: 10);
		Put(sim, Material.Grog, 16, 16);

		RunTicks(sim, 10);

		Assert.Equal(10, sim.State.GoblinChaos[0]);
	}

	[Fact]
	public void The_Need_being_restored_by_sleeping_adds_nothing_but_other_low_Needs_still_add_during_sleep()
	{
		var sim = Lone(grub: 10, grog: 10, sleep: 5);
		var s = sim.State;

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		RunTicks(sim, 9);
		Assert.Equal(2, s.GoblinChaos[0]);
		RunTicks(sim, 10);
		Assert.Equal(4, s.GoblinChaos[0]);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
	}

	[Fact]
	public void A_goblin_asleep_with_nothing_else_low_gains_nothing()
	{
		var sim = Lone(sleep: 5, chaos: 0);

		RunTicks(sim, 100);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(sim.State, 0));
		Assert.Equal(0, sim.State.GoblinChaos[0]);
	}

	[Fact]
	public void A_Starving_goblin_gains_at_the_sim_rate_not_half_of_it()
	{
		var sim = Lone(grub: 0);

		RunTicks(sim, 100);

		Assert.Equal(10, sim.State.GoblinChaos[0]);
	}

	[Fact]
	public void Zero_Grog_now_has_a_consequence_where_P3_had_none()
	{
		var sim = Lone(grog: 0);

		RunTicks(sim, 50);

		Assert.Equal(5, sim.State.GoblinChaos[0]);
	}

	// ---- Range and recovery

	[Fact]
	public void Chaos_is_held_within_0_and_100()
	{
		var high = Lone(grub: 10, grog: 10, chaos: 99);
		RunTicks(high, 30);
		Assert.Equal(100, high.State.GoblinChaos[0]);

		var low = Lone(chaos: 0);
		RunTicks(low, 100);
		Assert.Equal(0, low.State.GoblinChaos[0]);
	}

	[Fact]
	public void A_content_goblin_loses_1_Chaos_on_every_10th_sim_tick()
	{
		var sim = Lone(chaos: 30);
		var s = sim.State;

		RunTicks(sim, 9);
		Assert.Equal(30, s.GoblinChaos[0]);
		RunTicks(sim, 1);
		Assert.Equal(29, s.GoblinChaos[0]);
		RunTicks(sim, 100);
		Assert.Equal(19, s.GoblinChaos[0]);
	}

	[Fact]
	public void A_meal_does_not_clear_the_Chaos_at_once_it_only_lets_it_fade()
	{
		var sim = Lone(grub: 20, chaos: 50);
		Put(sim, Material.Grub, 16, 16);
		var s = sim.State;

		RunTicks(sim, 10);
		Assert.Equal(70, s.GoblinGrub[0]);
		Assert.Equal(50, s.GoblinChaos[0]);

		RunTicks(sim, 10);
		Assert.Equal(49, s.GoblinChaos[0]);
	}

	[Fact]
	public void Recovery_and_gain_do_not_both_happen_on_one_interval()
	{
		var sim = Lone(grog: 10, chaos: 20);

		RunTicks(sim, 10);

		Assert.Equal(21, sim.State.GoblinChaos[0]);
	}

	// ---- Waiting carriers

	// Goblin 0 holds a unit of dirt with no Pile in the warren to take it.
	static Simulation WaitingCarrier(int chaos = 0, int grog = 100)
	{
		var sim = Lone(grog: grog, chaos: chaos);
		GoblinTasks.FinishPickUp(sim.State, 0, Material.Dirt);
		return sim;
	}

	[Fact]
	public void A_waiting_carrier_gains_1_Chaos_on_every_20th_sim_tick_and_drains_Grog_at_the_ordinary_rate()
	{
		var sim = WaitingCarrier();
		var s = sim.State;

		RunTicks(sim, 19);
		Assert.Equal(0, s.GoblinChaos[0]);
		RunTicks(sim, 1);
		Assert.Equal(1, s.GoblinChaos[0]);
		RunTicks(sim, 180);
		Assert.Equal(10, s.GoblinChaos[0]);

		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.True(NeedsSystem.IsWaitingCarrier(s, 0));
		// 200 ticks is five drains of 1 point; the P3 doubled drain would have lost ten.
		Assert.Equal(95, s.GoblinGrog[0]);
	}

	[Fact]
	public void A_waiting_carrier_does_not_recover_even_with_every_Need_met()
	{
		var sim = WaitingCarrier(chaos: 10);

		RunTicks(sim, 10);
		Assert.Equal(10, sim.State.GoblinChaos[0]);
		RunTicks(sim, 10);
		Assert.Equal(11, sim.State.GoblinChaos[0]);
	}

	[Fact]
	public void Waiting_and_an_unmet_Need_are_separate_pressures_that_both_count()
	{
		var sim = WaitingCarrier(grog: 10);

		// Tick 10: the unmet Grog adds 1. Tick 20: the unmet Grog adds 1 and the wait adds 1.
		RunTicks(sim, 10);
		Assert.Equal(1, sim.State.GoblinChaos[0]);
		RunTicks(sim, 10);
		Assert.Equal(3, sim.State.GoblinChaos[0]);
	}

	[Fact]
	public void A_waiting_carrier_with_nothing_to_drink_stays_put_until_its_Chaos_makes_it_give_up_the_unit_and_shirk()
	{
		var sim = WaitingCarrier(grog: 5);
		var s = sim.State;
		s.GoblinActCooldown[0] = 0;

		// Two unmet-Need points and one waiting point every 20 ticks: 70 Chaos by tick 467.
		RunTicks(sim, 460);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
		RunTicks(sim, 10);

		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(At(s, 16, 16), Material.Dirt));
	}

	[Fact]
	public void A_carrier_with_a_Pile_to_walk_to_is_not_waiting_and_recovers_instead()
	{
		var sim = WaitingCarrier(chaos: 10);
		Paint(sim, 30, 16);

		RunTicks(sim, 20);

		Assert.False(NeedsSystem.IsWaitingCarrier(sim.State, 0));
		Assert.Equal(8, sim.State.GoblinChaos[0]);
	}

	[Fact]
	public void A_goblin_that_stops_waiting_when_a_Pile_appears_recovers_from_then_on()
	{
		var sim = WaitingCarrier(chaos: 10);
		var s = sim.State;
		RunTicks(sim, 20);
		Assert.Equal(11, s.GoblinChaos[0]);

		Paint(sim, 17, 16);
		RunTicks(sim, 20);

		Assert.Equal(1, s.StoredCount(At(s, 17, 16), Material.Dirt));
		Assert.Equal(9, s.GoblinChaos[0]);
	}

	// ---- A Haul lost to a meal

	// Goblins 0, 1 and 2: 0 and 1 are idle and haul to the Pile at (20, 16); 2 stands on (16, 18) and eats the Grub there at once.
	static Simulation Trio(params (Material Material, int Units)[] onTheTile)
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		var s = sim.State;
		s.GoblinCount = 3;
		Place(s, 2, 16, 18);
		for (var g = 0; g < 3; g++)
			(s.GoblinGrub[g], s.GoblinGrog[g], s.GoblinSleep[g]) = (100, 100, 100);
		s.GoblinGrub[2] = 10;
		foreach (var (material, units) in onTheTile)
			s.AddItems(At(s, 16, 18), material, units);
		sim.RecheckReachability();
		Paint(sim, 20, 16);
		return sim;
	}

	[Fact]
	public void A_hauler_whose_claim_is_cancelled_by_another_goblin_eating_gains_15_Chaos_once_and_the_sim_says_so()
	{
		var sim = Trio((Material.Grub, 1));
		var s = sim.State;
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Eating, TaskOf(s, 2));
		Assert.True(TaskOf(s, 0) is GoblinTask.Fetching);
		sim.DrainEvents();

		RunTicks(sim, 9);

		Assert.Equal(15, s.GoblinChaos[0]);
		Assert.Equal(0, s.GoblinChaos[1]);
		Assert.Equal(0, s.GoblinChaos[2]);
		Assert.Contains(new HaulCancelled(0), sim.DrainEvents());

		// Once: nothing more is added by the claim being gone, and the next interval is ordinary recovery.
		RunTicks(sim, 9);
		Assert.Equal(15, s.GoblinChaos[0]);
		RunTicks(sim, 1);
		Assert.Equal(14, s.GoblinChaos[0]);
	}

	[Fact]
	public void On_a_mixed_tile_the_consumed_unit_displaces_the_highest_numbered_claimant_whatever_material_it_meant_to_haul()
	{
		var sim = Trio((Material.Dirt, 1), (Material.Grub, 1));
		var s = sim.State;
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 1));
		sim.DrainEvents();

		RunTicks(sim, 9);

		// The tile holds one unit left and goblin 0 has the first claim on it; goblin 1 is the one displaced.
		Assert.Equal(0, s.GoblinChaos[0]);
		Assert.Equal(15, s.GoblinChaos[1]);
		Assert.Equal(1, s.ItemCount(At(s, 16, 18), Material.Dirt));
		Assert.Equal(0, s.ItemCount(At(s, 16, 18), Material.Grub));
		Assert.Equal([new HaulCancelled(1)], sim.DrainEvents().OfType<HaulCancelled>());
	}

	[Fact]
	public void A_consumed_unit_that_leaves_every_claim_a_unit_of_its_own_annoys_nobody()
	{
		var sim = Trio((Material.Dirt, 2), (Material.Grub, 1));
		var s = sim.State;

		RunTicks(sim, 10);

		Assert.Equal(0, s.GoblinChaos[0]);
		Assert.Equal(0, s.GoblinChaos[1]);
		Assert.Empty(sim.DrainEvents().OfType<HaulCancelled>());
	}

	[Fact]
	public void Drinking_cancels_a_Haul_the_same_way()
	{
		var sim = Trio((Material.Grog, 1));
		var s = sim.State;
		(s.GoblinGrub[2], s.GoblinGrog[2]) = (100, 10);

		RunTicks(sim, 10);

		Assert.Equal(15, s.GoblinChaos[0]);
	}

	[Fact]
	public void Painting_a_Pile_over_a_claimed_unit_cancels_the_Haul_with_no_annoyance()
	{
		var sim = Trio((Material.Dirt, 1));
		var s = sim.State;
		s.GoblinGrub[2] = 100;
		RunTicks(sim, 2);
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 0));
		sim.DrainEvents();

		Paint(sim, 16, 18);

		Assert.NotEqual(GoblinTask.Fetching, TaskOf(s, 0));
		Assert.Equal(0, s.GoblinChaos[0]);
		Assert.Empty(sim.DrainEvents().OfType<HaulCancelled>());
		RunTicks(sim, 40);
		Assert.Equal(0, s.GoblinChaos[0]);
	}

	[Fact]
	public void Another_hauler_collecting_a_unit_annoys_nobody()
	{
		var sim = Trio((Material.Dirt, 2));
		var s = sim.State;
		s.GoblinGrub[2] = 100;

		RunTicks(sim, 40);

		Assert.Equal(0, s.GoblinChaos[0]);
		Assert.Equal(0, s.GoblinChaos[1]);
		Assert.Empty(sim.DrainEvents().OfType<HaulCancelled>());
	}

	// ---- Shirking: starting

	[Fact]
	public void A_goblin_at_70_Chaos_begins_to_shirk_and_one_at_69_does_not()
	{
		var shirker = Lone(chaos: 70, quiet: false);
		RunTicks(shirker, 1);
		Assert.Equal(GoblinTask.Shirking, TaskOf(shirker.State, 0));
		Assert.Contains(new ShirkStarted(0), shirker.DrainEvents());

		var calm = Lone(chaos: 69, quiet: false);
		RunTicks(calm, 9);
		Assert.NotEqual(GoblinTask.Shirking, TaskOf(calm.State, 0));
		Assert.Empty(calm.DrainEvents().OfType<ShirkStarted>());
	}

	[Fact]
	public void A_shirker_loafs_on_the_spot_for_100_ticks_refusing_every_Job_then_is_free()
	{
		var sim = Lone(chaos: 70, quiet: false);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 14, 16, 14));
		Put(sim, Material.Dirt, 20, 20);
		Paint(sim, 30, 16);

		RunTicks(sim, 99);
		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal((16, 16), (s.GoblinX[0], s.GoblinY[0]));
		Assert.Equal(-1, s.GoblinJobTile[0]);
		Assert.Equal(0, s.DigProgress[At(s, 16, 14)]);

		RunTicks(sim, 1);
		Assert.NotEqual(GoblinTask.Shirking, TaskOf(s, 0));
		// Ten content intervals took 10 off the 70 before the 40 of relief: 70 - 10 - 40.
		Assert.Equal(20, s.GoblinChaos[0]);
	}

	[Fact]
	public void A_shirker_does_not_wander_even_with_nothing_to_do()
	{
		var sim = Lone(chaos: 100, quiet: false);
		var s = sim.State;

		for (var i = 0; i < 99; i++)
		{
			RunTicks(sim, 1);
			Assert.Equal((16, 16), (s.GoblinX[0], s.GoblinY[0]));
		}
	}

	[Fact]
	public void Shirking_takes_100_ticks_of_sim_time_even_when_the_goblin_is_Starving()
	{
		var sim = Lone(grub: 0, chaos: 100, quiet: false);
		var s = sim.State;

		RunTicks(sim, 99);
		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.NotEqual(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[0]);
	}

	[Fact]
	public void Starting_to_shirk_draws_nothing_from_the_Rng_while_shirking_is_the_only_act()
	{
		var sim = Lone(chaos: 100, quiet: false);
		var untouched = SimJson.Deserialize(SimJson.Serialize(sim));

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(sim.State, 0));
		Assert.Equal(untouched.State.Rng.NextInt(1000), sim.State.Rng.NextInt(1000));
	}

	[Fact]
	public void A_satisfiable_Craving_comes_before_acting_out()
	{
		var sim = Lone(grub: 20, chaos: 100, quiet: false);
		Put(sim, Material.Grub, 16, 20);
		var s = sim.State;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(0, s.GoblinActCooldown[0]);
		Assert.Empty(sim.DrainEvents().OfType<ShirkStarted>());
	}

	[Fact]
	public void A_Craving_with_no_supply_in_reach_does_not_stop_a_goblin_acting_out()
	{
		var sim = Lone(grub: 20, chaos: 100, quiet: false);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(sim.State, 0));
	}

	[Theory]
	[InlineData(GoblinTask.Seeking)]
	[InlineData(GoblinTask.Eating)]
	public void Chaos_does_not_divert_a_goblin_already_seeking_or_eating(GoblinTask task)
	{
		var sim = Lone(grub: 20, chaos: 100, quiet: false);
		Put(sim, Material.Grub, 16, task == GoblinTask.Eating ? 16 : 25);
		var s = sim.State;
		GoblinTasks.BeginSeek(s, 0, -1, eating: task == GoblinTask.Eating);

		RunTicks(sim, 5);

		Assert.Equal(task, TaskOf(s, 0));
	}

	[Fact]
	public void Protected_sleep_is_not_interrupted_by_Chaos_and_the_goblin_acts_out_once_awake()
	{
		var sim = Lone(sleep: 95, chaos: 100, quiet: false);
		var s = sim.State;
		GoblinTasks.BeginSleep(s, 0, -1, asleep: true);

		RunTicks(sim, 40);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Empty(sim.DrainEvents().OfType<ShirkStarted>());

		RunTicks(sim, 20);
		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
	}

	[Fact]
	public void A_goblin_in_its_cooldown_does_not_act_out_however_high_its_Chaos()
	{
		var sim = Lone(chaos: 100, quiet: true);

		RunTicks(sim, 299);

		Assert.NotEqual(GoblinTask.Shirking, TaskOf(sim.State, 0));
		Assert.Equal(1, sim.State.GoblinActCooldown[0]);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Shirking, TaskOf(sim.State, 0));
	}

	// ---- Shirking: what it abandons

	[Fact]
	public void A_digger_who_shirks_keeps_the_dig_progress_and_finishes_the_tile_faster_afterwards()
	{
		var sim = Lone(quiet: false);
		var s = sim.State;
		var tile = At(s, 16, 14);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 14, 16, 14));
		RunTicks(sim, 10);
		var progress = s.DigProgress[tile];
		Assert.InRange((int)progress, 5, 15);
		Assert.Equal(GoblinTask.Digging, TaskOf(s, 0));

		s.GoblinChaos[0] = 100;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal(progress, s.DigProgress[tile]);
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(tile));
		Assert.Equal(-1, s.GoblinJobTile[0]);

		RunTicks(sim, 99);
		Assert.NotEqual(GoblinTask.Shirking, TaskOf(s, 0));
		// A fresh dig takes 20 ticks; the 13 or so that remain take fewer than that, plus the tick it takes to claim it again.
		RunTicks(sim, 20 - progress + 2);
		Assert.Equal(DesignationKind.None, s.DesignationAt(tile));
		Assert.Equal(TileKind.Stairs, s.KindAt(tile));
	}

	[Fact]
	public void A_tender_who_shirks_loses_the_Tend_progress()
	{
		var sim = Lone(quiet: false);
		var s = sim.State;
		var pit = At(s, 16, 16);
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 16, 16, 16, 16));
		RunTicks(sim, 10);
		Assert.Equal(GoblinTask.Tending, TaskOf(s, 0));
		Assert.Equal(10, s.GoblinWorkTicks[0]);
		Assert.Equal(0, s.FarmUnspawned[pit]);

		s.GoblinChaos[0] = 100;
		RunTicks(sim, 100);
		Assert.NotEqual(GoblinTask.Shirking, TaskOf(s, 0));

		// The Tend starts again from nothing and takes its full 20 ticks.
		RunTicks(sim, 19);
		Assert.Equal(0, s.FarmUnspawned[pit]);
		RunTicks(sim, 1);
		Assert.True(s.FarmUnspawned[pit] > 0);
	}

	[Fact]
	public void A_hauler_who_shirks_releases_its_claim_so_another_goblin_can_take_the_Haul()
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		sim.State.GoblinCount = 2;
		var s = sim.State;
		Place(s, 1, 25, 25);
		for (var g = 0; g < 2; g++)
			(s.GoblinGrub[g], s.GoblinGrog[g], s.GoblinSleep[g]) = (100, 100, 100);
		sim.RecheckReachability();
		Put(sim, Material.Dirt, 16, 20);
		Paint(sim, 20, 16);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 0));

		s.GoblinChaos[0] = 100;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal(-1, s.GoblinJobTile[0]);

		RunTicks(sim, 2);
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 1));
		Assert.Equal(At(s, 16, 20), s.GoblinJobTile[1]);
	}

	[Fact]
	public void A_brewer_who_shirks_puts_the_Fungus_down_on_its_own_tile_and_loses_the_brew()
	{
		var sim = Lone(quiet: false);
		var s = sim.State;
		var brewery = At(s, 16, 16);
		sim.Apply(new PaintRoom(RoomKind.Brewery, 0, 16, 16, 16, 16));
		Paint(sim, 30, 16);
		GoblinTasks.FinishTake(s, 0, brewery, brewing: true);
		RunTicks(sim, 5);
		Assert.Equal(GoblinTask.Brewing, TaskOf(s, 0));

		s.GoblinChaos[0] = 100;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(brewery, Material.Fungus));
		Assert.Equal(0, s.ItemCount(brewery, Material.Grog));
		Assert.False(GoblinTasks.IsBreweryClaimed(s, brewery));
	}

	[Fact]
	public void A_carrier_who_shirks_puts_its_unit_down_loose_on_its_own_tile()
	{
		var sim = WaitingCarrier(chaos: 100);
		var s = sim.State;
		s.GoblinActCooldown[0] = 0;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(At(s, 16, 16), Material.Dirt));
		Assert.False(NeedsSystem.IsWaitingCarrier(s, 0));
	}

	[Fact]
	public void A_carrier_standing_on_a_Pile_with_room_stores_its_unit_there_when_it_shirks()
	{
		var sim = WaitingCarrier(chaos: 100);
		var s = sim.State;
		s.GoblinActCooldown[0] = 0;
		Paint(sim, 16, 16);
		s.AddItems(At(s, 16, 16), Material.Dirt, SimState.PileCapacity - 1);
		sim.RecheckReachability();

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal(SimState.PileCapacity, s.ItemCount(At(s, 16, 16), Material.Dirt));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
	}

	// The goblin and the only other tile it can reach, with every other tile on z0 solid: two tiles, both full Piles of dirt, and a unit of dirt in hand.
	static Simulation BoxedInByFullPiles(out int here, out int beside)
	{
		var sim = Lone(chaos: 100, quiet: false);
		var s = sim.State;
		here = At(s, 16, 16);
		beside = At(s, 17, 16);
		for (var tile = 0; tile < s.Width * s.Height; tile++)
			if (tile != here && tile != beside)
				s.TileKinds[tile] = (byte)TileKind.Solid;
		foreach (var tile in new[] { here, beside })
		{
			s.AddItems(tile, Material.Dirt, SimState.PileCapacity);
			s.Piles[tile] = 1;
		}
		sim.RecheckReachability();
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		return sim;
	}

	[Fact]
	public void A_carrier_with_no_legal_place_to_put_its_unit_keeps_it_and_defers_acting_out_until_one_opens()
	{
		var sim = BoxedInByFullPiles(out var here, out var beside);
		var s = sim.State;

		RunTicks(sim, 50);

		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
		Assert.Equal(SimState.PileCapacity, s.ItemCount(here, Material.Dirt));
		Assert.Equal(SimState.PileCapacity, s.ItemCount(beside, Material.Dirt));
		Assert.Equal(0, s.GoblinActCooldown[0]);
		Assert.Empty(sim.DrainEvents().OfType<ShirkStarted>());

		sim.Apply(new RemovePile(0, 17, 16, 17, 16));
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(SimState.PileCapacity + 1, s.ItemCount(beside, Material.Dirt));
		Assert.Equal(SimState.PileCapacity, s.ItemCount(here, Material.Dirt));
		Assert.Single(sim.DrainEvents().OfType<ShirkStarted>());
	}

	// ---- Shirking: ending

	[Fact]
	public void Finishing_the_100_ticks_removes_40_Chaos_and_starts_a_300_tick_cooldown_that_ends_exactly_300_ticks_later()
	{
		var sim = Lone(chaos: 100, quiet: false);
		var s = sim.State;

		RunTicks(sim, 100);

		Assert.NotEqual(GoblinTask.Shirking, TaskOf(s, 0));
		// Ten content intervals fell on the way: 100 - 10 - 40.
		Assert.Equal(50, s.GoblinChaos[0]);
		Assert.Equal(300, s.GoblinActCooldown[0]);

		s.GoblinChaos[0] = 100;
		RunTicks(sim, 299);
		Assert.NotEqual(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal(1, s.GoblinActCooldown[0]);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
	}

	[Fact]
	public void Chaos_keeps_accumulating_through_the_cooldown()
	{
		var sim = Lone(chaos: 100, quiet: false);
		var s = sim.State;
		RunTicks(sim, 100);
		s.GoblinGrog[0] = 5;
		var before = s.GoblinChaos[0];

		RunTicks(sim, 100);

		Assert.Equal(before + 10, s.GoblinChaos[0]);
	}

	[Fact]
	public void A_satisfiable_Craving_interrupts_shirking_for_no_relief_and_starts_the_cooldown_and_the_act_never_resumes()
	{
		var sim = Lone(chaos: 100, quiet: false);
		var s = sim.State;
		RunTicks(sim, 31);
		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		Put(sim, Material.Grub, 16, 18);
		s.GoblinGrub[0] = 29;
		var chaos = s.GoblinChaos[0];

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(300, s.GoblinActCooldown[0]);
		Assert.Equal(chaos, s.GoblinChaos[0]);

		RunTicks(sim, 60);
		Assert.NotEqual(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.True(s.GoblinGrub[0] > 70, $"Grub is {s.GoblinGrub[0]}: the meal was not eaten.");
	}

	[Fact]
	public void An_unsatisfiable_Craving_does_not_interrupt_shirking()
	{
		var sim = Lone(chaos: 100, quiet: false);
		var s = sim.State;
		RunTicks(sim, 30);
		s.GoblinGrub[0] = 10;

		RunTicks(sim, 30);

		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
	}

	[Fact]
	public void A_Craving_for_Sleep_is_always_satisfiable_so_it_interrupts_shirking()
	{
		var sim = Lone(chaos: 100, quiet: false);
		var s = sim.State;
		RunTicks(sim, 30);
		s.GoblinSleep[0] = 29;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(300, s.GoblinActCooldown[0]);
	}

	[Fact]
	public void Collapse_interrupts_shirking_with_no_relief()
	{
		var sim = Lone(chaos: 100, quiet: false);
		var s = sim.State;
		RunTicks(sim, 31);
		s.GoblinSleep[0] = 0;
		var chaos = s.GoblinChaos[0];

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(300, s.GoblinActCooldown[0]);
		Assert.Equal(chaos, s.GoblinChaos[0]);
	}

	[Fact]
	public void Collapse_comes_before_acting_out_for_a_goblin_that_could_do_both()
	{
		var sim = Lone(sleep: 0, chaos: 100, quiet: false);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(sim.State, 0));
		Assert.Empty(sim.DrainEvents().OfType<ShirkStarted>());
	}

	// ---- Saves and replay

	[Fact]
	public void A_save_taken_mid_shirk_continues_identically()
	{
		var sim = Lone(chaos: 100, quiet: false);
		RunTicks(sim, 40);
		Assert.Equal(GoblinTask.Shirking, TaskOf(sim.State, 0));

		var json = SimJson.Serialize(sim);
		var restored = SimJson.Deserialize(json);
		Assert.Equal(json, SimJson.Serialize(restored));

		RunTicks(sim, 400);
		RunTicks(restored, 400);

		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
		Assert.NotEqual(GoblinTask.Shirking, TaskOf(restored.State, 0));
	}

	[Fact]
	public void A_save_taken_mid_cooldown_keeps_the_cooldown()
	{
		var sim = Lone(chaos: 100, quiet: false);
		RunTicks(sim, 150);

		var restored = SimJson.Deserialize(SimJson.Serialize(sim));

		Assert.Equal(sim.State.GoblinActCooldown[0], restored.State.GoblinActCooldown[0]);
		Assert.Equal(250, restored.State.GoblinActCooldown[0]);
	}

	[Fact]
	public void Two_runs_of_one_seed_with_one_command_log_agree_to_the_last_byte_and_Chaos_reaches_shirking()
	{
		static Simulation Run(int seed)
		{
			var sim = Simulation.NewWorld(seed);
			sim.Apply(new PaintPile(0, 20, 14, 21, 15));
			sim.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 10, 11, 11));
			// Nothing to eat or drink for the whole warren, so every Need that falls below 30 stays unmet.
			Array.Clear(sim.State.TileItems);
			sim.RecheckReachability();
			for (var i = 0; i < 6000; i++)
				sim.Advance(0.1);
			return sim;
		}

		var a = Run(5);
		var b = Run(5);

		Assert.Equal(SimJson.Serialize(a), SimJson.Serialize(b));
		Assert.NotEqual(SimJson.Serialize(a), SimJson.Serialize(Run(6)));
		Assert.Contains(Enumerable.Range(0, a.State.GoblinCount), g => a.State.GoblinChaos[g] > 0);
	}

	[Fact]
	public void A_world_that_has_shirked_replays_the_same_from_a_mid_run_save()
	{
		var sim = Simulation.NewWorld(seed: 9);
		Array.Clear(sim.State.TileItems);
		sim.RecheckReachability();
		for (var g = 0; g < sim.State.GoblinCount; g++)
			sim.State.GoblinChaos[g] = (byte)(60 + g * 5);
		var shirks = 0;
		for (var i = 0; i < 1000; i++)
		{
			sim.Advance(0.1);
			shirks += sim.DrainEvents().OfType<ShirkStarted>().Count();
		}
		Assert.True(shirks > 0, "No goblin shirked, so the run never reached the behaviour it replays.");

		var restored = SimJson.Deserialize(SimJson.Serialize(sim));
		for (var i = 0; i < 1500; i++)
		{
			sim.Advance(0.1);
			restored.Advance(0.1);
		}

		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
	}

	// A save written mid-shirk by goblin 0, after the given change to the state.
	static string SaveMidShirk(Action<SimState>? change = null)
	{
		var sim = Lone(chaos: 100, quiet: false);
		RunTicks(sim, 5);
		change?.Invoke(sim.State);
		return SimJson.Serialize(sim);
	}

	static string WithoutProperty(string name)
	{
		var root = JsonNode.Parse(SaveMidShirk())!.AsObject();
		root.Remove(name);
		return root.ToJsonString();
	}

	// Goblin 0 as it is just after a shirk: idle, with a cooldown.
	static void EndShirk(SimState s, int cooldown)
	{
		s.GoblinTask[0] = (byte)GoblinTask.Idle;
		s.GoblinWorkTicks[0] = 0;
		s.GoblinActCooldown[0] = (ushort)cooldown;
	}

	[Fact]
	public void A_save_with_Chaos_above_100_or_a_cooldown_above_300_is_rejected()
	{
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SaveMidShirk(s => s.GoblinChaos[0] = 101)));
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SaveMidShirk(s => EndShirk(s, cooldown: 301))));
		SimJson.Deserialize(SaveMidShirk(s => s.GoblinChaos[0] = 100));
		SimJson.Deserialize(SaveMidShirk(s => EndShirk(s, cooldown: 300)));
	}

	[Fact]
	public void A_save_with_the_wrong_number_of_Chaos_values_or_none_is_rejected()
	{
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SaveMidShirk(s => s.GoblinChaos = new byte[3])));
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SaveMidShirk(s => s.GoblinActCooldown = new ushort[3])));
		// A save from before Chaos existed has neither array.
		Assert.Throws<JsonException>(() => SimJson.Deserialize(WithoutProperty("GoblinChaos")));
		Assert.Throws<JsonException>(() => SimJson.Deserialize(WithoutProperty("GoblinActCooldown")));
	}

	[Fact]
	public void A_save_with_a_shirker_that_breaks_the_rules_of_shirking_is_rejected()
	{
		// Mid-shirk, goblin 0 holds no Job, carries nothing, has counted fewer than 100 ticks and has no cooldown.
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SaveMidShirk(s => s.GoblinActCooldown[0] = 5)));
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SaveMidShirk(s => s.GoblinWorkTicks[0] = 100)));
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SaveMidShirk(s => s.GoblinJobTile[0] = 5)));
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SaveMidShirk(s => s.GoblinCarry[0] = (byte)Material.Dirt)));
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SaveMidShirk(s => s.GoblinWanderTile[0] = 5)));
		SimJson.Deserialize(SaveMidShirk(s => s.GoblinWorkTicks[0] = 99));
	}
}
