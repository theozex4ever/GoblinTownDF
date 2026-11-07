using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>P3's Grog Need: the meter, drinking, which Craving wins, and the waiting carrier that gives up its unit for a drink. P4 replaced the carrier's doubled drain with Chaos (see ChaosTests).</summary>
public class GrogTests
{
	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	// The lone goblin 0 stands at (16, 16, 0) with the given meters, nothing to eat or drink anywhere and Grub and Grog well above a Craving unless given.
	static Simulation Lone(int grog, int grub = 100)
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		sim.State.GoblinGrog[0] = (byte)grog;
		sim.State.GoblinGrub[0] = (byte)grub;
		return sim;
	}

	// Goblins 0 and 1, at (16, 16) and (17, 16) unless a test moves them, with nothing to eat or drink anywhere.
	static Simulation Pair(int grog0, int grog1, int grub0 = 100, int grub1 = 100)
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		sim.State.GoblinCount = 2;
		sim.State.GoblinGrog[0] = (byte)grog0;
		sim.State.GoblinGrog[1] = (byte)grog1;
		sim.State.GoblinGrub[0] = (byte)grub0;
		sim.State.GoblinGrub[1] = (byte)grub1;
		sim.RecheckReachability();
		return sim;
	}

	static int At(SimState s, int x, int y, int z = 0) => s.TileIndex(x, y, z);

	static void Put(Simulation sim, Material material, int x, int y, int units = 1, int z = 0)
	{
		sim.State.AddItems(sim.State.TileIndex(x, y, z), material, units);
		sim.RecheckReachability();
	}

	static void Place(SimState s, int g, int x, int y)
	{
		s.GoblinX[g] = x;
		s.GoblinY[g] = y;
	}

	static void Paint(Simulation sim, int x, int y) => sim.Apply(new PaintPile(0, x, y, x, y));

	static int Total(SimState s, Material material)
	{
		var total = 0;
		for (var tile = 0; tile < s.Piles.Length; tile++)
			total += s.ItemCount(tile, material);
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.GoblinCarry[g] == (byte)material)
				total++;
		return total;
	}

	// ---- The meter

	[Fact]
	public void Every_goblin_starts_with_a_Grog_meter_of_70_to_100_drawn_from_SimRandom_after_the_Grub_meters()
	{
		var seen = new HashSet<int>();
		for (var seed = 0; seed < 300; seed++)
		{
			var s = Simulation.NewWorld(seed).State;
			var rng = new SimRandom(seed);
			for (var g = 0; g < s.GoblinCount; g++)
				rng.NextInt(31);
			for (var g = 0; g < s.GoblinCount; g++)
			{
				Assert.InRange((int)s.GoblinGrog[g], 70, 100);
				Assert.Equal(70 + rng.NextInt(31), s.GoblinGrog[g]);
				seen.Add(s.GoblinGrog[g]);
			}
		}
		Assert.Contains(70, seen);
		Assert.Contains(100, seen);
		Assert.True(seen.Count > 25, "The starting meters should be spread across 70 to 100.");
	}

	[Fact]
	public void The_same_seed_starts_every_goblin_with_the_same_Grog_meter()
	{
		Assert.Equal(Simulation.NewWorld(seed: 9).State.GoblinGrog, Simulation.NewWorld(seed: 9).State.GoblinGrog);
		Assert.NotEqual(Simulation.NewWorld(seed: 9).State.GoblinGrog, Simulation.NewWorld(seed: 10).State.GoblinGrog);
	}

	[Fact]
	public void The_Grog_meter_drains_one_point_every_35_ticks()
	{
		var sim = Lone(grog: 100);

		RunTicks(sim, 34);
		Assert.Equal(100, sim.State.GoblinGrog[0]);
		RunTicks(sim, 1);
		Assert.Equal(99, sim.State.GoblinGrog[0]);
		RunTicks(sim, 34);
		Assert.Equal(99, sim.State.GoblinGrog[0]);
		RunTicks(sim, 1);
		Assert.Equal(98, sim.State.GoblinGrog[0]);
		RunTicks(sim, 350);
		Assert.Equal(88, sim.State.GoblinGrog[0]);
	}

	[Fact]
	public void Draining_Grog_leaves_Grub_on_its_own_rate()
	{
		var sim = Lone(grog: 100, grub: 100);

		RunTicks(sim, 175);

		Assert.Equal(95, sim.State.GoblinGrog[0]);
		Assert.Equal(93, sim.State.GoblinGrub[0]);
	}

	[Fact]
	public void The_Grog_meter_stops_at_0_and_nothing_dies()
	{
		var sim = Lone(grog: 1);

		RunTicks(sim, 35 * 3);

		Assert.Equal(0, sim.State.GoblinGrog[0]);
		Assert.Equal(1, sim.State.GoblinCount);
	}

	[Fact]
	public void Grog_at_0_changes_nothing_else_the_goblin_digs_at_full_speed()
	{
		static int TicksToDig(int grog)
		{
			var sim = Lone(grog);
			sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 14, 16, 14));
			var tile = sim.State.TileIndex(16, 14, 0);
			var ticks = 0;
			while (sim.State.KindAt(tile) != TileKind.Stairs && ticks < 500)
			{
				RunTicks(sim, 1);
				ticks++;
			}
			return ticks;
		}

		Assert.Equal(23, TicksToDig(grog: 100));
		Assert.Equal(23, TicksToDig(grog: 0));
	}

	[Fact]
	public void Grog_at_0_does_not_slow_hauling_either()
	{
		var sim = Lone(grog: 0);
		Paint(sim, 17, 16);
		Put(sim, Material.Dirt, 16, 16);

		// 5 ticks to pick up (the claim shares the first), 3 to step, 5 to drop.
		RunTicks(sim, 13);

		Assert.Equal(1, sim.State.StoredCount(At(sim.State, 17, 16), Material.Dirt));
	}

	[Fact]
	public void A_goblin_with_Grog_at_0_and_no_Grog_anywhere_is_not_slowed_but_its_Chaos_rises_until_it_acts_out()
	{
		var sim = Lone(grog: 0);
		var shirks = 0;

		// Sleep is kept topped up so that the goblin's only Craving is the Grog nobody can fetch.
		for (var i = 0; i < 30; i++)
		{
			sim.State.GoblinSleep[0] = 100;
			RunTicks(sim, 100);
			shirks += sim.DrainEvents().OfType<ShirkStarted>().Count();
		}

		Assert.Equal(0, sim.State.GoblinGrog[0]);
		Assert.Equal(1, sim.State.GoblinCount);
		// 70 Chaos takes 700 ticks of one unmet Need; each shirk (100 ticks) and cooldown (300) then nets it back to 70 in 400: ticks 700, 1100 ... 2700.
		Assert.Equal(6, shirks);
	}

	// ---- Cravings interrupt

	[Fact]
	public void A_digging_goblin_below_30_Grog_with_Grog_reachable_drops_its_Job_and_drinks_for_10_ticks_adding_50_and_removing_one_unit()
	{
		var sim = Lone(grog: 100);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 14, 16, 14));
		var tile = At(s, 16, 14);
		RunTicks(sim, 13);
		Assert.Equal(GoblinTask.Digging, TaskOf(s, 0));
		var progress = s.DigProgress[tile];

		s.GoblinGrog[0] = 29;
		var underfoot = At(s, s.GoblinX[0], s.GoblinY[0]);
		Put(sim, Material.Grog, s.GoblinX[0], s.GoblinY[0], units: 3);
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Drinking, TaskOf(s, 0));
		Assert.Equal(-1, s.GoblinJobTile[0]);
		Assert.Equal(progress, s.DigProgress[tile]);
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(tile));

		// It stands on the unit, so the drink takes the 10 ticks and no more.
		RunTicks(sim, 8);
		Assert.Equal(GoblinTask.Drinking, TaskOf(s, 0));
		Assert.Equal(3, s.ItemCount(underfoot, Material.Grog));
		Assert.Equal(29, s.GoblinGrog[0]);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(2, s.ItemCount(underfoot, Material.Grog));
		Assert.Equal(79, s.GoblinGrog[0]);

		// Fed, the goblin goes back to the same tile and finishes it.
		RunTicks(sim, 200);
		Assert.Equal(TileKind.Stairs, s.KindAt(tile));
	}

	[Fact]
	public void A_goblin_at_30_Grog_does_not_crave()
	{
		var sim = Lone(grog: 30);
		Put(sim, Material.Grog, 16, 19);

		RunTicks(sim, 1);

		Assert.NotEqual(GoblinTask.SeekingGrog, TaskOf(sim.State, 0));
		Assert.Equal(1, sim.State.ItemCount(At(sim.State, 16, 19), Material.Grog));
	}

	[Fact]
	public void A_drink_from_nothing_adds_50_to_the_Grog_meter_and_leaves_Grub_alone()
	{
		var sim = Lone(grog: 0);
		Put(sim, Material.Grog, 16, 16);

		RunTicks(sim, 10);

		Assert.Equal(50, sim.State.GoblinGrog[0]);
		Assert.Equal(100, sim.State.GoblinGrub[0]);
	}

	[Fact]
	public void A_goblin_walks_to_a_unit_three_tiles_away_and_drinks_it_there_without_carrying()
	{
		var sim = Lone(grog: 29);
		var s = sim.State;
		Put(sim, Material.Grog, 16, 19);

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.SeekingGrog, TaskOf(s, 0));
		RunTicks(sim, 8);
		Assert.Equal((16, 19), (s.GoblinX[0], s.GoblinY[0]));
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Drinking, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		RunTicks(sim, 10);

		Assert.Equal(0, s.ItemCount(At(s, 16, 19), Material.Grog));
		Assert.Equal(79, s.GoblinGrog[0]);
	}

	[Fact]
	public void A_goblin_drinks_a_stored_unit_from_a_Pile_where_it_lies()
	{
		var sim = Lone(grog: 29);
		var s = sim.State;
		s.AddItems(At(s, 16, 17), Material.Grog, 4);
		Paint(sim, 16, 17);

		RunTicks(sim, 14);

		Assert.Equal(3, s.StoredCount(At(s, 16, 17), Material.Grog));
		Assert.True(s.GoblinGrog[0] >= 78);
	}

	[Fact]
	public void The_nearest_Grog_by_path_is_drunk_with_ties_to_the_lowest_tile_index()
	{
		var sim = Lone(grog: 29);
		var s = sim.State;
		Put(sim, Material.Grog, 16, 18);
		Put(sim, Material.Grog, 12, 16);
		Put(sim, Material.Grog, 20, 16);

		RunTicks(sim, 30);

		// (16, 18), (12, 16) and (20, 16) are two, four and four tiles away: the nearest goes.
		Assert.Equal(0, s.ItemCount(At(s, 16, 18), Material.Grog));
		Assert.Equal(1, s.ItemCount(At(s, 12, 16), Material.Grog));
		Assert.Equal(1, s.ItemCount(At(s, 20, 16), Material.Grog));

		var tie = Lone(grog: 29);
		Put(tie, Material.Grog, 14, 16);
		Put(tie, Material.Grog, 18, 16);
		Put(tie, Material.Grog, 16, 14);
		RunTicks(tie, 30);
		// All three are two tiles away; (16, 14) has the lowest tile index.
		Assert.Equal(0, tie.State.ItemCount(At(tie.State, 16, 14), Material.Grog));
		Assert.Equal(1, tie.State.ItemCount(At(tie.State, 14, 16), Material.Grog));
		Assert.Equal(1, tie.State.ItemCount(At(tie.State, 18, 16), Material.Grog));
	}

	[Fact]
	public void Grub_is_not_a_drink_and_Grog_is_not_a_meal()
	{
		var thirsty = Lone(grog: 29);
		Put(thirsty, Material.Grub, 16, 16);
		RunTicks(thirsty, 20);
		Assert.Equal(1, thirsty.State.ItemCount(At(thirsty.State, 16, 16), Material.Grub));
		Assert.InRange((int)thirsty.State.GoblinGrog[0], 27, 29);

		var hungry = Lone(grog: 100, grub: 29);
		Put(hungry, Material.Grog, 16, 16);
		RunTicks(hungry, 20);
		Assert.Equal(1, hungry.State.ItemCount(At(hungry.State, 16, 16), Material.Grog));
	}

	[Fact]
	public void Drinking_the_last_unit_of_a_full_Pile_makes_room_for_a_waiting_Haul()
	{
		var sim = Pair(grog0: 29, grog1: 100);
		var s = sim.State;
		var pile = At(s, 16, 16);
		s.AddItems(pile, Material.Grog, SimState.PileCapacity);
		Paint(sim, 16, 16);
		Place(s, 1, 20, 20);
		Put(sim, Material.Grog, 22, 22);
		Assert.False(s.PileHasRoom(pile, Material.Grog));
		Assert.False(s.HasStorageRoom(At(s, 22, 22), Material.Grog));

		RunTicks(sim, 10);

		Assert.Equal(SimState.PileCapacity - 1, s.StoredCount(pile, Material.Grog));
		Assert.True(s.HasStorageRoom(At(s, 22, 22), Material.Grog));
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 1));
	}

	// ---- Which Craving wins

	[Fact]
	public void With_two_Cravings_the_lowest_meter_wins_even_when_the_other_supply_is_nearer()
	{
		var sim = Lone(grog: 10, grub: 25);
		var s = sim.State;
		Put(sim, Material.Grub, 16, 17);
		Put(sim, Material.Grog, 16, 20);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.SeekingGrog, TaskOf(s, 0));

		var other = Lone(grog: 25, grub: 10);
		Put(other, Material.Grub, 16, 20);
		Put(other, Material.Grog, 16, 17);
		RunTicks(other, 1);
		Assert.Equal(GoblinTask.Seeking, TaskOf(other.State, 0));
	}

	[Fact]
	public void A_tie_between_Cravings_goes_to_Grub()
	{
		var sim = Lone(grog: 20, grub: 20);
		Put(sim, Material.Grub, 16, 20);
		Put(sim, Material.Grog, 16, 17);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(sim.State, 0));
	}

	[Fact]
	public void A_lower_meter_with_no_reachable_supply_does_not_stop_the_other_Craving_being_met()
	{
		var noGrog = Lone(grog: 5, grub: 25);
		Put(noGrog, Material.Grub, 16, 18);
		RunTicks(noGrog, 1);
		Assert.Equal(GoblinTask.Seeking, TaskOf(noGrog.State, 0));

		var noGrub = Lone(grog: 25, grub: 5);
		Put(noGrub, Material.Grog, 16, 18);
		RunTicks(noGrub, 1);
		Assert.Equal(GoblinTask.SeekingGrog, TaskOf(noGrub.State, 0));
	}

	[Fact]
	public void A_Craving_cut_off_from_its_supply_by_solid_ground_is_passed_over_for_the_other()
	{
		var sim = Lone(grog: 5, grub: 25);
		var s = sim.State;
		var islet = At(s, 25, 25, 1);
		s.TileKinds[islet] = (byte)TileKind.Floor;
		s.AddItems(islet, Material.Grog, 1);
		Put(sim, Material.Grub, 16, 18);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
	}

	[Fact]
	public void A_goblin_eating_does_not_stop_to_drink_and_considers_Grog_when_the_meal_is_done()
	{
		var sim = Lone(grog: 100, grub: 20);
		var s = sim.State;
		Put(sim, Material.Grub, 16, 16);
		Put(sim, Material.Grog, 16, 18);
		RunTicks(sim, 3);
		Assert.Equal(GoblinTask.Eating, TaskOf(s, 0));

		// Grog falls far below Grub mid-meal: the meal goes on.
		s.GoblinGrog[0] = 1;
		RunTicks(sim, 6);
		Assert.Equal(GoblinTask.Eating, TaskOf(s, 0));
		Assert.Equal(1, s.GoblinGrog[0]);
		RunTicks(sim, 1);

		Assert.Equal(70, s.GoblinGrub[0]);
		Assert.Equal(0, s.ItemCount(At(s, 16, 16), Material.Grub));
		Assert.NotEqual(GoblinTask.Eating, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.SeekingGrog, TaskOf(s, 0));
		RunTicks(sim, 30);
		Assert.Equal(0, s.ItemCount(At(s, 16, 18), Material.Grog));
		Assert.InRange((int)s.GoblinGrog[0], 50, 54);
	}

	[Fact]
	public void A_goblin_drinking_does_not_stop_to_eat_and_considers_Grub_when_the_drink_is_done()
	{
		var sim = Lone(grog: 20, grub: 100);
		var s = sim.State;
		Put(sim, Material.Grog, 16, 16);
		Put(sim, Material.Grub, 16, 18);
		RunTicks(sim, 3);
		Assert.Equal(GoblinTask.Drinking, TaskOf(s, 0));

		s.GoblinGrub[0] = 1;
		RunTicks(sim, 6);
		Assert.Equal(GoblinTask.Drinking, TaskOf(s, 0));
		RunTicks(sim, 1);

		Assert.Equal(70, s.GoblinGrog[0]);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
	}

	[Fact]
	public void A_goblin_seeking_one_Need_keeps_its_goal_when_the_other_meter_falls_lower()
	{
		var sim = Lone(grog: 100, grub: 25);
		var s = sim.State;
		Put(sim, Material.Grub, 16, 20);
		Put(sim, Material.Grog, 16, 17);
		RunTicks(sim, 2);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));

		s.GoblinGrog[0] = 3;
		RunTicks(sim, 5);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(1, s.ItemCount(At(s, 16, 17), Material.Grog));
	}

	[Fact]
	public void A_goblin_with_both_Cravings_meets_the_lower_one_and_then_the_other()
	{
		var sim = Lone(grog: 25, grub: 10);
		var s = sim.State;
		Put(sim, Material.Grub, 16, 17);
		Put(sim, Material.Grog, 16, 18);

		RunTicks(sim, 80);

		Assert.Equal(0, s.ItemCount(At(s, 16, 17), Material.Grub));
		Assert.Equal(0, s.ItemCount(At(s, 16, 18), Material.Grog));
		Assert.InRange((int)s.GoblinGrub[0], 55, 60);
		Assert.InRange((int)s.GoblinGrog[0], 70, 75);
	}

	// ---- Starving

	[Fact]
	public void A_Starving_goblin_walks_to_a_drink_at_half_speed_and_drinks_in_10_ticks()
	{
		static int TicksToDrinking(int grub)
		{
			var sim = Lone(grog: 29, grub);
			Put(sim, Material.Grog, 16, 19);
			var ticks = 0;
			while (TaskOf(sim.State, 0) != GoblinTask.Drinking && ticks < 200)
			{
				RunTicks(sim, 1);
				ticks++;
			}
			return ticks;
		}

		var fed = TicksToDrinking(grub: 100);
		var starving = TicksToDrinking(grub: 0);

		Assert.Equal(2 * fed, starving);

		var sim = Lone(grog: 29, grub: 0);
		Put(sim, Material.Grog, 16, 16);
		RunTicks(sim, 10);
		Assert.Equal(79, sim.State.GoblinGrog[0]);
	}

	// ---- The waiting carrier

	// Goblin 0 holds a unit of dirt with no Pile in the warren to take it.
	static Simulation WaitingCarrier(int grog, int grub = 100)
	{
		var sim = Lone(grog, grub);
		GoblinTasks.FinishPickUp(sim.State, 0, Material.Dirt);
		return sim;
	}

	[Fact]
	public void A_waiting_carrier_drains_Grog_at_the_ordinary_rate()
	{
		var sim = WaitingCarrier(grog: 100);
		var s = sim.State;

		RunTicks(sim, 34);
		Assert.Equal(100, s.GoblinGrog[0]);
		RunTicks(sim, 1);
		Assert.Equal(99, s.GoblinGrog[0]);
		RunTicks(sim, 35);
		Assert.Equal(98, s.GoblinGrog[0]);
		RunTicks(sim, 350);
		Assert.Equal(88, s.GoblinGrog[0]);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
	}

	[Fact]
	public void A_waiting_carrier_drains_only_Grog_and_stops_at_0()
	{
		var sim = WaitingCarrier(grog: 3, grub: 100);

		RunTicks(sim, 120);

		Assert.Equal(0, sim.State.GoblinGrog[0]);
		Assert.Equal(96, sim.State.GoblinGrub[0]);
	}

	[Fact]
	public void A_carrier_with_a_Pile_to_walk_to_drains_at_the_ordinary_rate()
	{
		var sim = WaitingCarrier(grog: 100);
		Paint(sim, 30, 16);

		RunTicks(sim, 35);

		// 14 tiles at 3 ticks each is 42 ticks: it is still walking.
		Assert.Equal(GoblinTask.Carrying, TaskOf(sim.State, 0));
		Assert.Equal(99, sim.State.GoblinGrog[0]);
	}

	[Fact]
	public void A_goblin_that_is_not_carrying_drains_at_the_ordinary_rate_whatever_it_is_doing()
	{
		var sim = Lone(grog: 100);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 14, 16, 14));
		Put(sim, Material.Dirt, 20, 16);

		RunTicks(sim, 35);

		Assert.Equal(99, s.GoblinGrog[0]);
	}

	[Fact]
	public void A_waiting_carrier_below_30_with_Grog_reachable_drops_its_unit_and_goes_to_drink()
	{
		var sim = WaitingCarrier(grog: 31);
		var s = sim.State;
		Put(sim, Material.Grog, 16, 19);
		RunTicks(sim, 34);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal(31, s.GoblinGrog[0]);
		RunTicks(sim, 35);
		Assert.Equal(30, s.GoblinGrog[0]);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));

		// The second drain takes it from 30 to 29 on the 70th tick, and the Craving acts at once.
		RunTicks(sim, 1);
		Assert.Equal(29, s.GoblinGrog[0]);
		Assert.Equal(GoblinTask.SeekingGrog, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(At(s, 16, 16), Material.Dirt));

		RunTicks(sim, 30);
		Assert.Equal(0, s.ItemCount(At(s, 16, 19), Material.Grog));
		Assert.InRange((int)s.GoblinGrog[0], 78, 79);
	}

	[Fact]
	public void A_carrier_that_is_not_waiting_keeps_its_unit_while_its_Grog_is_still_above_a_Craving()
	{
		var sim = WaitingCarrier(grog: 31);
		var s = sim.State;
		Paint(sim, 30, 16);
		Put(sim, Material.Grog, 16, 19);

		RunTicks(sim, 35);

		Assert.Equal(30, s.GoblinGrog[0]);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
	}

	[Fact]
	public void A_waiting_carrier_with_no_Grog_reachable_stays_put_holding_its_unit()
	{
		var sim = WaitingCarrier(grog: 5);
		var s = sim.State;
		var here = (s.GoblinX[0], s.GoblinY[0]);

		RunTicks(sim, 400);

		Assert.Equal(0, s.GoblinGrog[0]);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
		Assert.Equal(here, (s.GoblinX[0], s.GoblinY[0]));
		Assert.Equal(1, Total(s, Material.Dirt));
	}

	[Fact]
	public void A_waiting_carrier_with_Grog_only_cut_off_stays_put_until_the_Grog_is_reachable()
	{
		var sim = WaitingCarrier(grog: 5);
		var s = sim.State;
		var islet = At(s, 25, 25, 1);
		s.TileKinds[islet] = (byte)TileKind.Floor;
		s.AddItems(islet, Material.Grog, 1);
		sim.RecheckReachability();
		RunTicks(sim, 40);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));

		s.TileKinds[At(s, 25, 25, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[islet] = (byte)TileKind.Stairs;
		sim.RecheckReachability();
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.SeekingGrog, TaskOf(s, 0));
		Assert.Equal(1, Total(s, Material.Dirt));
	}

	// The goblin and the only other tile it can reach, with every other tile on z0 solid: two tiles, both full Piles of dirt.
	static Simulation BoxedInByFullPiles(Material craving, out int here, out int beside)
	{
		var sim = Lone(grog: craving == Material.Grog ? 20 : 100, grub: craving == Material.Grub ? 20 : 100);
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
		s.AddItems(beside, craving, 1);
		sim.RecheckReachability();
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		return sim;
	}

	[Theory]
	[InlineData(Material.Grub, GoblinTask.Seeking)]
	[InlineData(Material.Grog, GoblinTask.SeekingGrog)]
	public void A_carrier_with_no_legal_tile_to_drop_on_keeps_its_unit_and_capacity_and_recovers_when_one_opens(Material craving, GoblinTask seeking)
	{
		var sim = BoxedInByFullPiles(craving, out var here, out var beside);
		var s = sim.State;

		RunTicks(sim, 50);

		// Both tiles are full Piles of dirt: nowhere may take the unit, so it stays in hand and nothing exceeds a Pile's capacity.
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
		Assert.Equal(SimState.PileCapacity, s.ItemCount(here, Material.Dirt));
		Assert.Equal(SimState.PileCapacity, s.ItemCount(beside, Material.Dirt));
		Assert.Equal(2 * SimState.PileCapacity + 1, Total(s, Material.Dirt));

		// A tile that is no longer a Pile can take the unit: the goblin puts it down there and goes for its drink.
		sim.Apply(new RemovePile(0, 17, 16, 17, 16));
		RunTicks(sim, 1);

		Assert.Equal(seeking, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(SimState.PileCapacity + 1, s.ItemCount(beside, Material.Dirt));
		Assert.Equal(SimState.PileCapacity, s.StoredCount(here, Material.Dirt));
		Assert.Equal(2 * SimState.PileCapacity + 1, Total(s, Material.Dirt));
	}

	[Fact]
	public void A_boxed_in_carrier_recovers_when_a_unit_leaves_a_full_Pile_under_it()
	{
		var sim = BoxedInByFullPiles(Material.Grog, out var here, out _);
		var s = sim.State;
		RunTicks(sim, 20);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));

		// Some other cause frees a slot under the goblin: it is no longer waiting, and just delivers.
		s.RemoveItems(here, Material.Dirt, 1);
		sim.RecheckReachability();
		RunTicks(sim, 20);

		Assert.NotEqual(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(SimState.PileCapacity, s.StoredCount(here, Material.Dirt));
	}

	// ---- Nothing reserves a unit

	[Fact]
	public void A_goblin_can_drink_the_unit_a_hauler_was_walking_to_and_the_hauler_finds_other_work()
	{
		var sim = Pair(grog0: 100, grog1: 29);
		var s = sim.State;
		Paint(sim, 10, 16);
		Place(s, 0, 6, 16);
		Put(sim, Material.Grog, 20, 16);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 0));

		Put(sim, Material.Dirt, 6, 20);
		RunTicks(sim, 200);

		Assert.Equal(0, s.ItemCount(At(s, 20, 16), Material.Grog));
		Assert.Equal(0, s.StoredCount(At(s, 10, 16), Material.Grog));
		Assert.True(s.GoblinGrog[1] >= 70, "Goblin 1 drank the unit.");
		Assert.Equal(1, s.StoredCount(At(s, 10, 16), Material.Dirt));
	}

	[Fact]
	public void A_goblin_drinking_a_unit_a_hauler_picks_up_first_searches_again()
	{
		var sim = Pair(grog0: 100, grog1: 29);
		var s = sim.State;
		Paint(sim, 10, 16);
		Place(s, 0, 18, 16);
		Put(sim, Material.Grog, 20, 16);

		RunTicks(sim, 12);

		Assert.Equal(0, s.ItemCount(At(s, 20, 16), Material.Grog));
		Assert.Equal(29, s.GoblinGrog[1]);
		Assert.NotEqual(GoblinTask.Drinking, TaskOf(s, 1));
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Grog, s.GoblinCarry[0]);
	}

	[Fact]
	public void Two_goblins_heading_for_the_last_unit_of_Grog_one_drinks_and_the_other_searches_again()
	{
		var sim = Pair(grog0: 29, grog1: 28);
		var s = sim.State;
		Put(sim, Material.Grog, 16, 18);

		RunTicks(sim, 60);

		Assert.Equal(0, s.ItemCount(At(s, 16, 18), Material.Grog));
		Assert.InRange((int)s.GoblinGrog[0], 70, 79);
		Assert.InRange((int)s.GoblinGrog[1], 26, 28);
		Assert.NotEqual(GoblinTask.Drinking, TaskOf(s, 1));

		Put(sim, Material.Grog, 12, 16);
		RunTicks(sim, 120);
		Assert.Equal(0, s.ItemCount(At(s, 12, 16), Material.Grog));
		Assert.InRange((int)s.GoblinGrog[1], 68, 79);
	}

	[Fact]
	public void Two_goblins_drinking_the_last_unit_on_one_tile_the_first_to_finish_drinks_it()
	{
		var sim = Pair(grog0: 29, grog1: 29);
		var s = sim.State;
		Place(s, 1, 16, 16);
		Put(sim, Material.Grog, 16, 16);

		RunTicks(sim, 9);
		Assert.Equal(GoblinTask.Drinking, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Drinking, TaskOf(s, 1));
		RunTicks(sim, 1);

		Assert.Equal(79, s.GoblinGrog[0]);
		Assert.Equal(29, s.GoblinGrog[1]);
		Assert.Equal(0, s.ItemCount(At(s, 16, 16), Material.Grog));
		Assert.NotEqual(GoblinTask.Drinking, TaskOf(s, 1));
	}

	// ---- Saves and replays

	[Fact]
	public void A_save_with_a_goblin_mid_drink_round_trips_and_replays_identically()
	{
		var sim = Simulation.NewWorld(seed: 3);
		var s = sim.State;
		s.GoblinGrog[2] = 29;
		s.GoblinGrog[5] = 20;
		RunTicks(sim, 4);
		Assert.Contains(Enumerable.Range(0, s.GoblinCount), g => TaskOf(s, g) is GoblinTask.SeekingGrog or GoblinTask.Drinking);
		RunTicks(sim, 6);
		Assert.Contains(Enumerable.Range(0, s.GoblinCount), g => TaskOf(s, g) == GoblinTask.Drinking);

		var json = SimJson.Serialize(sim);
		var restored = SimJson.Deserialize(json);
		Assert.Equal(json, SimJson.Serialize(restored));

		RunTicks(sim, 400);
		RunTicks(restored, 400);

		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
	}

	[Fact]
	public void A_save_with_a_waiting_carrier_mid_wait_round_trips_and_replays_identically()
	{
		var sim = WaitingCarrier(grog: 60);
		RunTicks(sim, 50);

		var json = SimJson.Serialize(sim);
		var restored = SimJson.Deserialize(json);
		RunTicks(sim, 120);
		RunTicks(restored, 120);

		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
		Assert.Equal(sim.State.GoblinGrog[0], restored.State.GoblinGrog[0]);
		Assert.Equal(60 - 4, sim.State.GoblinGrog[0]);
	}

	[Fact]
	public void A_save_with_a_Grog_meter_above_100_is_rejected()
	{
		var sim = Simulation.NewWorld(seed: 3);
		sim.State.GoblinGrog[0] = 101;

		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Fact]
	public void A_save_with_the_wrong_number_of_Grog_meters_or_none_is_rejected()
	{
		var sim = Simulation.NewWorld(seed: 3);
		sim.State.GoblinGrog = new byte[3];
		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));

		// A save from before Grog existed has no GoblinGrog at all.
		var json = System.Text.Json.Nodes.JsonNode.Parse(SimJson.Serialize(Simulation.NewWorld(seed: 3)))!.AsObject();
		json.Remove("GoblinGrog");
		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(json.ToJsonString()));
	}

	[Fact]
	public void A_save_with_a_drinker_who_has_no_Craving_or_no_Grog_underfoot_is_rejected()
	{
		var sim = Lone(grog: 20);
		Put(sim, Material.Grog, 16, 16);
		RunTicks(sim, 3);
		Assert.Equal(GoblinTask.Drinking, TaskOf(sim.State, 0));
		var json = SimJson.Serialize(sim);
		Assert.Equal(GoblinTask.Drinking, TaskOf(SimJson.Deserialize(json).State, 0));

		sim.State.GoblinGrog[0] = 80;
		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));

		sim.State.GoblinGrog[0] = 20;
		sim.State.RemoveItems(At(sim.State, 16, 16), Material.Grog, 1);
		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	// ---- The starting warren

	[Fact]
	public void Seven_craving_Grunts_each_drink_one_unit_of_the_starting_Grog_amid_other_work()
	{
		var sim = Simulation.NewWorld(seed: 12);
		var s = sim.State;
		for (var g = 0; g < s.GoblinCount; g++)
			s.GoblinGrog[g] = 29;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 10, 14, 12));
		Paint(sim, 8, 8);

		RunTicks(sim, 250);

		Assert.Equal(7 - 7, Total(s, Material.Grog));
		for (var g = 0; g < s.GoblinCount; g++)
			Assert.InRange((int)s.GoblinGrog[g], 60, 79);
	}
}
