using System.Text.Json;
using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>
/// P3's Sleep Need: the meter, a Craving for Sleep and which Craving wins, sleeping on a Sleeping Heap or the floor, and Collapse.
/// Goblin 0 of a lone world stands at (16, 16, 0); nothing lies on the ground unless a test puts it there.
/// </summary>
public class SleepTests
{
	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	static int At(SimState s, int x, int y, int z = 0) => s.TileIndex(x, y, z);

	// Goblin 0 alone, with the given meters; Grub and Grog sit well above a Craving unless a test lowers them.
	static Simulation Lone(int sleep, int grub = 100, int grog = 100)
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		sim.State.GoblinSleep[0] = (byte)sleep;
		sim.State.GoblinGrub[0] = (byte)grub;
		sim.State.GoblinGrog[0] = (byte)grog;
		return sim;
	}

	// The first `count` Grunts of the cluster, every meter at 100 except Sleep, which is `sleep`.
	static Simulation Crew(int count, int sleep)
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		var s = sim.State;
		s.GoblinCount = count;
		for (var g = 0; g < count; g++)
			(s.GoblinSleep[g], s.GoblinGrub[g], s.GoblinGrog[g]) = ((byte)sleep, 100, 100);
		sim.RecheckReachability();
		return sim;
	}

	static void Heap(Simulation sim, int x, int y) => sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 0, x, y, x, y));

	static void Put(Simulation sim, Material material, int x, int y, int units = 1)
	{
		sim.State.AddItems(sim.State.TileIndex(x, y, 0), material, units);
		sim.RecheckReachability();
	}

	// Makes a rectangle of the surface Solid, as a wall.
	static void Wall(Simulation sim, int x0, int y0, int x1, int y1)
	{
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
			sim.State.TileKinds[sim.State.TileIndex(x, y, 0)] = (byte)TileKind.Solid;
		sim.RecheckReachability();
	}

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

	static void AssertNoPileOverCapacity(SimState s)
	{
		for (var tile = 0; tile < s.Piles.Length; tile++)
			if (s.IsPile(tile))
				for (var material = Material.Dirt; (int)material <= SimState.CountedMaterials; material++)
					Assert.True(s.ItemCount(tile, material) <= SimState.PileCapacity, $"Pile tile {tile} holds {s.ItemCount(tile, material)} {material}.");
	}

	// ---- The meter

	[Fact]
	public void Every_goblin_starts_with_a_Sleep_meter_of_70_to_100_drawn_from_SimRandom_after_the_Grub_and_Grog_meters()
	{
		var seen = new HashSet<int>();
		for (var seed = 0; seed < 300; seed++)
		{
			var s = Simulation.NewWorld(seed).State;
			var rng = new SimRandom(seed);
			for (var g = 0; g < 2 * s.GoblinCount; g++)
				rng.NextInt(31);
			for (var g = 0; g < s.GoblinCount; g++)
			{
				Assert.InRange((int)s.GoblinSleep[g], 70, 100);
				Assert.Equal(70 + rng.NextInt(31), s.GoblinSleep[g]);
				seen.Add(s.GoblinSleep[g]);
			}
		}
		Assert.Contains(70, seen);
		Assert.Contains(100, seen);
		Assert.True(seen.Count > 25, "The starting meters should be spread across 70 to 100.");
	}

	[Fact]
	public void The_same_seed_starts_every_goblin_with_the_same_Sleep_meter()
	{
		Assert.Equal(Simulation.NewWorld(seed: 9).State.GoblinSleep, Simulation.NewWorld(seed: 9).State.GoblinSleep);
		Assert.NotEqual(Simulation.NewWorld(seed: 9).State.GoblinSleep, Simulation.NewWorld(seed: 10).State.GoblinSleep);
	}

	[Fact]
	public void The_Sleep_meter_drains_one_point_every_45_ticks_and_leaves_Grub_and_Grog_on_their_own_rates()
	{
		var sim = Lone(sleep: 100);

		RunTicks(sim, 44);
		Assert.Equal(100, sim.State.GoblinSleep[0]);
		RunTicks(sim, 1);
		Assert.Equal(99, sim.State.GoblinSleep[0]);
		RunTicks(sim, 44);
		Assert.Equal(99, sim.State.GoblinSleep[0]);
		RunTicks(sim, 1);
		Assert.Equal(98, sim.State.GoblinSleep[0]);
		RunTicks(sim, 450);
		Assert.Equal(88, sim.State.GoblinSleep[0]);
		Assert.Equal(100 - 540 / 25, sim.State.GoblinGrub[0]);
		Assert.Equal(100 - 540 / 35, sim.State.GoblinGrog[0]);
	}

	[Fact]
	public void A_goblin_at_30_or_more_does_not_crave_sleep()
	{
		var sim = Lone(sleep: 30);

		RunTicks(sim, 44);

		Assert.NotEqual(GoblinTask.Sleeping, TaskOf(sim.State, 0));
		Assert.NotEqual(GoblinTask.SeekingHeap, TaskOf(sim.State, 0));
	}

	// ---- The leading Craving

	[Theory]
	[InlineData(20, 20, 20, Need.Grub)]
	[InlineData(30, 20, 20, Need.Grog)]
	[InlineData(30, 30, 20, Need.Sleep)]
	[InlineData(25, 20, 25, Need.Grog)]
	[InlineData(25, 25, 10, Need.Sleep)]
	[InlineData(10, 25, 20, Need.Grub)]
	[InlineData(29, 30, 30, Need.Grub)]
	[InlineData(30, 29, 30, Need.Grog)]
	[InlineData(30, 30, 29, Need.Sleep)]
	[InlineData(30, 30, 30, Need.None)]
	[InlineData(100, 100, 0, Need.Sleep)]
	public void The_leading_Craving_is_the_lowest_meter_below_30_with_ties_going_Grub_then_Grog_then_Sleep(int grub, int grog, int sleep, Need expected)
	{
		Assert.Equal(expected, NeedRules.LeadingCraving(grub, grog, sleep));
	}

	[Fact]
	public void A_Need_that_was_passed_over_is_skipped_for_the_next_lowest()
	{
		var passedOver = NeedRules.Bit(Need.Grub);
		Assert.Equal(Need.Grog, NeedRules.LeadingCraving(10, 20, 20, passedOver));
		passedOver |= NeedRules.Bit(Need.Grog);
		Assert.Equal(Need.Sleep, NeedRules.LeadingCraving(10, 20, 20, passedOver));
		passedOver |= NeedRules.Bit(Need.Sleep);
		Assert.Equal(Need.None, NeedRules.LeadingCraving(10, 20, 20, passedOver));
	}

	// ---- Which Craving wins

	// Grub lies at (22, 16) and Grog at (10, 16), both in reach of goblin 0 at (16, 16).
	[Theory]
	[InlineData(20, 20, 20, GoblinTask.Seeking)]
	[InlineData(30, 20, 20, GoblinTask.SeekingGrog)]
	[InlineData(30, 30, 20, GoblinTask.Sleeping)]
	[InlineData(25, 25, 10, GoblinTask.Sleeping)]
	[InlineData(25, 10, 20, GoblinTask.SeekingGrog)]
	[InlineData(10, 25, 20, GoblinTask.Seeking)]
	[InlineData(29, 28, 27, GoblinTask.Sleeping)]
	public void With_Cravings_for_several_Needs_the_lowest_meter_wins_and_a_tie_goes_Grub_then_Grog_then_Sleep(int grub, int grog, int sleep, GoblinTask expected)
	{
		var sim = Lone(sleep, grub, grog);
		Put(sim, Material.Grub, 22, 16);
		Put(sim, Material.Grog, 10, 16);

		RunTicks(sim, 1);

		Assert.Equal(expected, TaskOf(sim.State, 0));
	}

	[Fact]
	public void A_Craving_with_nothing_to_satisfy_it_is_passed_over_for_Sleep_which_is_always_satisfiable()
	{
		var sim = Lone(sleep: 25, grub: 10, grog: 20);

		RunTicks(sim, 1);

		// No Grub or Grog anywhere: the lower Cravings wait and the goblin lies down on the spot.
		Assert.Equal(GoblinTask.Sleeping, TaskOf(sim.State, 0));
	}

	[Fact]
	public void A_Craving_for_Sleep_below_a_satisfiable_Craving_waits_until_that_one_is_met()
	{
		var sim = Lone(sleep: 25, grub: 10);
		Put(sim, Material.Grub, 18, 16);

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Seeking, TaskOf(sim.State, 0));

		// 2 steps of 3 ticks, then 10 to eat, then Sleep.
		RunTicks(sim, 6);
		Assert.Equal(GoblinTask.Eating, TaskOf(sim.State, 0));
		RunTicks(sim, 10);
		Assert.Equal(60, sim.State.GoblinGrub[0]);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(sim.State, 0));
	}

	[Fact]
	public void A_goblin_finishes_the_Need_it_has_begun_even_when_Sleep_falls_below_it()
	{
		var sim = Lone(sleep: 29, grub: 29);
		Put(sim, Material.Grub, 22, 16);
		var s = sim.State;

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));

		s.GoblinSleep[0] = 5;
		RunTicks(sim, 16);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Eating, TaskOf(s, 0));
		s.GoblinSleep[0] = 4;
		RunTicks(sim, 9);
		Assert.Equal(GoblinTask.Eating, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(0, s.ItemCount(At(s, 22, 16), Material.Grub));
		Assert.True(s.GoblinGrub[0] >= 70);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
	}

	// ---- Sleeping on the floor

	[Fact]
	public void With_no_Heap_a_goblin_below_30_sleeps_on_the_spot_at_1_point_per_10_ticks_until_full()
	{
		var sim = Lone(sleep: 29);
		var s = sim.State;

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		RunTicks(sim, 8);
		Assert.Equal(29, s.GoblinSleep[0]);
		RunTicks(sim, 1);
		Assert.Equal(30, s.GoblinSleep[0]);
		RunTicks(sim, 10);
		Assert.Equal(31, s.GoblinSleep[0]);

		// Rising past the Craving threshold does not wake it; 29 + 71 points is full on tick 710.
		RunTicks(sim, 689);
		Assert.Equal(99, s.GoblinSleep[0]);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(100, s.GoblinSleep[0]);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal((16, 16, 0), (s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]));
	}

	[Fact]
	public void The_Sleep_meter_does_not_drain_while_the_goblin_sleeps()
	{
		var sim = Lone(sleep: 20);

		// 100 ticks on the floor is 10 points, with two drain ticks (45 and 90) passing.
		RunTicks(sim, 100);

		Assert.Equal(30, sim.State.GoblinSleep[0]);
	}

	[Fact]
	public void A_Heap_out_of_reach_is_no_Heap_and_the_goblin_sleeps_on_the_spot_at_the_floor_rate()
	{
		var sim = Lone(sleep: 20);
		Heap(sim, 20, 16);
		Wall(sim, 18, 0, 18, 31);
		var s = sim.State;

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		RunTicks(sim, 9);
		Assert.Equal(21, s.GoblinSleep[0]);
		RunTicks(sim, 9);
		Assert.Equal(21, s.GoblinSleep[0]);
		RunTicks(sim, 1);
		Assert.Equal(22, s.GoblinSleep[0]);
		Assert.Equal((16, 16), (s.GoblinX[0], s.GoblinY[0]));
	}

	[Fact]
	public void A_Starving_goblin_sleeps_at_the_normal_rate()
	{
		var sim = Lone(sleep: 20, grub: 0);

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(sim.State, 0));
		RunTicks(sim, 99);

		Assert.Equal(30, sim.State.GoblinSleep[0]);
	}

	[Fact]
	public void A_Starving_goblin_sleeps_at_the_normal_rate_on_a_Heap_too()
	{
		var sim = Lone(sleep: 20, grub: 0);
		Heap(sim, 16, 16);

		RunTicks(sim, 100);

		Assert.Equal(40, sim.State.GoblinSleep[0]);
	}

	[Fact]
	public void A_Starving_goblin_walks_to_the_Heap_at_half_speed()
	{
		var sim = Lone(sleep: 20, grub: 0);
		Heap(sim, 19, 16);
		var s = sim.State;

		// 3 tiles at one step per 6 ticks: the third step is on tick 18.
		RunTicks(sim, 17);
		Assert.Equal(GoblinTask.SeekingHeap, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(19, s.GoblinX[0]);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
	}

	// ---- Sleeping on a Heap

	[Fact]
	public void A_goblin_below_30_walks_to_the_Heap_and_sleeps_there_at_1_point_per_5_ticks_until_full()
	{
		var sim = Lone(sleep: 20);
		Heap(sim, 20, 16);
		var s = sim.State;

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.SeekingHeap, TaskOf(s, 0));
		// Four steps of 3 ticks: the goblin arrives, and lies down, on tick 12.
		RunTicks(sim, 10);
		Assert.Equal(GoblinTask.SeekingHeap, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal((20, 16, 0), (s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]));

		// 80 points at 5 ticks each is 400 ticks of sleep, with the first counted on tick 13.
		RunTicks(sim, 4);
		Assert.Equal(20, s.GoblinSleep[0]);
		RunTicks(sim, 1);
		Assert.Equal(21, s.GoblinSleep[0]);
		RunTicks(sim, 5);
		Assert.Equal(22, s.GoblinSleep[0]);
		RunTicks(sim, 389);
		Assert.Equal(99, s.GoblinSleep[0]);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(100, s.GoblinSleep[0]);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(412, s.Tick);
	}

	[Fact]
	public void A_goblin_standing_on_a_Heap_sleeps_there_at_once()
	{
		var sim = Lone(sleep: 20);
		Heap(sim, 16, 16);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(sim.State, 0));
		RunTicks(sim, 4);
		Assert.Equal(21, sim.State.GoblinSleep[0]);
	}

	[Fact]
	public void The_nearest_Heap_by_path_wins_with_ties_to_the_lowest_tile_index()
	{
		var sim = Lone(sleep: 20);
		Heap(sim, 16, 20);
		Heap(sim, 16, 12);
		Heap(sim, 12, 16);
		Heap(sim, 20, 16);

		RunTicks(sim, 60);

		// The four are equally far; the one with the lowest index is the one above.
		Assert.Equal((16, 12), (sim.State.GoblinX[0], sim.State.GoblinY[0]));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(sim.State, 0));
	}

	[Fact]
	public void A_Heap_that_is_close_as_the_crow_flies_but_far_by_path_loses_to_a_nearer_walk()
	{
		var sim = Lone(sleep: 20);
		Wall(sim, 18, 12, 18, 20);
		Heap(sim, 19, 16);
		Heap(sim, 16, 22);

		RunTicks(sim, 60);

		Assert.Equal((16, 22), (sim.State.GoblinX[0], sim.State.GoblinY[0]));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(sim.State, 0));
	}

	[Fact]
	public void Several_goblins_share_one_Heap_tile()
	{
		var sim = Crew(count: 4, sleep: 20);
		Heap(sim, 16, 12);
		var s = sim.State;

		RunTicks(sim, 40);

		for (var g = 0; g < 4; g++)
		{
			Assert.Equal(GoblinTask.Sleeping, TaskOf(s, g));
			Assert.Equal((16, 12), (s.GoblinX[g], s.GoblinY[g]));
		}
		RunTicks(sim, 5);
		for (var g = 0; g < 4; g++)
			Assert.True(s.GoblinSleep[g] > 20);
	}

	[Fact]
	public void A_goblin_on_another_level_walks_to_a_Heap_over_the_stairs()
	{
		var sim = Lone(sleep: 20);
		var s = sim.State;
		// Stairs down at (17, 16) on z0 and z1, and a Heap beyond them on z1.
		foreach (var z in new[] { 0, 1 })
			s.TileKinds[At(s, 17, 16, z)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 18, 16, 1)] = (byte)TileKind.Floor;
		s.TileKinds[At(s, 19, 16, 1)] = (byte)TileKind.Floor;
		sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 1, 19, 16, 19, 16));

		RunTicks(sim, 60);

		Assert.Equal((19, 16, 1), (s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
	}

	[Fact]
	public void A_Heap_removed_while_the_goblin_walks_to_it_sends_it_to_sleep_where_it_stands_at_the_floor_rate()
	{
		var sim = Lone(sleep: 20);
		Heap(sim, 20, 16);
		var s = sim.State;
		RunTicks(sim, 2);
		Assert.Equal(GoblinTask.SeekingHeap, TaskOf(s, 0));

		sim.Apply(new RemoveRoom(RoomKind.SleepingHeap, 0, 20, 16, 20, 16));
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal((16, 16), (s.GoblinX[0], s.GoblinY[0]));
		RunTicks(sim, 9);
		Assert.Equal(20, s.GoblinSleep[0]);
		RunTicks(sim, 1);
		Assert.Equal(21, s.GoblinSleep[0]);
	}

	[Fact]
	public void A_Heap_removed_but_for_a_farther_one_redirects_the_goblin_to_it()
	{
		var sim = Lone(sleep: 20);
		Heap(sim, 20, 16);
		Heap(sim, 16, 24);
		var s = sim.State;
		RunTicks(sim, 4);

		sim.Apply(new RemoveRoom(RoomKind.SleepingHeap, 0, 20, 16, 20, 16));
		RunTicks(sim, 80);

		Assert.Equal((16, 24), (s.GoblinX[0], s.GoblinY[0]));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
	}

	[Fact]
	public void A_Heap_removed_under_a_sleeper_slows_it_to_the_floor_rate_and_the_count_carries_on()
	{
		var sim = Lone(sleep: 20);
		Heap(sim, 16, 16);
		var s = sim.State;
		RunTicks(sim, 7);
		Assert.Equal(21, s.GoblinSleep[0]);

		sim.Apply(new RemoveRoom(RoomKind.SleepingHeap, 0, 16, 16, 16, 16));
		RunTicks(sim, 7);
		Assert.Equal(21, s.GoblinSleep[0]);
		RunTicks(sim, 1);
		Assert.Equal(22, s.GoblinSleep[0]);
		RunTicks(sim, 9);
		Assert.Equal(22, s.GoblinSleep[0]);
		RunTicks(sim, 1);
		Assert.Equal(23, s.GoblinSleep[0]);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
	}

	[Fact]
	public void A_Heap_painted_under_a_floor_sleeper_speeds_it_to_the_Heap_rate()
	{
		var sim = Lone(sleep: 20);
		var s = sim.State;
		RunTicks(sim, 6);
		Assert.Equal(20, s.GoblinSleep[0]);

		Heap(sim, 16, 16);
		RunTicks(sim, 1);
		Assert.Equal(21, s.GoblinSleep[0]);
		RunTicks(sim, 5);
		Assert.Equal(22, s.GoblinSleep[0]);
	}

	[Fact]
	public void Digging_stairs_through_a_Heap_clears_it()
	{
		var sim = Lone(sleep: 100);
		Heap(sim, 17, 16);
		Heap(sim, 18, 16);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 17, 16, 17, 16));
		var s = sim.State;

		RunTicks(sim, 80);

		Assert.Equal(TileKind.Stairs, s.KindAt(At(s, 17, 16)));
		Assert.Equal(RoomKind.None, s.RoomAt(At(s, 17, 16)));
		Assert.Equal(RoomKind.SleepingHeap, s.RoomAt(At(s, 18, 16)));
	}

	// ---- Painting

	[Fact]
	public void A_Heap_is_painted_on_Floor_only_and_skips_a_tile_holding_another_Room()
	{
		var sim = Lone(sleep: 100);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 4, 4, 4, 4));
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 5, 4, 5, 4));
		s.TileKinds[At(s, 6, 4)] = (byte)TileKind.Stairs;

		sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 0, 4, 4, 7, 4));
		sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 1, 4, 4, 7, 4));

		Assert.Equal(RoomKind.Pile, s.RoomAt(At(s, 4, 4)));
		Assert.Equal(RoomKind.GrubPit, s.RoomAt(At(s, 5, 4)));
		Assert.Equal(RoomKind.None, s.RoomAt(At(s, 6, 4)));
		Assert.Equal(RoomKind.SleepingHeap, s.RoomAt(At(s, 7, 4)));
		Assert.Equal(RoomKind.None, s.RoomAt(At(s, 7, 4, 1)));
	}

	[Fact]
	public void Painting_a_Pile_or_a_Grub_Pit_skips_a_Heap_tile()
	{
		var sim = Lone(sleep: 100);
		var s = sim.State;
		Heap(sim, 4, 4);

		sim.Apply(new PaintPile(0, 4, 4, 4, 4));
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 4, 4, 4, 4));

		Assert.Equal(RoomKind.SleepingHeap, s.RoomAt(At(s, 4, 4)));
		Assert.False(s.IsPile(At(s, 4, 4)));
	}

	[Fact]
	public void Removing_Heaps_takes_only_Heaps_and_leaves_loose_items_where_they_lie()
	{
		var sim = Lone(sleep: 100);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 4, 4, 4, 4));
		Heap(sim, 5, 4);
		Heap(sim, 6, 4);
		Put(sim, Material.Dirt, 6, 4, units: 2);

		sim.Apply(new RemoveRoom(RoomKind.SleepingHeap, 0, 3, 4, 7, 4));

		Assert.Equal(RoomKind.Pile, s.RoomAt(At(s, 4, 4)));
		Assert.Equal(RoomKind.None, s.RoomAt(At(s, 5, 4)));
		Assert.Equal(RoomKind.None, s.RoomAt(At(s, 6, 4)));
		Assert.Equal(2, s.LooseCount(At(s, 6, 4), Material.Dirt));
	}

	[Fact]
	public void Removing_Piles_leaves_a_Heap_alone()
	{
		var sim = Lone(sleep: 100);
		Heap(sim, 5, 4);

		sim.Apply(new RemoveRoom(RoomKind.Pile, 0, 5, 4, 5, 4));

		Assert.Equal(RoomKind.SleepingHeap, sim.State.RoomAt(At(sim.State, 5, 4)));
	}

	// ---- Other Needs while asleep

	[Fact]
	public void Grub_and_Grog_keep_draining_during_sleep_and_do_not_wake_the_goblin()
	{
		var sim = Lone(sleep: 20, grub: 40, grog: 40);
		Put(sim, Material.Grub, 17, 16);
		Put(sim, Material.Grog, 15, 16);
		var s = sim.State;

		// Sleeping on the floor to 100 takes 800 ticks; Grub falls from 40 to below 30 by tick 275 and Grog by tick 385.
		for (var i = 0; i < 79; i++)
		{
			RunTicks(sim, 10);
			Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		}
		Assert.Equal(99, s.GoblinSleep[0]);
		Assert.Equal(40 - 790 / 25, s.GoblinGrub[0]);
		Assert.Equal(40 - 790 / 35, s.GoblinGrog[0]);
		Assert.Equal(1, s.ItemCount(At(s, 17, 16), Material.Grub));
		Assert.Equal(1, s.ItemCount(At(s, 15, 16), Material.Grog));
		Assert.Equal((16, 16), (s.GoblinX[0], s.GoblinY[0]));

		// Woken by a full meter, it goes for the Grub it has been craving, the lower meter.
		RunTicks(sim, 10);
		Assert.Equal(100, s.GoblinSleep[0]);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
	}

	[Fact]
	public void A_goblin_that_wakes_goes_back_to_the_dig_it_left()
	{
		var sim = Lone(sleep: 29);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 18, 16, 18, 16));

		// The dig is the nearest work for an idle goblin, so it leaves the dig for the lie-down and resumes afterwards.
		RunTicks(sim, 5);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(sim.State, 0));
		RunTicks(sim, 800);

		Assert.Equal(TileKind.Stairs, sim.State.KindAt(At(sim.State, 18, 16)));
	}

	// ---- Cravings drop work and carried units

	[Fact]
	public void A_digging_goblin_below_30_leaves_the_dig_to_sleep_and_the_tile_keeps_its_progress()
	{
		var sim = Lone(sleep: 40);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 18, 16, 18, 16));
		var s = sim.State;
		RunTicks(sim, 10);
		Assert.Equal(GoblinTask.Digging, TaskOf(s, 0));
		var progress = s.DigProgress[At(s, 18, 16)];
		Assert.True(progress > 0);

		s.GoblinSleep[0] = 29;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(progress, s.DigProgress[At(s, 18, 16)]);
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(At(s, 18, 16)));
	}

	[Fact]
	public void A_carrier_with_a_Craving_for_Sleep_stores_its_unit_on_the_Pile_it_stands_on_and_sleeps()
	{
		var sim = Lone(sleep: 29);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 16, 16, 16, 16));
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(1, s.ItemCount(At(s, 16, 16), Material.Dirt));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
	}

	[Fact]
	public void A_carrier_away_from_a_Pile_with_a_Craving_for_Sleep_drops_its_unit_loose_and_sleeps()
	{
		var sim = Lone(sleep: 29);
		var s = sim.State;
		GoblinTasks.FinishPickUp(s, 0, Material.Stone);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(1, s.LooseCount(At(s, 16, 16), Material.Stone));
		Assert.Equal(1, Total(s, Material.Stone));
	}

	[Fact]
	public void A_carrier_on_a_full_Pile_with_a_Craving_for_Sleep_drops_on_the_nearest_legal_tile()
	{
		var sim = Lone(sleep: 29);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 15, 16, 17, 16));
		sim.Apply(new PaintPile(0, 16, 15, 16, 15));
		foreach (var (x, y) in new[] { (15, 16), (16, 16), (17, 16), (16, 15) })
			s.AddItems(At(s, x, y), Material.Dirt, SimState.PileCapacity);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		sim.RecheckReachability();

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(1, s.LooseCount(At(s, 16, 17), Material.Dirt));
		Assert.Equal(4 * SimState.PileCapacity + 1, Total(s, Material.Dirt));
		AssertNoPileOverCapacity(s);
	}

	// ---- Collapse

	[Fact]
	public void A_digging_goblin_at_Sleep_0_collapses_on_the_spot_and_sleeps_until_full_whatever_else_it_craves()
	{
		var sim = Lone(sleep: 40);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 18, 16, 18, 16));
		Heap(sim, 12, 16);
		Put(sim, Material.Grub, 15, 16);
		var s = sim.State;
		RunTicks(sim, 10);
		Assert.Equal(GoblinTask.Digging, TaskOf(s, 0));
		var progress = s.DigProgress[At(s, 18, 16)];
		var (x, y) = (s.GoblinX[0], s.GoblinY[0]);

		s.GoblinSleep[0] = 0;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal((x, y), (s.GoblinX[0], s.GoblinY[0]));
		Assert.Equal(progress, s.DigProgress[At(s, 18, 16)]);

		// Hunger cannot interrupt it. The nearby Heap is a walk away, so the spot is the floor: 1 point per 10 ticks,
		// the first counted on the tick it collapsed.
		s.GoblinGrub[0] = 5;
		RunTicks(sim, 8);
		Assert.Equal(0, s.GoblinSleep[0]);
		RunTicks(sim, 1);
		Assert.Equal(1, s.GoblinSleep[0]);
		RunTicks(sim, 980);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(99, s.GoblinSleep[0]);
		Assert.Equal(1, s.ItemCount(At(s, 15, 16), Material.Grub));
		RunTicks(sim, 10);
		Assert.Equal(100, s.GoblinSleep[0]);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(0, s.GoblinGrub[0]);
	}

	[Fact]
	public void A_collapsed_goblin_standing_on_a_Heap_sleeps_at_the_Heap_rate()
	{
		var sim = Lone(sleep: 40);
		Heap(sim, 16, 16);
		var s = sim.State;
		s.GoblinSleep[0] = 0;

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		RunTicks(sim, 4);

		Assert.Equal(1, s.GoblinSleep[0]);
	}

	[Fact]
	public void A_hungry_goblin_at_Sleep_0_standing_on_Grub_collapses_and_does_not_eat()
	{
		var sim = Lone(sleep: 0, grub: 5);
		Put(sim, Material.Grub, 16, 16);
		var s = sim.State;

		// Standing on Grub, hungry, and at Sleep 0: it collapses and does not eat.
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		RunTicks(sim, 200);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(1, s.ItemCount(At(s, 16, 16), Material.Grub));
	}

	[Fact]
	public void A_goblin_mid_meal_at_Sleep_0_collapses_and_the_unit_is_not_eaten()
	{
		var sim = Lone(sleep: 40, grub: 25);
		Put(sim, Material.Grub, 16, 16);
		var s = sim.State;
		RunTicks(sim, 4);
		Assert.Equal(GoblinTask.Eating, TaskOf(s, 0));

		s.GoblinSleep[0] = 0;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(1, s.ItemCount(At(s, 16, 16), Material.Grub));
		// The meal's ticks are gone: the tick it collapsed is the first of the sleep.
		Assert.Equal(1, s.GoblinWorkTicks[0]);
	}

	[Fact]
	public void A_goblin_walking_to_a_meal_at_Sleep_0_collapses_where_it_stands()
	{
		var sim = Lone(sleep: 40, grub: 25);
		Put(sim, Material.Grub, 22, 16);
		var s = sim.State;
		RunTicks(sim, 7);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		var (x, y) = (s.GoblinX[0], s.GoblinY[0]);

		s.GoblinSleep[0] = 0;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal((x, y), (s.GoblinX[0], s.GoblinY[0]));
	}

	[Fact]
	public void A_goblin_walking_to_a_Heap_at_Sleep_0_collapses_where_it_stands()
	{
		var sim = Lone(sleep: 20);
		Heap(sim, 26, 16);
		var s = sim.State;
		RunTicks(sim, 7);
		Assert.Equal(GoblinTask.SeekingHeap, TaskOf(s, 0));
		var (x, y) = (s.GoblinX[0], s.GoblinY[0]);

		s.GoblinSleep[0] = 0;
		RunTicks(sim, 5);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal((x, y), (s.GoblinX[0], s.GoblinY[0]));
	}

	[Fact]
	public void Sleep_draining_to_0_during_a_long_walk_for_Grub_collapses_the_goblin()
	{
		var sim = Lone(sleep: 1, grub: 10);
		Put(sim, Material.Grub, 30, 16);
		var s = sim.State;
		// Sleep 1 is below Grub's 10, so the goblin lies down first; make Grub the lower one so that it walks.
		s.GoblinGrub[0] = 0;
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));

		RunTicks(sim, 44);

		Assert.Equal(0, s.GoblinSleep[0]);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(1, s.ItemCount(At(s, 30, 16), Material.Grub));
	}

	[Fact]
	public void A_carrier_at_Sleep_0_drops_its_unit_loose_and_collapses()
	{
		var sim = Lone(sleep: 40);
		var s = sim.State;
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		s.GoblinSleep[0] = 0;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(At(s, 16, 16), Material.Dirt));
	}

	[Fact]
	public void A_goblin_mid_drop_at_Sleep_0_stores_its_unit_on_the_Pile_it_stands_on()
	{
		var sim = Lone(sleep: 40);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 16, 16, 16, 16));
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		GoblinTasks.StartDropping(s, 0);
		s.GoblinSleep[0] = 0;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(1, s.StoredCount(At(s, 16, 16), Material.Dirt));
	}

	[Fact]
	public void A_collapsing_carrier_on_a_full_Pile_drops_on_the_nearest_legal_tile_with_ties_to_the_lowest_index()
	{
		var sim = Lone(sleep: 40);
		var s = sim.State;
		// Full dirt Piles on the goblin's tile and the tiles to its left and right; above and below are bare floor.
		sim.Apply(new PaintPile(0, 15, 16, 17, 16));
		foreach (var x in new[] { 15, 16, 17 })
			s.AddItems(At(s, x, 16), Material.Dirt, SimState.PileCapacity);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		sim.RecheckReachability();
		s.GoblinSleep[0] = 0;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(1, s.LooseCount(At(s, 16, 15), Material.Dirt));
		Assert.Equal(0, s.LooseCount(At(s, 16, 17), Material.Dirt));
		Assert.Equal(3 * SimState.PileCapacity + 1, Total(s, Material.Dirt));
		AssertNoPileOverCapacity(s);
	}

	[Fact]
	public void A_collapsing_carrier_boxed_in_by_full_Piles_drops_on_the_nearest_legal_tile_further_out()
	{
		var sim = Lone(sleep: 40);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 15, 15, 17, 17));
		for (var y = 15; y <= 17; y++)
		for (var x = 15; x <= 17; x++)
			s.AddItems(At(s, x, y), Material.Dirt, SimState.PileCapacity);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		sim.RecheckReachability();
		s.GoblinSleep[0] = 0;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(1, s.LooseCount(At(s, 16, 14), Material.Dirt));
		Assert.Equal(9 * SimState.PileCapacity + 1, Total(s, Material.Dirt));
		AssertNoPileOverCapacity(s);
	}

	[Fact]
	public void A_collapsing_carrier_with_nowhere_legal_to_put_its_unit_keeps_it_and_collapses_once_a_tile_opens()
	{
		var sim = Lone(sleep: 40);
		var s = sim.State;
		// A one-tile cell: the goblin's own tile is a full dirt Pile and every neighbour is Solid.
		sim.Apply(new PaintPile(0, 16, 16, 16, 16));
		s.AddItems(At(s, 16, 16), Material.Dirt, SimState.PileCapacity);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		Wall(sim, 15, 16, 15, 16);
		Wall(sim, 17, 16, 17, 16);
		Wall(sim, 16, 15, 16, 15);
		Wall(sim, 16, 17, 16, 17);
		s.GoblinSleep[0] = 0;

		RunTicks(sim, 20);

		// Nothing is dropped past the Pile's capacity and nothing is lost: the goblin waits, still holding the unit.
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
		Assert.Equal(SimState.PileCapacity + 1, Total(s, Material.Dirt));
		AssertNoPileOverCapacity(s);

		s.TileKinds[At(s, 17, 16)] = (byte)TileKind.Floor;
		sim.RecheckReachability();
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(1, s.LooseCount(At(s, 17, 16), Material.Dirt));
		Assert.Equal(SimState.PileCapacity + 1, Total(s, Material.Dirt));
	}

	[Fact]
	public void A_Starving_goblin_collapses_and_sleeps_at_the_normal_rate()
	{
		var sim = Lone(sleep: 0, grub: 0);

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(sim.State, 0));
		RunTicks(sim, 99);

		Assert.Equal(10, sim.State.GoblinSleep[0]);
	}

	// ---- Tasks

	[Fact]
	public void BeginSleep_walks_to_a_Heap_or_sleeps_at_once_and_puts_a_carried_unit_down_first()
	{
		var sim = Lone(sleep: 20);
		var s = sim.State;
		GoblinTasks.BeginWander(s, 0, At(s, 18, 18));

		GoblinTasks.BeginSleep(s, 0, dropTile: -1, asleep: false);

		Assert.Equal(GoblinTask.SeekingHeap, TaskOf(s, 0));
		Assert.Equal(-1, s.GoblinWanderTile[0]);
		Assert.Null(GoblinTasks.FindViolation(s));

		var other = Lone(sleep: 20);
		var o = other.State;
		GoblinTasks.FinishPickUp(o, 0, Material.Dirt);
		var beside = At(o, 17, 16);
		GoblinTasks.BeginSleep(o, 0, beside, asleep: true);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(o, 0));
		Assert.Equal((byte)Material.None, o.GoblinCarry[0]);
		Assert.Equal(1, o.LooseCount(beside, Material.Dirt));
		Assert.Null(GoblinTasks.FindViolation(o));
	}

	[Fact]
	public void BeginSleep_refuses_a_goblin_already_satisfying_a_Need_and_a_drop_tile_that_cannot_take_the_unit()
	{
		var sim = Lone(sleep: 20, grub: 20);
		var s = sim.State;
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: false);
		Assert.Throws<InvalidOperationException>(() => GoblinTasks.BeginSleep(s, 0, dropTile: -1, asleep: true));

		var other = Lone(sleep: 20);
		GoblinTasks.BeginSleep(other.State, 0, dropTile: -1, asleep: true);
		Assert.Throws<InvalidOperationException>(() => GoblinTasks.BeginSleep(other.State, 0, dropTile: -1, asleep: false));
		Assert.Throws<InvalidOperationException>(() => GoblinTasks.BeginSeek(other.State, 0, dropTile: -1, eating: false));

		var carrier = Lone(sleep: 20);
		var c = carrier.State;
		GoblinTasks.FinishPickUp(c, 0, Material.Dirt);
		Assert.Throws<InvalidOperationException>(() => GoblinTasks.BeginSleep(c, 0, c.Width * c.Height * c.Depth, asleep: true));
	}

	[Fact]
	public void BeginCollapse_takes_a_goblin_off_any_Task_but_only_at_Sleep_0()
	{
		var sim = Lone(sleep: 20, grub: 20);
		var s = sim.State;
		Put(sim, Material.Grub, 16, 16);
		GoblinTasks.BeginSeek(s, 0, dropTile: -1, eating: true);
		Assert.Throws<InvalidOperationException>(() => GoblinTasks.BeginCollapse(s, 0, dropTile: -1));

		s.GoblinSleep[0] = 0;
		GoblinTasks.BeginCollapse(s, 0, dropTile: -1);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
		// Already asleep: nothing to collapse.
		Assert.Throws<InvalidOperationException>(() => GoblinTasks.BeginCollapse(s, 0, dropTile: -1));
	}

	[Fact]
	public void SleepTick_counts_work_ticks_and_adds_a_point_on_the_rate_then_releases_a_full_goblin()
	{
		var sim = Lone(sleep: 98);
		var s = sim.State;
		GoblinTasks.BeginSleep(s, 0, dropTile: -1, asleep: true);

		for (var i = 0; i < NeedRules.FloorRecoveryTicks - 1; i++)
			GoblinTasks.SleepTick(s, 0);
		Assert.Equal(98, s.GoblinSleep[0]);
		Assert.Equal(NeedRules.FloorRecoveryTicks - 1, s.GoblinWorkTicks[0]);
		GoblinTasks.SleepTick(s, 0);
		Assert.Equal(99, s.GoblinSleep[0]);
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		for (var i = 0; i < NeedRules.FloorRecoveryTicks; i++)
			GoblinTasks.SleepTick(s, 0);
		Assert.Equal(100, s.GoblinSleep[0]);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_flags_a_seeker_with_no_Craving_and_a_sleeper_with_a_full_meter_or_stray_fields()
	{
		var sim = Lone(sleep: 20);
		var s = sim.State;
		GoblinTasks.BeginSleep(s, 0, dropTile: -1, asleep: false);
		Assert.Null(GoblinTasks.FindViolation(s));
		s.GoblinSleep[0] = 30;
		Assert.Contains("no Craving", GoblinTasks.FindViolation(s));

		s.GoblinSleep[0] = 50;
		s.GoblinTask[0] = (byte)GoblinTask.Sleeping;
		Assert.Null(GoblinTasks.FindViolation(s));
		s.GoblinSleep[0] = 100;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinSleep[0] = 50;

		s.GoblinWorkTicks[0] = (byte)(NeedRules.FloorRecoveryTicks - 1);
		Assert.Null(GoblinTasks.FindViolation(s));
		s.GoblinWorkTicks[0] = (byte)NeedRules.FloorRecoveryTicks;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinWorkTicks[0] = 0;

		s.GoblinJobTile[0] = At(s, 3, 3);
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinJobTile[0] = -1;
		s.GoblinCarry[0] = (byte)Material.Dirt;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinCarry[0] = (byte)Material.None;
		s.GoblinWanderTile[0] = At(s, 3, 3);
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinWanderTile[0] = -1;
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	// ---- Saves and replays

	static void RunBoth(Simulation a, Simulation b, int ticks)
	{
		RunTicks(a, ticks);
		RunTicks(b, ticks);
	}

	[Fact]
	public void A_save_with_goblins_asleep_on_a_Heap_and_the_floor_round_trips_and_replays_identically()
	{
		var sim = Simulation.NewWorld(seed: 5);
		var s = sim.State;
		sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 0, 12, 12, 12, 12));
		(s.GoblinSleep[0], s.GoblinSleep[1], s.GoblinSleep[2], s.GoblinSleep[3]) = (28, 22, 0, 25);
		Array.Fill(s.GoblinGrub, (byte)90);
		Array.Fill(s.GoblinGrog, (byte)90);
		// Mid-walk to the Heap, with one goblin already collapsed.
		RunTicks(sim, 10);
		Assert.Contains(Enumerable.Range(0, s.GoblinCount), g => TaskOf(s, g) == GoblinTask.SeekingHeap);
		Assert.Contains(Enumerable.Range(0, s.GoblinCount), g => TaskOf(s, g) == GoblinTask.Sleeping);
		var walking = SimJson.Deserialize(SimJson.Serialize(sim));
		RunBoth(sim, walking, 400);
		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(walking));

		// Asleep on the Heap and on the floor.
		sim = Simulation.NewWorld(seed: 5);
		s = sim.State;
		sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 0, 12, 12, 12, 12));
		(s.GoblinSleep[0], s.GoblinSleep[1], s.GoblinSleep[2], s.GoblinSleep[3]) = (28, 22, 0, 25);
		Array.Fill(s.GoblinGrub, (byte)90);
		Array.Fill(s.GoblinGrog, (byte)90);
		RunTicks(sim, 37);
		Assert.Contains(Enumerable.Range(0, s.GoblinCount), g => TaskOf(s, g) == GoblinTask.Sleeping && s.GoblinX[g] == 12 && s.GoblinY[g] == 12);
		Assert.Contains(Enumerable.Range(0, s.GoblinCount), g => TaskOf(s, g) == GoblinTask.Sleeping && s.GoblinSleep[g] < 5);

		var json = SimJson.Serialize(sim);
		var restored = SimJson.Deserialize(json);
		Assert.Equal(json, SimJson.Serialize(restored));

		RunBoth(sim, restored, 120);
		var command = new PaintRoom(RoomKind.SleepingHeap, 0, 20, 20, 20, 20);
		sim.Apply(command);
		restored.Apply(command);
		RunBoth(sim, restored, 500);

		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
		var again = SimJson.Deserialize(SimJson.Serialize(sim));
		RunBoth(sim, again, 300);
		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(again));
	}

	[Fact]
	public void The_same_seed_and_commands_replay_a_run_with_sleep_identically()
	{
		static string Run()
		{
			var sim = Simulation.NewWorld(seed: 21);
			var s = sim.State;
			for (var g = 0; g < s.GoblinCount; g++)
				s.GoblinSleep[g] = (byte)(15 + 4 * g);
			sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 0, 8, 8, 9, 9));
			sim.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 20, 12, 21));
			RunTicks(sim, 1500);
			return SimJson.Serialize(sim);
		}

		Assert.Equal(Run(), Run());
	}

	[Fact]
	public void A_save_with_no_Sleep_meters_is_rejected_as_an_old_save()
	{
		var sim = Simulation.NewWorld(seed: 3);
		sim.State.GoblinSleep = [];

		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Fact]
	public void A_save_with_a_Sleep_meter_above_100_or_the_wrong_number_of_meters_is_rejected()
	{
		var tall = Simulation.NewWorld(seed: 3);
		tall.State.GoblinSleep[0] = 101;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(tall)));

		var short_ = Simulation.NewWorld(seed: 3);
		short_.State.GoblinSleep = new byte[3];
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(short_)));
	}

	[Fact]
	public void A_save_with_a_Heap_on_a_tile_that_is_not_Floor_or_that_is_also_a_Pile_is_rejected()
	{
		var stone = Simulation.NewWorld(seed: 3);
		stone.State.Rooms[At(stone.State, 3, 3, 1)] = (byte)RoomKind.SleepingHeap;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(stone)));

		var both = Simulation.NewWorld(seed: 3);
		both.State.Rooms[At(both.State, 3, 3)] = (byte)RoomKind.SleepingHeap;
		both.State.Piles[At(both.State, 3, 3)] = 1;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(both)));

		var unknown = Simulation.NewWorld(seed: 3);
		unknown.State.Rooms[At(unknown.State, 3, 3)] = 9;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(unknown)));
	}

	[Fact]
	public void A_save_with_a_sleeping_goblin_is_accepted_whatever_its_meter_below_100()
	{
		foreach (var meter in new[] { 0, 29, 30, 99 })
		{
			var sim = Lone(sleep: meter);
			GoblinTasks.BeginSleep(sim.State, 0, dropTile: -1, asleep: true);
			sim.State.GoblinWorkTicks[0] = (byte)(NeedRules.FloorRecoveryTicks - 1);

			var restored = SimJson.Deserialize(SimJson.Serialize(sim));

			Assert.Equal(GoblinTask.Sleeping, TaskOf(restored.State, 0));
			Assert.Equal(meter, restored.State.GoblinSleep[0]);
			Assert.Equal(NeedRules.FloorRecoveryTicks - 1, restored.State.GoblinWorkTicks[0]);
		}
	}

	[Fact]
	public void A_save_with_malformed_sleep_Tasks_is_rejected()
	{
		void Rejects(Action<SimState> corrupt)
		{
			var sim = Lone(sleep: 40);
			GoblinTasks.BeginSleep(sim.State, 0, dropTile: -1, asleep: true);
			corrupt(sim.State);
			Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
		}

		Rejects(s => s.GoblinSleep[0] = 100);
		Rejects(s => s.GoblinWorkTicks[0] = (byte)NeedRules.FloorRecoveryTicks);
		Rejects(s => s.GoblinJobTile[0] = At(s, 3, 3));
		Rejects(s => s.GoblinCarry[0] = (byte)Material.Dirt);
		Rejects(s => s.GoblinWanderTile[0] = At(s, 3, 3));
		Rejects(s => s.GoblinWanderTicks[0] = 3);
		Rejects(s => s.GoblinTask[0] = (byte)GoblinTask.SeekingHeap);
		Rejects(s => s.GoblinTask[0] = 99);
		Rejects(s => s.GoblinStepTicks[0] = (byte)GoblinTasks.WalkTicksPerTile);
	}

	// ---- The whole run

	[Fact]
	public void A_long_run_keeps_every_Task_rule_and_every_Pile_in_bounds_with_goblins_sleeping_among_other_work()
	{
		var sim = Simulation.NewWorld(seed: 8);
		var s = sim.State;
		for (var g = 0; g < s.GoblinCount; g++)
			(s.GoblinSleep[g], s.GoblinGrub[g], s.GoblinGrog[g]) = ((byte)(12 + 3 * g), (byte)(45 + 6 * g), (byte)(55 + 5 * g));
		sim.Apply(new PaintPile(0, 8, 8, 9, 9));
		sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 0, 20, 20, 21, 20));
		sim.Apply(new Designate(DesignationKind.Dig, 0, 14, 4, 18, 6));
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 24, 8, 25, 8));

		RunTicks(sim, 4000);

		AssertNoPileOverCapacity(s);
		for (var g = 0; g < s.GoblinCount; g++)
			Assert.InRange((int)s.GoblinSleep[g], 0, 100);
	}
}
