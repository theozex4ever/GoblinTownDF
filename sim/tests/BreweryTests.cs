using System.Text.Json;
using System.Text.Json.Nodes;
using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>P3's Brewery: painting it, and the Brew Job that takes one Fungus to a free Brewery tile and works it into one Grog.</summary>
public class BreweryTests
{
	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	static int At(SimState s, int x, int y, int z = 0) => s.TileIndex(x, y, z);

	static bool IsBrewTask(SimState s, int g) =>
		TaskOf(s, g) is GoblinTask.Gathering or GoblinTask.Taking or GoblinTask.Bringing or GoblinTask.Brewing;

	// A trimmed world of n Grunts with full meters on the open Surface, standing in a column from (10, 10).
	static Simulation World(int goblins = 1)
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		var s = sim.State;
		s.GoblinCount = goblins;
		for (var g = 0; g < goblins; g++)
		{
			s.GoblinX[g] = 10;
			s.GoblinY[g] = 10 + g;
			s.GoblinZ[g] = 0;
			s.GoblinGrub[g] = s.GoblinGrog[g] = s.GoblinSleep[g] = 100;
		}
		sim.RecheckReachability();
		return sim;
	}

	static void PaintBrewery(Simulation sim, int x, int y, int x1 = -1, int y1 = -1, int z = 0) =>
		sim.Apply(new PaintRoom(RoomKind.Brewery, z, x, y, x1 < 0 ? x : x1, y1 < 0 ? y : y1));

	static void PaintPile(Simulation sim, int x, int y, int x1 = -1, int y1 = -1, int z = 0) =>
		sim.Apply(new PaintPile(z, x, y, x1 < 0 ? x : x1, y1 < 0 ? y : y1));

	// A Pile with the given stock, painted first so the units are stored.
	static int StockPile(Simulation sim, int x, int y, int fungus = 0, int grog = 0, int z = 0)
	{
		var s = sim.State;
		PaintPile(sim, x, y, z: z);
		var tile = At(s, x, y, z);
		s.AddItems(tile, Material.Fungus, fungus);
		s.AddItems(tile, Material.Grog, grog);
		sim.RecheckReachability();
		return tile;
	}

	static int Total(SimState s, Material material)
	{
		var total = 0;
		for (var tile = 0; tile < s.TileKinds.Length; tile++)
			total += s.ItemCount(tile, material);
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.GoblinCarry[g] == (byte)material)
				total++;
		return total;
	}

	static void RunUntil(Simulation sim, Func<bool> done, int limit = 2000, string what = "the condition")
	{
		for (var i = 0; i < limit; i++)
		{
			if (done())
				return;
			RunTicks(sim, 1);
		}
		Assert.True(done(), $"{what} never came true in {limit} ticks.");
	}

	static void RunUntilTask(Simulation sim, int g, GoblinTask task, int limit = 2000) =>
		RunUntil(sim, () => TaskOf(sim.State, g) == task, limit, $"Goblin {g} never reached {task}");

	// A brewer on the Surface with a Brewery at (10, 10), one loose Fungus at (10, 10), and a distant Pile (30 Fungus) as the Grog sink.
	// The full Fungus stock leaves the loose unit no Pile to be hauled to, so only a Brew will take it.
	static Simulation LooseBrew(out int brewery, out int pile)
	{
		var sim = World();
		var s = sim.State;
		brewery = At(s, 10, 10);
		PaintBrewery(sim, 10, 10);
		pile = StockPile(sim, 30, 10, fungus: 30);
		s.AddItems(brewery, Material.Fungus, 1);
		sim.RecheckReachability();
		return sim;
	}

	// ---- Painting

	[Fact]
	public void Breweries_paint_on_Floor_only_one_Room_per_tile_and_right_drag_removes_only_Breweries()
	{
		var sim = World();
		var s = sim.State;
		s.TileKinds[At(s, 5, 5)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 6, 5)] = (byte)TileKind.OpenAir;
		PaintPile(sim, 7, 5);
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 8, 5, 8, 5));

		sim.Apply(new PaintRoom(RoomKind.Brewery, 0, 9, 5, 4, 5));
		Assert.Equal(RoomKind.None, s.RoomAt(At(s, 5, 5)));
		Assert.Equal(RoomKind.None, s.RoomAt(At(s, 6, 5)));
		Assert.Equal(RoomKind.Pile, s.RoomAt(At(s, 7, 5)));
		Assert.Equal(RoomKind.GrubPit, s.RoomAt(At(s, 8, 5)));
		Assert.Equal(RoomKind.Brewery, s.RoomAt(At(s, 9, 5)));
		Assert.Equal(RoomKind.Brewery, s.RoomAt(At(s, 4, 5)));
		Assert.False(s.IsPile(At(s, 9, 5)));

		// A Brewery works at any depth: it is no Fungus Farm.
		sim.Apply(new PaintRoom(RoomKind.Brewery, 1, 0, 0, 31, 31));
		Assert.Equal(RoomKind.None, s.RoomAt(At(s, 5, 5, 1)));

		// Removing takes Breweries only.
		sim.Apply(new RemoveRoom(RoomKind.Brewery, 0, 0, 0, 31, 31));
		Assert.Equal(RoomKind.None, s.RoomAt(At(s, 9, 5)));
		Assert.Equal(RoomKind.Pile, s.RoomAt(At(s, 7, 5)));
		Assert.Equal(RoomKind.GrubPit, s.RoomAt(At(s, 8, 5)));
	}

	[Fact]
	public void Painting_a_Pile_or_a_Heap_over_a_Brewery_skips_the_tile_and_Dig_stairs_clears_it()
	{
		var sim = World(2);
		var s = sim.State;
		var tile = At(s, 20, 20);
		s.GoblinX[1] = 21;
		s.GoblinY[1] = 20;
		PaintBrewery(sim, 20, 20);
		PaintPile(sim, 20, 20);
		sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 0, 20, 20, 20, 20));
		Assert.Equal(RoomKind.Brewery, s.RoomAt(tile));

		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 20, 20, 20, 20));
		RunUntil(sim, () => s.KindAt(tile) == TileKind.Stairs, what: "The stairs");
		Assert.Equal(RoomKind.None, s.RoomAt(tile));
	}

	// ---- The Brew

	[Fact]
	public void A_Brew_turns_exactly_one_loose_Fungus_into_one_Grog_in_20_working_ticks_loose_on_the_Brewery_tile()
	{
		var sim = LooseBrew(out var brewery, out var pile);
		var s = sim.State;

		// The goblin stands on the Fungus and the free Brewery tile: 5 ticks to take it, then the brew starts at once.
		RunTicks(sim, 4);
		Assert.Equal(GoblinTask.Taking, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Brewing, TaskOf(s, 0));
		Assert.Equal((byte)Material.Fungus, s.GoblinCarry[0]);
		Assert.Equal(0, s.ItemCount(brewery, Material.Fungus));
		Assert.Equal(brewery, s.GoblinJobTile[0]);
		Assert.Equal(0, s.GoblinWorkTicks[0]);

		RunTicks(sim, 19);
		Assert.Equal(GoblinTask.Brewing, TaskOf(s, 0));
		Assert.Equal(19, s.GoblinWorkTicks[0]);
		Assert.Equal(0, s.ItemCount(brewery, Material.Grog));

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(brewery, Material.Grog));
		Assert.False(s.IsPile(brewery));
		Assert.Equal(0, Total(s, Material.Fungus) - 30);
		Assert.Equal(30, s.StoredCount(pile, Material.Fungus));
		Assert.Equal(1, Total(s, Material.Grog));
	}

	[Fact]
	public void A_Brew_takes_its_Fungus_from_a_Pile_and_the_stored_count_falls_when_it_is_taken()
	{
		var sim = World();
		var s = sim.State;
		var pile = StockPile(sim, 13, 10, fungus: 3);
		PaintBrewery(sim, 7, 10);
		var brewery = At(s, 7, 10);

		RunUntilTask(sim, 0, GoblinTask.Taking);
		Assert.Equal(3, s.StoredCount(pile, Material.Fungus));
		Assert.Equal(pile, s.GoblinJobTile[0]);
		RunUntilTask(sim, 0, GoblinTask.Bringing);
		Assert.Equal(2, s.StoredCount(pile, Material.Fungus));
		Assert.Equal(brewery, s.GoblinJobTile[0]);
		Assert.Equal((byte)Material.Fungus, s.GoblinCarry[0]);
		Assert.Equal(3, Total(s, Material.Fungus));
		RunUntilTask(sim, 0, GoblinTask.Brewing);
		Assert.Equal(brewery, At(s, s.GoblinX[0], s.GoblinY[0]));
	}

	[Fact]
	public void The_Grog_is_hauled_to_the_nearest_Pile_and_nothing_is_invented_or_lost()
	{
		var sim = LooseBrew(out _, out var pile);
		var s = sim.State;
		var near = StockPile(sim, 14, 10);

		for (var i = 0; i < 600 && s.StoredCount(near, Material.Grog) + s.StoredCount(pile, Material.Grog) == 0; i++)
		{
			RunTicks(sim, 1);
			Assert.Equal(31, Total(s, Material.Fungus) + Total(s, Material.Grog));
		}

		Assert.Equal(1, s.StoredCount(near, Material.Grog));
		Assert.Equal(0, s.StoredCount(pile, Material.Grog));
		Assert.Equal(31, Total(s, Material.Fungus) + Total(s, Material.Grog));
	}

	[Fact]
	public void A_brewer_that_stands_on_neither_walks_to_the_Fungus_then_to_a_free_Brewery_tile()
	{
		var sim = World();
		var s = sim.State;
		StockPile(sim, 10, 16, fungus: 1);
		PaintBrewery(sim, 14, 10);

		RunUntilTask(sim, 0, GoblinTask.Gathering);
		Assert.Equal(At(s, 10, 16), s.GoblinJobTile[0]);
		RunUntilTask(sim, 0, GoblinTask.Taking);
		Assert.Equal((10, 16), (s.GoblinX[0], s.GoblinY[0]));
		RunUntilTask(sim, 0, GoblinTask.Bringing);
		Assert.Equal(At(s, 14, 10), s.GoblinJobTile[0]);
		RunUntilTask(sim, 0, GoblinTask.Brewing);
		Assert.Equal((14, 10), (s.GoblinX[0], s.GoblinY[0]));
	}

	[Fact]
	public void A_Starving_brewer_works_at_half_speed_like_any_other_work()
	{
		var sim = LooseBrew(out var brewery, out _);
		var s = sim.State;
		s.GoblinGrub[0] = 0;

		RunUntilTask(sim, 0, GoblinTask.Brewing);
		var brewing = 0;
		while (TaskOf(s, 0) == GoblinTask.Brewing)
		{
			RunTicks(sim, 1);
			brewing++;
		}

		Assert.Equal(2 * ProductionRules.BrewTicks, brewing);
		Assert.Equal(1, s.LooseCount(brewery, Material.Grog));
	}

	// ---- Claims

	[Fact]
	public void Two_goblins_never_claim_the_same_unit_of_Fungus()
	{
		var sim = World(2);
		var s = sim.State;
		var pile = StockPile(sim, 16, 10, fungus: 1);
		PaintBrewery(sim, 8, 10);
		PaintBrewery(sim, 8, 12);

		for (var i = 0; i < 400; i++)
		{
			RunTicks(sim, 1);
			Assert.True(GoblinTasks.BrewClaims(s, pile, s.GoblinCount) <= 1, $"Tick {s.Tick}: two goblins claim one Fungus.");
			Assert.Equal(1, Total(s, Material.Fungus) + Total(s, Material.Grog));
		}

		Assert.Equal(1, Total(s, Material.Grog));
		Assert.Equal(0, Total(s, Material.Fungus));
	}

	[Fact]
	public void A_tile_holding_two_Fungus_is_claimed_by_two_goblins_at_most_and_each_gets_its_own_unit()
	{
		var sim = World(3);
		var s = sim.State;
		var pile = StockPile(sim, 16, 10, fungus: 2);
		PaintBrewery(sim, 8, 10, 8, 12);

		var mostAtOnce = 0;
		for (var i = 0; i < 200; i++)
		{
			RunTicks(sim, 1);
			mostAtOnce = Math.Max(mostAtOnce, GoblinTasks.BrewClaims(s, pile, s.GoblinCount));
			Assert.True(GoblinTasks.BrewClaims(s, pile, s.GoblinCount) <= s.ItemCount(pile, Material.Fungus));
		}

		Assert.Equal(2, mostAtOnce);
		Assert.Equal(2, Total(s, Material.Grog));
		Assert.Equal(0, Total(s, Material.Fungus));
	}

	[Fact]
	public void One_Brewery_tile_never_has_two_brewers_and_a_single_tile_serves_them_one_after_another()
	{
		var sim = World(3);
		var s = sim.State;
		StockPile(sim, 16, 10, fungus: 3);
		PaintBrewery(sim, 8, 10);

		for (var i = 0; i < 400; i++)
		{
			RunTicks(sim, 1);
			var committed = Enumerable.Range(0, 3).Count(g => IsBrewTask(s, g));
			Assert.True(committed <= 1, $"Tick {s.Tick}: {committed} goblins hold the one Brewery tile.");
		}

		Assert.Equal(3, Total(s, Material.Grog));
	}

	[Fact]
	public void Two_Brewery_tiles_serve_two_brewers_at_once()
	{
		var sim = World(3);
		var s = sim.State;
		StockPile(sim, 16, 10, fungus: 6);
		PaintBrewery(sim, 8, 10, 8, 11);

		var most = 0;
		for (var i = 0; i < 300; i++)
		{
			RunTicks(sim, 1);
			most = Math.Max(most, Enumerable.Range(0, 3).Count(g => TaskOf(s, g) is GoblinTask.Brewing));
			var tiles = Enumerable.Range(0, 3).Where(g => TaskOf(s, g) is GoblinTask.Bringing or GoblinTask.Brewing).Select(g => s.GoblinJobTile[g]).ToList();
			Assert.Equal(tiles.Count, tiles.Distinct().Count());
		}

		Assert.Equal(2, most);
	}

	[Fact]
	public void A_brewer_and_a_hauler_never_claim_the_same_Fungus()
	{
		var sim = World(2);
		var s = sim.State;
		var unit = At(s, 13, 10);
		s.AddItems(unit, Material.Fungus, 1);
		StockPile(sim, 20, 10);
		PaintBrewery(sim, 7, 10);

		for (var i = 0; i < 400; i++)
		{
			RunTicks(sim, 1);
			Assert.True(GoblinTasks.FetchClaims(s, unit, s.GoblinCount) + GoblinTasks.BrewClaims(s, unit, s.GoblinCount) <= 1, $"Tick {s.Tick}: the one unit has two claims.");
			Assert.Equal(1, Total(s, Material.Fungus) + Total(s, Material.Grog));
		}
	}

	// ---- Waiting

	[Fact]
	public void A_Brew_waits_greyed_while_Grog_has_no_Pile_with_room_and_resumes_when_room_appears()
	{
		var sim = World();
		var s = sim.State;
		var pile = StockPile(sim, 16, 10, fungus: 30, grog: 30);
		PaintBrewery(sim, 8, 10);
		var brewery = At(s, 8, 10);
		Assert.True(BrewSystem.IsWaiting(s, brewery));

		for (var i = 0; i < 200; i++)
		{
			RunTicks(sim, 1);
			Assert.False(IsBrewTask(s, 0));
		}
		Assert.Equal(30, s.StoredCount(pile, Material.Fungus));

		// A second Pile gives Grog somewhere to go.
		PaintPile(sim, 24, 24);
		Assert.False(BrewSystem.IsWaiting(s, brewery));
		RunUntilTask(sim, 0, GoblinTask.Gathering, limit: 50);
		RunUntil(sim, () => s.StoredCount(pile, Material.Fungus) == 29, limit: 100, what: "The Fungus being taken");
	}

	[Fact]
	public void A_Brew_resumes_when_a_Pile_gains_room_for_Grog()
	{
		var sim = World();
		var s = sim.State;
		var pile = StockPile(sim, 16, 10, fungus: 30, grog: 30);
		PaintBrewery(sim, 8, 10);
		RunTicks(sim, 100);
		Assert.False(IsBrewTask(s, 0));

		// Grog is drunk, or hauled off, elsewhere; the Pile has room for one more and the caches follow.
		s.RemoveItems(pile, Material.Grog, 1);
		sim.RecheckReachability();
		RunUntilTask(sim, 0, GoblinTask.Gathering, limit: 50);
	}

	[Fact]
	public void A_Brew_waits_with_no_Pile_at_all_and_a_removed_Pile_makes_a_waiting_Brew_again()
	{
		var sim = World();
		var s = sim.State;
		var fungus = At(s, 12, 10);
		s.AddItems(fungus, Material.Fungus, 1);
		PaintBrewery(sim, 8, 10);
		RunTicks(sim, 100);
		Assert.False(IsBrewTask(s, 0));
		Assert.True(BrewSystem.IsWaiting(s, At(s, 8, 10)));

		StockPile(sim, 24, 24, grog: 30);
		RunTicks(sim, 100);
		Assert.False(IsBrewTask(s, 0));
		Assert.True(BrewSystem.IsWaiting(s, At(s, 8, 10)));
	}

	[Fact]
	public void A_Brew_in_a_region_with_no_Grog_Pile_waits_even_while_another_region_has_one_and_resumes_when_the_regions_join()
	{
		var sim = World(2);
		var s = sim.State;
		// A pocket on z1 holding goblin 1, the Brewery and the Fungus; the only Pile is on the Surface.
		for (var x = 5; x <= 9; x++)
			s.TileKinds[At(s, x, 5, 1)] = (byte)TileKind.Floor;
		s.GoblinX[1] = 6;
		s.GoblinY[1] = 5;
		s.GoblinZ[1] = 1;
		sim.RecheckReachability();
		PaintBrewery(sim, 8, 5, z: 1);
		s.AddItems(At(s, 9, 5, 1), Material.Fungus, 1);
		StockPile(sim, 16, 16);
		sim.RecheckReachability();
		Assert.True(BrewSystem.IsWaiting(s, At(s, 8, 5, 1)));

		for (var i = 0; i < 150; i++)
		{
			RunTicks(sim, 1);
			Assert.False(IsBrewTask(s, 0) || IsBrewTask(s, 1));
		}

		s.TileKinds[At(s, 5, 5, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 5, 5, 1)] = (byte)TileKind.Stairs;
		sim.RecheckReachability();
		Assert.False(BrewSystem.IsWaiting(s, At(s, 8, 5, 1)));
		RunUntil(sim, () => Total(s, Material.Grog) == 1, what: "The Grog");
	}

	[Fact]
	public void A_Brewery_no_goblin_can_reach_the_Fungus_from_is_not_used()
	{
		var sim = World(2);
		var s = sim.State;
		// Goblin 1 is walled into a pocket on z1 with the only Brewery; the Fungus and the Pile are on the Surface.
		for (var x = 5; x <= 7; x++)
			s.TileKinds[At(s, x, 5, 1)] = (byte)TileKind.Floor;
		s.GoblinX[1] = 6;
		s.GoblinY[1] = 5;
		s.GoblinZ[1] = 1;
		sim.RecheckReachability();
		PaintBrewery(sim, 7, 5, z: 1);
		StockPile(sim, 16, 10, fungus: 3);

		for (var i = 0; i < 150; i++)
		{
			RunTicks(sim, 1);
			Assert.False(IsBrewTask(s, 0) || IsBrewTask(s, 1));
		}
		Assert.Equal(3, Total(s, Material.Fungus));

		s.TileKinds[At(s, 5, 5, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 5, 5, 1)] = (byte)TileKind.Stairs;
		sim.RecheckReachability();
		RunUntil(sim, () => Total(s, Material.Grog) >= 1, what: "A brewed Grog");
	}

	// ---- One job list

	[Fact]
	public void A_Brew_is_the_nearest_Job_by_path_and_a_wall_makes_a_near_Fungus_far()
	{
		var sim = World();
		var s = sim.State;
		// A wall at x = 12 from y = 5 to 15 puts the Pile at (13, 10) fifteen steps away, though it is three tiles off.
		for (var y = 5; y <= 15; y++)
			s.TileKinds[At(s, 12, y)] = (byte)TileKind.Solid;
		var behindWall = StockPile(sim, 13, 10, fungus: 1);
		var open = StockPile(sim, 10, 15, fungus: 1);
		PaintBrewery(sim, 8, 10);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Gathering, TaskOf(s, 0));
		Assert.Equal(open, s.GoblinJobTile[0]);
		Assert.NotEqual(behindWall, s.GoblinJobTile[0]);
	}

	[Fact]
	public void Equally_near_Fungus_goes_to_the_lowest_tile_index()
	{
		var sim = World();
		var s = sim.State;
		StockPile(sim, 10, 13, fungus: 1);
		var high = At(s, 10, 13);
		var low = StockPile(sim, 10, 7, fungus: 1);
		PaintBrewery(sim, 8, 10);

		RunTicks(sim, 1);

		Assert.Equal(low, s.GoblinJobTile[0]);
		Assert.True(low < high);
	}

	[Fact]
	public void A_Brew_competes_with_digs_and_hauls_on_one_list_nearest_first()
	{
		// Fungus three tiles away, a dig five away: the Brew.
		var brewNear = World();
		StockPile(brewNear, 10, 13, fungus: 1);
		PaintBrewery(brewNear, 6, 10);
		brewNear.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 16, 10, 16));
		RunTicks(brewNear, 1);
		Assert.Equal(GoblinTask.Gathering, TaskOf(brewNear.State, 0));

		// A dig three away, the Fungus five away: the dig.
		var digNear = World();
		StockPile(digNear, 10, 15, fungus: 1);
		PaintBrewery(digNear, 6, 10);
		digNear.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 14, 10, 14));
		RunTicks(digNear, 1);
		Assert.Equal(GoblinTask.Walking, TaskOf(digNear.State, 0));

		// A loose unit of dirt two away, the Fungus five away: the Haul.
		var haulNear = World();
		var s = haulNear.State;
		StockPile(haulNear, 10, 15, fungus: 1);
		PaintBrewery(haulNear, 6, 10);
		s.AddItems(At(s, 12, 10), Material.Dirt, 1);
		haulNear.RecheckReachability();
		RunTicks(haulNear, 1);
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 0));
	}

	[Fact]
	public void Fungus_on_the_floor_and_Fungus_in_a_Pile_are_both_sources()
	{
		var sim = World(2);
		var s = sim.State;
		var floor = At(s, 13, 10);
		s.AddItems(floor, Material.Fungus, 1);
		var pile = StockPile(sim, 10, 16, fungus: 30);
		PaintBrewery(sim, 6, 10, 6, 11);

		RunTicks(sim, 1);

		Assert.Equal(floor, s.GoblinJobTile[0]);
		Assert.Equal(pile, s.GoblinJobTile[1]);
		Assert.Equal(GoblinTask.Gathering, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Gathering, TaskOf(s, 1));
	}

	// ---- Taking out of a full Pile

	[Fact]
	public void Taking_Fungus_from_a_full_Pile_lets_a_Haul_waiting_on_that_room_resume()
	{
		var sim = World();
		var s = sim.State;
		var pile = StockPile(sim, 11, 10, fungus: 30);
		var stranded = At(s, 25, 25);
		s.AddItems(stranded, Material.Fungus, 1);
		PaintBrewery(sim, 8, 10);
		sim.RecheckReachability();
		Assert.False(s.IsHaulReachable(stranded, Material.Fungus));

		RunUntilTask(sim, 0, GoblinTask.Bringing);

		Assert.Equal(29, s.StoredCount(pile, Material.Fungus));
		Assert.True(s.HasStorageRoom(stranded, Material.Fungus));
		Assert.True(s.IsHaulReachable(stranded, Material.Fungus));
	}

	// ---- Interruption

	[Fact]
	public void A_Craving_interrupts_a_brewer_it_loses_the_progress_and_the_Fungus_drops_loose()
	{
		var sim = LooseBrew(out var brewery, out _);
		var s = sim.State;
		var drink = At(s, 20, 20);
		s.AddItems(drink, Material.Grog, 1);
		sim.RecheckReachability();
		RunUntilTask(sim, 0, GoblinTask.Brewing);
		RunTicks(sim, 10);
		Assert.Equal(10, s.GoblinWorkTicks[0]);

		s.GoblinGrog[0] = 10;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.SeekingGrog, TaskOf(s, 0));
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(brewery, Material.Fungus));
		Assert.Equal(0, s.ItemCount(brewery, Material.Grog));
		Assert.Equal(31, Total(s, Material.Fungus) + Total(s, Material.Grog) - 1);

		// After the drink the brew starts again from nothing: the same 20 working ticks.
		RunUntilTask(sim, 0, GoblinTask.Brewing);
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		RunTicks(sim, 19);
		Assert.Equal(GoblinTask.Brewing, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(1, s.LooseCount(brewery, Material.Grog));
	}

	[Fact]
	public void An_interrupted_carrier_standing_on_a_full_Fungus_Pile_drops_its_Fungus_elsewhere_and_never_overfills()
	{
		var sim = World();
		var s = sim.State;
		// A corridor along y = 10 from x = 11 to 13, so the walk to the Brewery crosses the full Pile at (12, 10).
		for (var x = 11; x <= 13; x++)
		{
			s.TileKinds[At(s, x, 9)] = (byte)TileKind.Solid;
			s.TileKinds[At(s, x, 11)] = (byte)TileKind.Solid;
		}
		s.GoblinX[0] = 14;
		s.GoblinY[0] = 10;
		var pile = StockPile(sim, 12, 10, fungus: 30);
		PaintBrewery(sim, 8, 10);
		var loose = At(s, 14, 10);
		s.AddItems(loose, Material.Fungus, 1);
		s.AddItems(At(s, 16, 10), Material.Grog, 1);
		sim.RecheckReachability();

		RunUntil(sim, () => TaskOf(s, 0) == GoblinTask.Bringing && At(s, s.GoblinX[0], s.GoblinY[0]) == pile, what: "The brewer crossing the Pile");
		s.GoblinGrog[0] = 10;
		RunTicks(sim, 1);

		Assert.Equal(30, s.ItemCount(pile, Material.Fungus));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(At(s, 11, 10), Material.Fungus));
		Assert.Equal(31, Total(s, Material.Fungus));
		Assert.Equal(GoblinTask.SeekingGrog, TaskOf(s, 0));
	}

	[Fact]
	public void A_Collapse_takes_a_brewer_off_the_brew_and_the_Fungus_drops_loose()
	{
		var sim = LooseBrew(out var brewery, out _);
		var s = sim.State;
		RunUntilTask(sim, 0, GoblinTask.Brewing);
		RunTicks(sim, 8);

		s.GoblinSleep[0] = 0;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(brewery, Material.Fungus));
	}

	[Fact]
	public void A_Collapse_while_gathering_or_bringing_loses_nothing()
	{
		var sim = World();
		var s = sim.State;
		var pile = StockPile(sim, 10, 16, fungus: 2);
		PaintBrewery(sim, 16, 10);
		RunUntilTask(sim, 0, GoblinTask.Bringing);
		RunTicks(sim, 4);
		var before = Total(s, Material.Fungus);

		s.GoblinSleep[0] = 0;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(before, Total(s, Material.Fungus));
		Assert.Equal(2, Total(s, Material.Fungus));
		Assert.True(s.ItemCount(pile, Material.Fungus) <= SimState.PileCapacity);
	}

	// ---- The world changes under a brewer

	[Fact]
	public void Removing_the_Brewery_under_a_brewer_sends_it_carrying_its_Fungus_to_a_Pile()
	{
		var sim = World();
		var s = sim.State;
		var pile = StockPile(sim, 13, 10, fungus: 2);
		PaintBrewery(sim, 7, 10);
		RunUntilTask(sim, 0, GoblinTask.Brewing);
		RunTicks(sim, 6);

		sim.Apply(new RemoveRoom(RoomKind.Brewery, 0, 7, 10, 7, 10));

		Assert.Null(GoblinTasks.FindViolation(s));
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Fungus, s.GoblinCarry[0]);
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Equal(-1, s.GoblinJobTile[0]);
		RunUntil(sim, () => s.StoredCount(pile, Material.Fungus) == 2 && TaskOf(s, 0) == GoblinTask.Idle, what: "The Fungus going back to the Pile");
		Assert.Equal(0, Total(s, Material.Grog));
	}

	[Fact]
	public void Removing_the_destination_Brewery_while_the_brewer_walks_to_it_sends_it_carrying()
	{
		var sim = World();
		var s = sim.State;
		StockPile(sim, 10, 16, fungus: 2);
		PaintBrewery(sim, 22, 10);
		RunUntilTask(sim, 0, GoblinTask.Bringing);
		RunTicks(sim, 3);

		sim.Apply(new RemoveRoom(RoomKind.Brewery, 0, 22, 10, 22, 10));

		Assert.Null(GoblinTasks.FindViolation(s));
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal(2, Total(s, Material.Fungus));
		RunTicks(sim, 400);
		Assert.Equal(2, Total(s, Material.Fungus));
	}

	[Fact]
	public void Digging_stairs_through_the_Brewery_under_a_brewer_clears_it_and_the_brewer_carries_on_carrying()
	{
		var sim = World(2);
		var s = sim.State;
		var brewery = At(s, 7, 10);
		var pile = StockPile(sim, 13, 10);
		PaintBrewery(sim, 7, 10);
		// Goblin 0 is Starving, so its brew takes 40 ticks, long enough for the digger beside it to finish first.
		s.GoblinGrub[0] = 0;
		s.GoblinX[1] = 7;
		s.GoblinY[1] = 11;
		// The Fungus appears on an even tick, when the Starving goblin 0 acts and claims it before goblin 1.
		RunTicks(sim, 1);
		s.AddItems(pile, Material.Fungus, 2);
		sim.RecheckReachability();
		RunUntilTask(sim, 0, GoblinTask.Brewing);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 7, 10, 7, 10));

		RunUntil(sim, () => s.KindAt(brewery) == TileKind.Stairs, what: "The stairs");

		Assert.Equal(RoomKind.None, s.RoomAt(brewery));
		Assert.NotEqual(GoblinTask.Brewing, TaskOf(s, 0));
		Assert.Equal(2, Total(s, Material.Fungus));
		Assert.Equal(0, Total(s, Material.Grog));
	}

	[Fact]
	public void Removing_the_Pile_a_gatherer_walks_to_leaves_the_Fungus_loose_and_still_claimed()
	{
		var sim = World();
		var s = sim.State;
		var pile = StockPile(sim, 10, 17, fungus: 1);
		StockPile(sim, 30, 30);
		PaintBrewery(sim, 8, 10);
		RunUntilTask(sim, 0, GoblinTask.Gathering);

		sim.Apply(new RemovePile(0, 10, 17, 10, 17));

		Assert.Null(GoblinTasks.FindViolation(s));
		Assert.Equal(1, s.LooseCount(pile, Material.Fungus));
		RunUntil(sim, () => Total(s, Material.Grog) == 1, what: "The Grog");
	}

	[Fact]
	public void A_gatherer_whose_source_shrinks_is_freed_and_the_higher_claimant_goes_first()
	{
		var sim = World(2);
		var s = sim.State;
		var pile = StockPile(sim, 20, 10, fungus: 2);
		PaintBrewery(sim, 8, 10, 8, 11);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Gathering, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Gathering, TaskOf(s, 1));

		// Something else took a unit: the lower-numbered claim holds, the other has no unit left.
		s.RemoveItems(pile, Material.Fungus, 1);
		RoomRules.Settle(s);

		Assert.Null(GoblinTasks.FindViolation(s));
		Assert.Equal(GoblinTask.Gathering, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 1));
		Assert.Equal(-1, s.GoblinJobTile[1]);

		s.RemoveItems(pile, Material.Fungus, 1);
		RoomRules.Settle(s);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void A_hauler_who_picks_up_the_Fungus_a_brewer_is_gathering_frees_the_brewer()
	{
		var sim = World(2);
		var s = sim.State;
		var tile = At(s, 14, 10);
		s.AddItems(tile, Material.Fungus, 1);
		StockPile(sim, 30, 10, fungus: 30);
		PaintBrewery(sim, 6, 10);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Gathering, TaskOf(s, 0));
		Assert.Equal(tile, s.GoblinJobTile[0]);

		// A stray hauler standing on the tile takes the unit (the Pile is full, so it is simulated by the removal rule alone).
		s.RemoveItems(tile, Material.Fungus, 1);
		RoomRules.Settle(s);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	// ---- Task rules

	static SimState Hand(out int pile, out int brewery)
	{
		var sim = World(2);
		var s = sim.State;
		pile = StockPile(sim, 16, 10, fungus: 1);
		PaintBrewery(sim, 8, 10);
		brewery = At(s, 8, 10);
		s.GoblinX[0] = 8;
		s.GoblinY[0] = 10;
		s.GoblinX[1] = 9;
		s.GoblinY[1] = 10;
		return s;
	}

	[Fact]
	public void FindViolation_accepts_each_brew_Task_and_rejects_a_claim_on_Fungus_that_is_not_there()
	{
		var s = Hand(out var pile, out var brewery);
		s.GoblinTask[0] = (byte)GoblinTask.Gathering;
		s.GoblinJobTile[0] = pile;
		Assert.Null(GoblinTasks.FindViolation(s));

		s.GoblinJobTile[0] = brewery;
		Assert.NotNull(GoblinTasks.FindViolation(s));

		s.GoblinJobTile[0] = pile;
		s.GoblinX[0] = 16;
		s.GoblinY[0] = 10;
		s.GoblinTask[0] = (byte)GoblinTask.Taking;
		s.GoblinWorkTicks[0] = 4;
		Assert.Null(GoblinTasks.FindViolation(s));
		s.GoblinWorkTicks[0] = 5;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinWorkTicks[0] = 0;
		s.GoblinX[0] = 15;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_rejects_more_claims_than_the_tile_holds_Fungus_across_brewers_and_haulers()
	{
		var s = Hand(out var pile, out _);
		s.GoblinTask[0] = (byte)GoblinTask.Gathering;
		s.GoblinJobTile[0] = pile;
		s.GoblinTask[1] = (byte)GoblinTask.Gathering;
		s.GoblinJobTile[1] = pile;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.AddItems(pile, Material.Fungus, 1);
		Assert.Null(GoblinTasks.FindViolation(s));

		// A loose unit claimed by a hauler and a brewer: one unit, two claims.
		var loose = At(s, 12, 12);
		s.AddItems(loose, Material.Fungus, 1);
		s.GoblinJobTile[0] = loose;
		s.GoblinTask[1] = (byte)GoblinTask.Fetching;
		s.GoblinJobTile[1] = loose;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_holds_a_bringing_or_brewing_goblin_to_Fungus_in_hand_and_a_Brewery_of_its_own()
	{
		var s = Hand(out _, out var brewery);
		s.GoblinTask[0] = (byte)GoblinTask.Bringing;
		s.GoblinJobTile[0] = brewery;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinCarry[0] = (byte)Material.Fungus;
		Assert.Null(GoblinTasks.FindViolation(s));
		s.GoblinCarry[0] = (byte)Material.Grub;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinCarry[0] = (byte)Material.Fungus;

		s.GoblinJobTile[0] = At(s, 9, 10);
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinJobTile[0] = brewery;

		s.GoblinTask[1] = (byte)GoblinTask.Bringing;
		s.GoblinCarry[1] = (byte)Material.Fungus;
		s.GoblinJobTile[1] = brewery;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinTask[1] = (byte)GoblinTask.Idle;
		s.GoblinCarry[1] = (byte)Material.None;
		s.GoblinJobTile[1] = -1;

		s.GoblinTask[0] = (byte)GoblinTask.Brewing;
		s.GoblinWorkTicks[0] = (byte)(ProductionRules.BrewTicks - 1);
		Assert.Null(GoblinTasks.FindViolation(s));
		s.GoblinWorkTicks[0] = (byte)ProductionRules.BrewTicks;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinWorkTicks[0] = 3;
		s.GoblinX[0] = 9;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void The_Task_helpers_name_the_brew_Tasks()
	{
		Assert.True(GoblinTasks.IsBrewSourceTask(GoblinTask.Gathering));
		Assert.True(GoblinTasks.IsBrewSourceTask(GoblinTask.Taking));
		Assert.False(GoblinTasks.IsBrewSourceTask(GoblinTask.Brewing));
		Assert.True(GoblinTasks.IsBreweryTask(GoblinTask.Bringing));
		Assert.True(GoblinTasks.IsBreweryTask(GoblinTask.Brewing));
		Assert.False(GoblinTasks.IsBreweryTask(GoblinTask.Taking));
	}

	// ---- Saves and replays

	static Simulation Busy(int seed)
	{
		var sim = Simulation.NewWorld(seed);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 20, 16, 21, 16));
		sim.Apply(new PaintRoom(RoomKind.Brewery, 0, 12, 20, 13, 20));
		s.AddItems(At(s, 14, 22), Material.Fungus, 3);
		s.AddItems(At(s, 22, 22), Material.Fungus, 2);
		sim.RecheckReachability();
		return sim;
	}

	[Fact]
	public void A_save_with_goblins_mid_brew_round_trips_and_replays_identically()
	{
		var original = Busy(seed: 7);
		var s = original.State;
		var seen = new HashSet<GoblinTask>();
		string? json = null;
		Simulation? restored = null;
		for (var i = 0; i < 600; i++)
		{
			RunTicks(original, 1);
			for (var g = 0; g < s.GoblinCount; g++)
				if (IsBrewTask(s, g))
					seen.Add(TaskOf(s, g));
			// Once all four brew Tasks have been seen, the save is taken with goblins in the middle of some of them.
			if (seen.Count == 4 && Enumerable.Range(0, s.GoblinCount).Any(g => TaskOf(s, g) is GoblinTask.Brewing or GoblinTask.Bringing))
			{
				json = SimJson.Serialize(original);
				restored = SimJson.Deserialize(json);
				break;
			}
		}

		Assert.NotNull(json);
		Assert.Equal(json, SimJson.Serialize(restored!));
		for (var i = 0; i < 800; i++)
		{
			RunTicks(original, 1);
			RunTicks(restored!, 1);
		}
		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored!));
	}

	[Fact]
	public void Each_brew_Task_survives_a_save_taken_inside_it()
	{
		foreach (var task in new[] { GoblinTask.Gathering, GoblinTask.Taking, GoblinTask.Bringing, GoblinTask.Brewing })
		{
			var sim = World();
			StockPile(sim, 10, 16, fungus: 2);
			PaintBrewery(sim, 16, 10);
			RunUntilTask(sim, 0, task);
			RunTicks(sim, 1);
			if (!IsBrewTask(sim.State, 0))
				continue;
			var copy = SimJson.Deserialize(SimJson.Serialize(sim));
			Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(copy));
			RunTicks(sim, 120);
			RunTicks(copy, 120);
			Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(copy));
		}
	}

	static void AssertRejected(Action<SimState> corrupt, GoblinTask task = GoblinTask.Brewing)
	{
		var sim = World(2);
		var s = sim.State;
		var pile = StockPile(sim, 16, 10, fungus: 1);
		PaintBrewery(sim, 8, 10);
		s.GoblinX[0] = 8;
		s.GoblinY[0] = 10;
		s.GoblinX[1] = 9;
		s.GoblinY[1] = 10;
		s.GoblinTask[0] = (byte)task;
		s.GoblinCarry[0] = (byte)(task is GoblinTask.Brewing or GoblinTask.Bringing ? Material.Fungus : Material.None);
		s.GoblinJobTile[0] = task is GoblinTask.Brewing or GoblinTask.Bringing ? At(s, 8, 10) : pile;
		if (task is GoblinTask.Gathering or GoblinTask.Taking)
		{
			s.GoblinX[0] = 16;
			s.GoblinY[0] = 10;
		}
		// The uncorrupted state is a valid save.
		SimJson.Deserialize(SimJson.Serialize(sim));
		corrupt(s);
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Fact]
	public void A_save_with_a_brewer_holding_nothing_or_the_wrong_thing_is_rejected()
	{
		AssertRejected(s => s.GoblinCarry[0] = (byte)Material.None);
		AssertRejected(s => s.GoblinCarry[0] = (byte)Material.Grog);
		AssertRejected(s => s.GoblinCarry[0] = (byte)Material.None, GoblinTask.Bringing);
	}

	[Fact]
	public void A_save_with_a_brewer_off_its_Brewery_or_on_a_tile_that_is_none_is_rejected()
	{
		AssertRejected(s => s.GoblinJobTile[0] = At(s, 9, 10));
		AssertRejected(s => s.GoblinX[0] = 9);
		AssertRejected(s => s.Rooms[At(s, 8, 10)] = (byte)RoomKind.None);
		AssertRejected(s => s.GoblinJobTile[0] = At(s, 9, 10), GoblinTask.Bringing);
	}

	[Fact]
	public void A_save_with_two_brewers_on_one_Brewery_tile_or_overrun_work_ticks_is_rejected()
	{
		AssertRejected(s =>
		{
			s.GoblinTask[1] = (byte)GoblinTask.Bringing;
			s.GoblinCarry[1] = (byte)Material.Fungus;
			s.GoblinJobTile[1] = At(s, 8, 10);
		});
		AssertRejected(s => s.GoblinWorkTicks[0] = (byte)ProductionRules.BrewTicks);
		AssertRejected(s => s.GoblinWorkTicks[0] = 5, GoblinTask.Taking);
	}

	[Fact]
	public void A_save_with_a_gatherer_on_Fungus_that_is_not_there_or_over_claimed_is_rejected()
	{
		AssertRejected(s => s.RemoveItems(s.GoblinJobTile[0], Material.Fungus, 1), GoblinTask.Gathering);
		AssertRejected(s => s.RemoveItems(s.GoblinJobTile[0], Material.Fungus, 1), GoblinTask.Taking);
		AssertRejected(s =>
		{
			s.GoblinTask[1] = (byte)GoblinTask.Gathering;
			s.GoblinJobTile[1] = s.GoblinJobTile[0];
		}, GoblinTask.Gathering);
		AssertRejected(s => s.GoblinCarry[0] = (byte)Material.Fungus, GoblinTask.Taking);
	}

	[Theory]
	[InlineData(RoomKind.Brewery, TileKind.Solid)]
	[InlineData(RoomKind.Brewery, TileKind.Stairs)]
	[InlineData(RoomKind.Brewery, TileKind.OpenAir)]
	public void A_save_with_a_Brewery_on_a_tile_that_is_not_Floor_is_rejected(RoomKind room, TileKind kind)
	{
		var sim = World();
		var s = sim.State;
		var tile = At(s, 20, 20);
		s.Rooms[tile] = (byte)room;
		s.TileKinds[tile] = (byte)kind;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Fact]
	public void A_save_with_an_unknown_Room_or_Task_is_rejected_and_a_Brewery_is_not_one()
	{
		var sim = World();
		var s = sim.State;
		PaintBrewery(sim, 20, 20);
		SimJson.Deserialize(SimJson.Serialize(sim));

		s.Rooms[At(s, 21, 20)] = (byte)RoomKind.Brewery + 1;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
		s.Rooms[At(s, 21, 20)] = 0;
		s.GoblinTask[0] = (byte)GoblinTask.Brewing + 1;
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Fact]
	public void A_save_whose_goblin_arrays_are_the_wrong_length_is_still_rejected_with_brew_state_in_it()
	{
		var sim = LooseBrew(out _, out _);
		RunUntilTask(sim, 0, GoblinTask.Brewing);
		var node = JsonNode.Parse(SimJson.Serialize(sim))!;
		node["GoblinCarry"] = JsonNode.Parse("[4]");
		Assert.Throws<JsonException>(() => SimJson.Deserialize(node.ToJsonString()));
	}

	// ---- The loop, end to end

	// Commands only, on the unchanged new world: stairs and a dug room underground, a farm, a Brewery, a Pile and a Grub Pit.
	[Fact]
	public void Fungus_is_farmed_underground_brewed_into_Grog_hauled_to_a_Pile_and_drunk()
	{
		var sim = Simulation.NewWorld(seed: 3);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 20, 12, 20, 12));
		sim.Apply(new Designate(DesignationKind.DigStairs, 1, 20, 12, 20, 12));
		sim.Apply(new Designate(DesignationKind.Dig, 1, 21, 12, 24, 13));
		sim.Apply(new PaintPile(0, 18, 12, 19, 12));
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 12, 20, 14, 20));

		RunUntil(sim, () => Enumerable.Range(0, s.Designations.Length).All(t => s.DesignationAt(t) == DesignationKind.None), limit: 3000, what: "The digging");
		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 1, 21, 12, 22, 13));
		sim.Apply(new PaintRoom(RoomKind.Brewery, 1, 23, 12, 24, 13));
		Assert.Equal(RoomKind.FungusFarm, s.RoomAt(At(s, 21, 12, 1)));
		Assert.Equal(RoomKind.Brewery, s.RoomAt(At(s, 24, 13, 1)));

		var brewed = 0;
		var drunk = 0;
		var startingGrog = Total(s, Material.Grog);
		var sawFungusOnBreweryLevel = false;
		var wasBrewing = new bool[s.GoblinCount];
		var wasDrinking = new bool[s.GoblinCount];
		for (var i = 0; i < 12000; i++)
		{
			RunTicks(sim, 1);
			for (var g = 0; g < s.GoblinCount; g++)
			{
				var brewing = TaskOf(s, g) == GoblinTask.Brewing;
				var drinking = TaskOf(s, g) == GoblinTask.Drinking;
				if (wasBrewing[g] && TaskOf(s, g) == GoblinTask.Idle && s.GoblinCarry[g] == (byte)Material.None)
					brewed++;
				if (wasDrinking[g] && !drinking && TaskOf(s, g) == GoblinTask.Idle)
					drunk++;
				wasBrewing[g] = brewing;
				wasDrinking[g] = drinking;
				if (brewing && s.GoblinZ[g] == 1)
					sawFungusOnBreweryLevel = true;
			}
			Assert.True(Total(s, Material.Grog) >= 0);
			if (brewed > 0 && drunk > startingGrog)
				break;
		}

		Assert.True(brewed >= 1, "No Grog was ever brewed.");
		Assert.True(sawFungusOnBreweryLevel);
		Assert.True(drunk > startingGrog, $"Only {drunk} drinks were taken, and {startingGrog} units of Grog were there to begin with.");
	}
}
