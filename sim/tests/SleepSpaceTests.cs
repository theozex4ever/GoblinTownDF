using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>
/// #58: at most one goblin sleeps on a tile. Awake goblins still share tiles and walk through sleepers. Every scenario is a lone crew
/// placed by hand, so the expected tiles come from the spec's rules (nearest free bed by path length, ties to the lowest tile index).
/// </summary>
public class SleepSpaceTests
{
	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	// The first `count` Grunts, every meter at 100 except Sleep, each placed on (x, y) of its entry in `at`.
	static Simulation Crew(int sleep, params (int X, int Y)[] at)
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		var s = sim.State;
		s.GoblinCount = at.Length;
		for (var g = 0; g < at.Length; g++)
		{
			(s.GoblinSleep[g], s.GoblinGrub[g], s.GoblinGrog[g]) = ((byte)sleep, 100, 100);
			(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]) = (at[g].X, at[g].Y, 0);
		}
		sim.RecheckReachability();
		return sim;
	}

	static void Heap(Simulation sim, int x, int y) => sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 0, x, y, x, y));

	// Turns the whole surface to Solid, then back to Floor on the given tiles, so the goblins have exactly that much room.
	static void Pocket(Simulation sim, params (int X, int Y)[] floor)
	{
		var s = sim.State;
		for (var tile = 0; tile < s.Width * s.Height; tile++)
			s.TileKinds[tile] = (byte)TileKind.Solid;
		foreach (var (x, y) in floor)
			s.TileKinds[s.TileIndex(x, y, 0)] = (byte)TileKind.Floor;
		sim.RecheckReachability();
	}

	// Puts goblin g to sleep where it stands with the given Sleep meter.
	static void LieDown(SimState s, int g, int sleep)
	{
		s.GoblinSleep[g] = (byte)sleep;
		GoblinTasks.BeginSleep(s, g, dropTile: -1, asleep: true);
	}

	static int TileOf(SimState s, int g) => s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]);

	// Runs tick by tick and asserts no two goblins ever sleep on one tile.
	static void RunAssertingOneSleeperPerTile(Simulation sim, int ticks)
	{
		var s = sim.State;
		for (var i = 0; i < ticks; i++)
		{
			RunTicks(sim, 1);
			var seen = new HashSet<int>();
			for (var g = 0; g < s.GoblinCount; g++)
				if (TaskOf(s, g) == GoblinTask.Sleeping)
					Assert.True(seen.Add(TileOf(s, g)), $"Tick {s.Tick}: two goblins sleep on tile {TileOf(s, g)}.");
		}
	}

	[Fact]
	public void Two_goblins_on_two_Heap_tiles_sleep_on_separate_tiles()
	{
		var sim = Crew(20, (16, 16), (16, 16));
		Heap(sim, 16, 12);
		Heap(sim, 20, 16);
		var s = sim.State;

		RunAssertingOneSleeperPerTile(sim, 60);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.NotEqual(TileOf(s, 0), TileOf(s, 1));
		Assert.All(new[] { 0, 1 }, g => Assert.Equal(RoomKind.SleepingHeap, s.RoomAt(TileOf(s, g))));
	}

	[Fact]
	public void Competing_arrivals_at_the_last_free_Heap_tile_leave_the_loser_the_floor()
	{
		var sim = Crew(20, (16, 14), (16, 18));
		Heap(sim, 16, 16);
		var s = sim.State;

		RunAssertingOneSleeperPerTile(sim, 60);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.NotEqual(TileOf(s, 0), TileOf(s, 1));
		// Both set out for the Heap at once; goblin 0 acts first each tick, so it lies down first and goblin 1 is left the floor.
		Assert.Equal(s.TileIndex(16, 16, 0), TileOf(s, 0));
		Assert.Equal(RoomKind.None, s.RoomAt(TileOf(s, 1)));
	}

	[Fact]
	public void A_goblin_prefers_a_farther_free_Heap_tile_to_a_nearer_occupied_one()
	{
		var sim = Crew(20, (16, 16), (16, 16));
		Heap(sim, 16, 16);
		Heap(sim, 22, 16);
		var s = sim.State;

		RunAssertingOneSleeperPerTile(sim, 80);

		Assert.Equal((16, 16), (s.GoblinX[0], s.GoblinY[0]));
		Assert.Equal((22, 16), (s.GoblinX[1], s.GoblinY[1]));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
	}

	[Fact]
	public void With_no_Heap_a_goblin_on_an_occupied_tile_takes_the_nearest_free_tile_with_ties_to_the_lowest_tile_index()
	{
		var sim = Crew(20, (16, 16), (16, 16));
		var s = sim.State;

		RunAssertingOneSleeperPerTile(sim, 30);

		Assert.Equal((16, 16), (s.GoblinX[0], s.GoblinY[0]));
		// Four free neighbours are one step away; (16, 15) has the lowest tile index.
		Assert.Equal((16, 15), (s.GoblinX[1], s.GoblinY[1]));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
	}

	[Fact]
	public void Heap_ties_go_to_the_lowest_tile_index()
	{
		var sim = Crew(20, (16, 16));
		Heap(sim, 18, 16);
		Heap(sim, 14, 16);
		Heap(sim, 16, 14);
		var s = sim.State;

		RunAssertingOneSleeperPerTile(sim, 40);

		Assert.Equal((16, 14), (s.GoblinX[0], s.GoblinY[0]));
	}

	[Fact]
	public void With_every_reachable_tile_occupied_a_goblin_waits_with_no_Sleep_restored_then_lies_down_when_a_tile_is_freed()
	{
		// One tile of room: goblin 0 sleeps on it (about to wake), goblin 1 has nowhere to lie down.
		var sim = Crew(20, (16, 16), (16, 16));
		Pocket(sim, (16, 16));
		var s = sim.State;
		LieDown(s, 0, 98);

		RunAssertingOneSleeperPerTile(sim, 3);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(GoblinTask.AwaitingBed, TaskOf(s, 1));
		Assert.True(s.GoblinSleep[1] <= 20);

		RunAssertingOneSleeperPerTile(sim, 60);

		Assert.NotEqual(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.True(s.GoblinSleep[1] > 20);
	}

	[Fact]
	public void A_waiting_goblin_never_displaces_a_sleeper_and_other_Needs_keep_draining()
	{
		var sim = Crew(20, (16, 16), (16, 16));
		Pocket(sim, (16, 16));
		var s = sim.State;
		LieDown(s, 0, 40);
		var grub = s.GoblinGrub[1];

		RunAssertingOneSleeperPerTile(sim, 100);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(GoblinTask.AwaitingBed, TaskOf(s, 1));
		Assert.True(s.GoblinSleep[1] <= 20);
		Assert.True(s.GoblinGrub[1] < grub);
	}

	// Goblin 0 sleeps on (16, 16), goblin 2 sleeps on (17, 16) with one unit of `supply` beside it; goblin 1 waits with Sleep 25.
	static Simulation WaiterWithSupply(Material supply)
	{
		var sim = Crew(25, (16, 16), (16, 16));
		Pocket(sim, (16, 16), (17, 16));
		var s = sim.State;
		s.GoblinCount = 3;
		(s.GoblinX[2], s.GoblinY[2], s.GoblinZ[2]) = (17, 16, 0);
		(s.GoblinGrub[2], s.GoblinGrog[2]) = (100, 100);
		LieDown(s, 2, 40);
		LieDown(s, 0, 40);
		s.AddItems(s.TileIndex(17, 16, 0), supply, 1);
		sim.RecheckReachability();
		RunAssertingOneSleeperPerTile(sim, 3);
		Assert.Equal(GoblinTask.AwaitingBed, TaskOf(s, 1));
		return sim;
	}

	[Theory]
	[InlineData(Material.Grub)]
	[InlineData(Material.Grog)]
	public void A_waiting_goblin_with_a_lower_Grub_or_Grog_Craving_goes_and_consumes_first(Material supply)
	{
		var sim = WaiterWithSupply(supply);
		var s = sim.State;

		if (supply == Material.Grub)
			s.GoblinGrub[1] = 10;
		else
			s.GoblinGrog[1] = 10;
		RunAssertingOneSleeperPerTile(sim, 40);

		Assert.True((supply == Material.Grub ? s.GoblinGrub[1] : s.GoblinGrog[1]) > 10);
		Assert.Equal(0, s.ItemCount(s.TileIndex(17, 16, 0), supply));
	}

	[Theory]
	[InlineData(Material.Grub)]
	[InlineData(Material.Grog)]
	public void A_waiting_goblins_Sleep_reaching_0_with_the_other_Need_already_at_0_collapses_rather_than_eating_or_drinking(Material supply)
	{
		var sim = WaiterWithSupply(supply);
		var s = sim.State;

		// Both meters are 0 and the supply is in reach: the ordinary tie-break would send the goblin for it.
		if (supply == Material.Grub)
			s.GoblinGrub[1] = 0;
		else
			s.GoblinGrog[1] = 0;
		s.GoblinSleep[1] = 0;
		RunAssertingOneSleeperPerTile(sim, 5);

		Assert.Equal(GoblinTask.AwaitingBed, TaskOf(s, 1));
		Assert.Equal(1, s.ItemCount(s.TileIndex(17, 16, 0), supply));
	}

	[Fact]
	public void Collapse_on_an_occupied_tile_moves_to_the_nearest_free_tile_and_sleeps_there()
	{
		var sim = Crew(20, (16, 16), (16, 16));
		var s = sim.State;
		s.GoblinSleep[1] = 0;

		RunAssertingOneSleeperPerTile(sim, 30);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.Equal((16, 15), (s.GoblinX[1], s.GoblinY[1]));
	}

	[Fact]
	public void Collapse_with_no_free_tile_waits_without_displacing_the_sleeper_and_sleeps_when_a_tile_frees()
	{
		var sim = Crew(20, (16, 16), (16, 16));
		Pocket(sim, (16, 16));
		var s = sim.State;
		LieDown(s, 0, 98);
		s.GoblinSleep[1] = 0;

		RunAssertingOneSleeperPerTile(sim, 3);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(GoblinTask.AwaitingBed, TaskOf(s, 1));
		Assert.Equal(0, s.GoblinSleep[1]);

		RunAssertingOneSleeperPerTile(sim, 60);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.True(s.GoblinSleep[1] > 0);
	}

	[Theory]
	[InlineData(Material.Grub)]
	[InlineData(Material.Grog)]
	public void Collapse_takes_priority_over_a_lower_Grub_or_Grog_Craving_while_waiting(Material supply)
	{
		var sim = Crew(20, (16, 16), (16, 16));
		Pocket(sim, (16, 16), (17, 16));
		var s = sim.State;
		s.AddItems(s.TileIndex(17, 16, 0), supply, 1);
		LieDown(s, 0, 40);
		s.GoblinSleep[1] = 0;
		if (supply == Material.Grub)
			s.GoblinGrub[1] = 5;
		else
			s.GoblinGrog[1] = 5;
		sim.RecheckReachability();

		RunAssertingOneSleeperPerTile(sim, 30);

		// Goblin 1 collapses on the free tile (17, 16) rather than consuming the supply there.
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.Equal(1, s.ItemCount(s.TileIndex(17, 16, 0), supply));
	}

	[Fact]
	public void A_collapsing_carrier_puts_its_unit_down_legally_then_finds_a_free_tile()
	{
		var sim = Crew(20, (16, 16), (16, 16));
		var s = sim.State;
		s.GoblinSleep[1] = 0;
		s.GoblinCarry[1] = (byte)Material.Dirt;
		var dirt = 1;

		RunAssertingOneSleeperPerTile(sim, 30);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.Equal((byte)Material.None, s.GoblinCarry[1]);
		var total = 0;
		for (var tile = 0; tile < s.Piles.Length; tile++)
			total += s.ItemCount(tile, Material.Dirt);
		Assert.Equal(dirt, total);
	}

	// Goblin 1 shares goblin 0's tile and is part-way through `task`, then its Sleep reaches 0.
	static Simulation CollapseDuringTask(GoblinTask task, bool freeTileElsewhere)
	{
		var sim = Crew(60, (16, 16), (16, 16));
		var s = sim.State;
		if (freeTileElsewhere)
			Pocket(sim, (16, 16), (17, 16));
		else
			Pocket(sim, (16, 16));
		LieDown(s, 0, 40);
		var here = s.TileIndex(16, 16, 0);
		switch (task)
		{
			case GoblinTask.Eating:
				s.AddItems(here, Material.Grub, 1);
				s.GoblinGrub[1] = 10;
				sim.RecheckReachability();
				GoblinTasks.BeginSeek(s, 1, -1, eating: true);
				break;
			case GoblinTask.Drinking:
				s.AddItems(here, Material.Grog, 1);
				s.GoblinGrog[1] = 10;
				sim.RecheckReachability();
				GoblinTasks.BeginSeek(s, 1, -1, eating: true, Material.Grog);
				break;
			case GoblinTask.SeekingHeap:
				GoblinTasks.BeginSleep(s, 1, -1, asleep: false);
				break;
		}
		Assert.Equal(task, TaskOf(s, 1));
		s.GoblinSleep[1] = 0;
		return sim;
	}

	[Theory]
	[InlineData(GoblinTask.Eating)]
	[InlineData(GoblinTask.Drinking)]
	[InlineData(GoblinTask.SeekingHeap)]
	public void Collapse_during_a_Need_on_an_occupied_tile_moves_to_the_free_tile_and_sleeps(GoblinTask task)
	{
		var sim = CollapseDuringTask(task, freeTileElsewhere: true);
		var s = sim.State;

		RunAssertingOneSleeperPerTile(sim, 20);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.Equal((17, 16), (s.GoblinX[1], s.GoblinY[1]));
	}

	[Theory]
	[InlineData(GoblinTask.Eating)]
	[InlineData(GoblinTask.Drinking)]
	[InlineData(GoblinTask.SeekingHeap)]
	public void Collapse_during_a_Need_on_an_occupied_tile_with_no_free_tile_waits(GoblinTask task)
	{
		var sim = CollapseDuringTask(task, freeTileElsewhere: false);
		var s = sim.State;

		RunAssertingOneSleeperPerTile(sim, 20);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(GoblinTask.AwaitingBed, TaskOf(s, 1));
	}

	[Fact]
	public void A_closer_goblin_that_starts_later_takes_the_Heap_and_the_farther_goblin_that_set_out_first_is_redirected()
	{
		var sim = Crew(100, (16, 22), (16, 18));
		Heap(sim, 16, 16);
		var s = sim.State;
		var heap = s.TileIndex(16, 16, 0);

		// Goblin 0, far off, sets out for the Heap first; goblin 1, nearer, only then gets tired.
		s.GoblinSleep[0] = 20;
		RunAssertingOneSleeperPerTile(sim, 1);
		Assert.Equal(GoblinTask.SeekingHeap, TaskOf(s, 0));
		s.GoblinSleep[1] = 20;
		var redirected = false;

		for (var i = 0; i < 80; i++)
		{
			RunAssertingOneSleeperPerTile(sim, 1);
			if (TaskOf(s, 1) == GoblinTask.Sleeping)
				redirected |= TaskOf(s, 0) == GoblinTask.SeekingHeap;
		}

		// No reservation: goblin 1 lay down first and goblin 0, still walking, then found the Heap taken and took the floor.
		Assert.True(redirected, "Goblin 0 was not still walking when goblin 1 took the Heap.");
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.Equal(heap, TileOf(s, 1));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.NotEqual(heap, TileOf(s, 0));
	}

	[Fact]
	public void A_collapsing_carrier_sharing_a_sleepers_full_Pile_tile_keeps_its_unit_until_a_legal_drop_exists_then_drops_it_on_that_tile_first()
	{
		var sim = Crew(20, (16, 16), (16, 16));
		var s = sim.State;
		var here = s.TileIndex(16, 16, 0);
		var next = s.TileIndex(17, 16, 0);
		Pocket(sim, (16, 16));
		sim.Apply(new PaintPile(0, 16, 16, 16, 16));
		s.AddItems(here, Material.Dirt, SimState.PileCapacity);
		LieDown(s, 0, 40);
		GoblinTasks.FinishPickUp(s, 1, Material.Dirt);
		s.GoblinSleep[1] = 0;

		RunAssertingOneSleeperPerTile(sim, 20);

		// The Pile is full and nothing else is walkable: the goblin keeps its unit and does not collapse.
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[1]);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 1));
		Assert.Equal(SimState.PileCapacity, s.ItemCount(here, Material.Dirt));

		s.TileKinds[next] = (byte)TileKind.Floor;
		sim.RecheckReachability();
		RunAssertingOneSleeperPerTile(sim, 1);

		// The unit went down on the one legal tile, before the goblin moved or lay down.
		Assert.Equal((byte)Material.None, s.GoblinCarry[1]);
		Assert.Equal(1, s.LooseCount(next, Material.Dirt));
		Assert.Equal(SimState.PileCapacity, s.ItemCount(here, Material.Dirt));
		Assert.NotEqual(GoblinTask.Sleeping, TaskOf(s, 1));

		RunAssertingOneSleeperPerTile(sim, 10);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.Equal(next, TileOf(s, 1));
	}

	[Fact]
	public void A_legacy_save_with_sleepers_stacked_on_one_tile_loads_runs_on_and_new_sleepers_avoid_the_occupied_tile()
	{
		var sim = Crew(100, (16, 16), (16, 16), (16, 16));
		var s = sim.State;
		// What a pre-#58 save holds: two goblins asleep on one tile.
		LieDown(s, 0, 40);
		LieDown(s, 1, 40);
		Assert.Equal(TileOf(s, 0), TileOf(s, 1));
		var restored = SimJson.Deserialize(SimJson.Serialize(sim));
		var r = restored.State;
		Assert.Null(GoblinTasks.FindViolation(r));
		Assert.Equal((GoblinTask.Sleeping, GoblinTask.Sleeping), (TaskOf(r, 0), TaskOf(r, 1)));

		// Goblin 2 tires on the same tile; the stack keeps sleeping and goblin 2 takes a different tile.
		r.GoblinSleep[2] = 20;
		RunTicks(restored, 30);

		Assert.Equal((GoblinTask.Sleeping, GoblinTask.Sleeping), (TaskOf(r, 0), TaskOf(r, 1)));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(r, 2));
		Assert.NotEqual(TileOf(r, 0), TileOf(r, 2));
		Assert.Equal(TileOf(r, 0), TileOf(r, 1));
		Assert.Null(GoblinTasks.FindViolation(r));
	}

	[Fact]
	public void A_save_taken_while_a_goblin_is_AwaitingBed_continues_exactly_like_the_uninterrupted_run()
	{
		var sim = Crew(20, (16, 16), (16, 16));
		Pocket(sim, (16, 16));
		var s = sim.State;
		LieDown(s, 0, 98);
		RunAssertingOneSleeperPerTile(sim, 3);
		Assert.Equal(GoblinTask.AwaitingBed, TaskOf(s, 1));

		var restored = SimJson.Deserialize(SimJson.Serialize(sim));
		Assert.Equal(GoblinTask.AwaitingBed, TaskOf(restored.State, 1));
		Assert.Null(GoblinTasks.FindViolation(restored.State));

		RunTicks(sim, 80);
		RunTicks(restored, 80);

		// The sleeper woke during the run, so the waiter lay down in both.
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
	}

	[Fact]
	public void Awake_goblins_walk_through_sleepers()
	{
		var sim = Crew(100, (16, 16), (18, 16));
		Pocket(sim, (16, 16), (17, 16), (18, 16), (19, 16), (20, 16));
		var s = sim.State;
		// Goblin 1 sleeps in the one-wide corridor at (18, 16); goblin 0 is hungry with the only Grub beyond it.
		(s.GoblinGrub[1], s.GoblinGrog[1]) = (100, 100);
		LieDown(s, 1, 60);
		s.AddItems(s.TileIndex(20, 16, 0), Material.Grub, 1);
		s.GoblinGrub[0] = 10;
		sim.RecheckReachability();
		var crossed = false;

		for (var i = 0; i < 80; i++)
		{
			RunAssertingOneSleeperPerTile(sim, 1);
			Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
			Assert.Equal((18, 16), (s.GoblinX[1], s.GoblinY[1]));
			crossed |= (s.GoblinX[0], s.GoblinY[0]) == (18, 16);
		}

		Assert.True(crossed, "Goblin 0 never stood on the sleeper's tile.");
		Assert.True(s.GoblinGrub[0] > 10);
		Assert.Equal(0, s.ItemCount(s.TileIndex(20, 16, 0), Material.Grub));
	}
}
