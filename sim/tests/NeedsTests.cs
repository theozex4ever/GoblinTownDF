using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>P3's Grub Need: the meter, Cravings that interrupt a Job, eating, and Starving.</summary>
public class NeedsTests
{
	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	// The lone goblin 0 stands at (16, 16, 0) with the given Grub meter and nothing to eat anywhere.
	static Simulation Lone(int grub)
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		sim.State.GoblinGrub[0] = (byte)grub;
		return sim;
	}

	// Goblins 0 and 1, at (16, 16) and (17, 16) unless a test moves them, with the given meters and nothing to eat anywhere.
	static Simulation Pair(int grub0, int grub1)
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		sim.State.GoblinCount = 2;
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

	static void AssertNoPileOverCapacity(SimState s)
	{
		for (var tile = 0; tile < s.Piles.Length; tile++)
			if (s.IsPile(tile))
				for (var material = Material.Dirt; (int)material <= SimState.CountedMaterials; material++)
					Assert.True(s.ItemCount(tile, material) <= SimState.PileCapacity,
						$"Tick {s.Tick}: Pile tile {tile} holds {s.ItemCount(tile, material)} {material}.");
	}

	// ---- The meter

	[Fact]
	public void Every_goblin_starts_with_a_Grub_meter_of_70_to_100_drawn_from_SimRandom()
	{
		var seen = new HashSet<int>();
		for (var seed = 0; seed < 300; seed++)
		{
			var s = Simulation.NewWorld(seed).State;
			var rng = new SimRandom(seed);
			for (var g = 0; g < s.GoblinCount; g++)
			{
				Assert.InRange((int)s.GoblinGrub[g], 70, 100);
				Assert.Equal(70 + rng.NextInt(31), s.GoblinGrub[g]);
				seen.Add(s.GoblinGrub[g]);
			}
		}
		Assert.Contains(70, seen);
		Assert.Contains(100, seen);
		Assert.True(seen.Count > 25, "The starting meters should be spread across 70 to 100.");
	}

	[Fact]
	public void The_same_seed_starts_every_goblin_with_the_same_meter()
	{
		Assert.Equal(Simulation.NewWorld(seed: 9).State.GoblinGrub, Simulation.NewWorld(seed: 9).State.GoblinGrub);
		Assert.NotEqual(Simulation.NewWorld(seed: 9).State.GoblinGrub, Simulation.NewWorld(seed: 10).State.GoblinGrub);
	}

	[Fact]
	public void The_meter_drains_one_point_every_25_ticks()
	{
		var sim = Lone(grub: 100);

		RunTicks(sim, 24);
		Assert.Equal(100, sim.State.GoblinGrub[0]);
		RunTicks(sim, 1);
		Assert.Equal(99, sim.State.GoblinGrub[0]);
		RunTicks(sim, 24);
		Assert.Equal(99, sim.State.GoblinGrub[0]);
		RunTicks(sim, 1);
		Assert.Equal(98, sim.State.GoblinGrub[0]);
		RunTicks(sim, 250);
		Assert.Equal(88, sim.State.GoblinGrub[0]);
	}

	[Fact]
	public void The_meter_stops_at_0_and_nothing_dies()
	{
		var sim = Lone(grub: 1);

		RunTicks(sim, 25 * 3);

		Assert.Equal(0, sim.State.GoblinGrub[0]);
		Assert.Equal(1, sim.State.GoblinCount);
	}

	// ---- Cravings interrupt

	[Fact]
	public void A_digging_goblin_below_30_leaves_at_once_and_the_tile_keeps_its_dig_progress()
	{
		var sim = Lone(grub: 100);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 14, 16, 14));
		var tile = At(s, 16, 14);
		RunTicks(sim, 13);
		Assert.Equal(GoblinTask.Digging, TaskOf(s, 0));
		var progress = s.DigProgress[tile];
		Assert.InRange((int)progress, 1, 19);

		s.GoblinGrub[0] = 29;
		Put(sim, Material.Grub, 20, 16);
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(-1, s.GoblinJobTile[0]);
		Assert.Equal(progress, s.DigProgress[tile]);
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(tile));
		RunTicks(sim, 10);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(progress, s.DigProgress[tile]);

		// Fed, the goblin goes back to the same tile and finishes it from where it left off.
		RunTicks(sim, 200);
		Assert.Equal(TileKind.Stairs, s.KindAt(tile));
		Assert.Equal(0, s.ItemCount(At(s, 20, 16), Material.Grub));
	}

	[Fact]
	public void A_goblin_with_no_reachable_Grub_keeps_working_and_goes_to_eat_once_Grub_becomes_reachable()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 14, 16, 14));
		RunTicks(sim, 13);
		Assert.Equal(GoblinTask.Digging, TaskOf(s, 0));

		// A unit of Grub on z1, cut off from the warren by solid dirt.
		var islet = At(s, 25, 25, 1);
		s.TileKinds[islet] = (byte)TileKind.Floor;
		s.AddItems(islet, Material.Grub, 1);
		sim.RecheckReachability();
		RunTicks(sim, 3);
		Assert.Equal(GoblinTask.Digging, TaskOf(s, 0));

		// Stairs open a way to it: the goblin leaves the dig for the Grub.
		s.TileKinds[At(s, 25, 25, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[islet] = (byte)TileKind.Stairs;
		sim.RecheckReachability();
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));

		RunTicks(sim, 200);
		Assert.Equal(0, s.ItemCount(islet, Material.Grub));
		Assert.InRange((int)s.GoblinGrub[0], 70, 100);
	}

	[Fact]
	public void A_craving_goblin_with_no_Grub_at_all_works_as_before()
	{
		var sim = Lone(grub: 10);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 14, 16, 14));

		RunTicks(sim, 30);

		Assert.Equal(TileKind.Stairs, s.KindAt(At(s, 16, 14)));
	}

	[Fact]
	public void A_wandering_or_idle_goblin_below_30_goes_to_eat()
	{
		var sim = Lone(grub: 29);
		Put(sim, Material.Grub, 16, 19);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(sim.State, 0));
	}

	[Fact]
	public void A_goblin_at_30_or_more_does_not_crave()
	{
		var sim = Lone(grub: 30);
		Put(sim, Material.Grub, 16, 19);

		RunTicks(sim, 24);

		Assert.NotEqual(GoblinTask.Seeking, TaskOf(sim.State, 0));
		Assert.Equal(1, sim.State.ItemCount(At(sim.State, 16, 19), Material.Grub));
	}

	[Fact]
	public void A_goblin_picking_up_or_fetching_drops_its_claim_when_it_craves()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		Paint(sim, 10, 16);
		Put(sim, Material.Dirt, 20, 16);
		Put(sim, Material.Grub, 16, 12);
		// Craving came after the Haul was claimed.
		s.GoblinGrub[0] = 100;
		RunTicks(sim, 4);
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 0));

		s.GoblinGrub[0] = 29;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(1, s.LooseCount(At(s, 20, 16), Material.Dirt));
	}

	// ---- Dropping what is carried

	[Fact]
	public void A_carrier_on_a_Pile_tile_with_room_stores_its_unit_there()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		Paint(sim, 16, 16);
		Put(sim, Material.Grub, 20, 16);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.StoredCount(At(s, 16, 16), Material.Dirt));
	}

	[Fact]
	public void A_goblin_mid_drop_on_a_Pile_tile_with_room_stores_its_unit_there()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		Paint(sim, 16, 16);
		Put(sim, Material.Grub, 20, 16);
		GoblinTasks.FinishPickUp(s, 0, Material.Stone);
		GoblinTasks.StartDropping(s, 0);
		s.GoblinWorkTicks[0] = 0;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(1, s.StoredCount(At(s, 16, 16), Material.Stone));
		Assert.Equal(0, s.GoblinWorkTicks[0]);
	}

	[Fact]
	public void A_carrier_on_a_full_Pile_tile_drops_its_unit_on_the_nearest_legal_tile_with_ties_to_the_lowest_index()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		s.AddItems(At(s, 16, 16), Material.Dirt, SimState.PileCapacity);
		Paint(sim, 16, 16);
		Put(sim, Material.Grub, 20, 16);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);

		RunTicks(sim, 1);

		// The four neighbours are equally near; (16, 15) has the lowest tile index.
		Assert.Equal(SimState.PileCapacity, s.StoredCount(At(s, 16, 16), Material.Dirt));
		Assert.Equal(1, s.LooseCount(At(s, 16, 15), Material.Dirt));
		Assert.Equal(1, s.ItemCount(At(s, 16, 15), Material.Dirt));
		AssertNoPileOverCapacity(s);
	}

	[Fact]
	public void A_full_neighbouring_Pile_tile_is_not_a_legal_destination_but_one_with_room_is()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		s.AddItems(At(s, 16, 16), Material.Dirt, SimState.PileCapacity);
		s.AddItems(At(s, 16, 15), Material.Dirt, SimState.PileCapacity);
		Paint(sim, 16, 16);
		Paint(sim, 16, 15);
		Put(sim, Material.Grub, 20, 16);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);

		RunTicks(sim, 1);

		Assert.Equal(SimState.PileCapacity, s.ItemCount(At(s, 16, 15), Material.Dirt));
		Assert.Equal(1, s.ItemCount(At(s, 15, 16), Material.Dirt));
		AssertNoPileOverCapacity(s);

		// A Pile tile that still has room takes the unit as storage, and counts as nearest.
		var sim2 = Lone(grub: 29);
		var s2 = sim2.State;
		s2.AddItems(At(s2, 16, 16), Material.Dirt, SimState.PileCapacity);
		Paint(sim2, 16, 16);
		Paint(sim2, 16, 15);
		Put(sim2, Material.Grub, 20, 16);
		GoblinTasks.FinishPickUp(s2, 0, Material.Dirt);

		RunTicks(sim2, 1);

		Assert.Equal(1, s2.StoredCount(At(s2, 16, 15), Material.Dirt));
		AssertNoPileOverCapacity(s2);
	}

	[Fact]
	public void A_carrier_boxed_in_by_full_Piles_drops_on_the_nearest_legal_tile_further_out()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		foreach (var (x, y) in new[] { (16, 16), (16, 15), (15, 16), (17, 16), (16, 17) })
		{
			s.AddItems(At(s, x, y), Material.Dirt, SimState.PileCapacity);
			Paint(sim, x, y);
		}
		Put(sim, Material.Grub, 20, 16);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);

		RunTicks(sim, 1);

		// Two steps away, (16, 14) has the lowest tile index.
		Assert.Equal(1, s.LooseCount(At(s, 16, 14), Material.Dirt));
		AssertNoPileOverCapacity(s);
	}

	[Fact]
	public void A_carrier_away_from_any_Pile_drops_its_unit_loose_on_its_own_tile()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		Paint(sim, 10, 10);
		Put(sim, Material.Grub, 20, 16);
		Place(s, 0, 18, 18);
		GoblinTasks.FinishPickUp(s, 0, Material.Stone);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(1, s.LooseCount(At(s, 18, 18), Material.Stone));
		Assert.Equal(0, s.StoredCount(At(s, 10, 10), Material.Stone));
	}

	[Fact]
	public void A_waiting_carrier_with_no_Pile_at_all_drops_its_unit_when_it_craves()
	{
		var sim = Lone(grub: 100);
		var s = sim.State;
		Put(sim, Material.Grub, 20, 16);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		RunTicks(sim, 30);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));

		s.GoblinGrub[0] = 29;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(1, s.LooseCount(s.TileIndex(s.GoblinX[0], s.GoblinY[0], 0), Material.Dirt));
	}

	[Fact]
	public void A_dropped_unit_becomes_a_Haul_that_can_be_claimed_again()
	{
		var sim = Pair(grub0: 29, grub1: 100);
		var s = sim.State;
		Paint(sim, 10, 16);
		Put(sim, Material.Grub, 20, 16);
		Place(s, 0, 18, 18);
		GoblinTasks.FinishPickUp(s, 0, Material.Stone);

		RunTicks(sim, 200);

		// Goblin 0 ate, and the stone it dropped was hauled to the Pile by someone.
		Assert.Equal(0, s.ItemCount(At(s, 20, 16), Material.Grub));
		Assert.Equal(1, s.StoredCount(At(s, 10, 16), Material.Stone));
		Assert.Equal(0, s.LooseCount(At(s, 18, 18), Material.Stone));
	}

	// ---- Eating

	[Fact]
	public void Eating_a_unit_underfoot_takes_10_ticks_adds_50_and_removes_the_unit()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		Put(sim, Material.Grub, 16, 16, units: 3);

		RunTicks(sim, 9);
		Assert.Equal(GoblinTask.Eating, TaskOf(s, 0));
		Assert.Equal(29, s.GoblinGrub[0]);
		Assert.Equal(3, s.ItemCount(At(s, 16, 16), Material.Grub));

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(79, s.GoblinGrub[0]);
		Assert.Equal(2, s.ItemCount(At(s, 16, 16), Material.Grub));
	}

	[Fact]
	public void A_meal_adds_50_to_the_meter_and_the_meter_tops_out_at_100()
	{
		Assert.Equal(79, NeedRules.Restore(29));
		Assert.Equal(50, NeedRules.Restore(0));
		Assert.Equal(100, NeedRules.Restore(50));
		Assert.Equal(100, NeedRules.Restore(80));
		Assert.Equal(100, NeedRules.Restore(100));
	}

	[Fact]
	public void A_goblin_walks_to_a_unit_three_tiles_away_and_eats_it_there_without_carrying()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		Put(sim, Material.Grub, 19, 16);

		RunTicks(sim, 8);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(19, s.GoblinX[0]);
		Assert.Equal(GoblinTask.Eating, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);

		RunTicks(sim, 9);
		Assert.Equal(29, s.GoblinGrub[0]);
		Assert.Equal(1, s.ItemCount(At(s, 19, 16), Material.Grub));
		RunTicks(sim, 1);
		Assert.Equal(79, s.GoblinGrub[0]);
		Assert.Equal(0, s.ItemCount(At(s, 19, 16), Material.Grub));
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
	}

	[Fact]
	public void A_goblin_eats_a_stored_unit_from_a_Pile_where_it_lies()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		Put(sim, Material.Grub, 16, 14, units: 3);
		Paint(sim, 16, 14);

		RunTicks(sim, 6 + 10);

		Assert.Equal(79, s.GoblinGrub[0]);
		Assert.Equal(2, s.StoredCount(At(s, 16, 14), Material.Grub));
		Assert.True(s.IsPile(At(s, 16, 14)));
	}

	[Fact]
	public void The_nearest_unit_by_path_is_eaten_with_ties_to_the_lowest_tile_index()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		Put(sim, Material.Grub, 13, 16);
		Put(sim, Material.Grub, 16, 19);
		Put(sim, Material.Grub, 21, 16);
		Paint(sim, 16, 19);

		RunTicks(sim, 9 + 10);

		Assert.Equal(0, s.ItemCount(At(s, 13, 16), Material.Grub));
		Assert.Equal(1, s.ItemCount(At(s, 16, 19), Material.Grub));
		Assert.Equal(1, s.ItemCount(At(s, 21, 16), Material.Grub));
	}

	[Fact]
	public void Eating_the_last_unit_of_a_full_Pile_makes_room_for_a_waiting_Haul()
	{
		var sim = Lone(grub: 29);
		var s = sim.State;
		s.AddItems(At(s, 16, 16), Material.Grub, SimState.PileCapacity);
		Paint(sim, 16, 16);
		sim.RecheckReachability();
		Assert.False(s.HasStorageRoom(At(s, 16, 16), Material.Grub));

		RunTicks(sim, 10);

		Assert.Equal(SimState.PileCapacity - 1, s.StoredCount(At(s, 16, 16), Material.Grub));
		Assert.True(s.HasStorageRoom(At(s, 16, 16), Material.Grub));
	}

	// ---- Nothing reserves a unit

	[Fact]
	public void A_goblin_can_eat_the_unit_a_hauler_was_walking_to_and_the_hauler_finds_other_work()
	{
		var sim = Pair(grub0: 100, grub1: 29);
		var s = sim.State;
		Paint(sim, 10, 16);
		Place(s, 0, 6, 16);
		Put(sim, Material.Grub, 20, 16);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 0));
		Assert.Equal(At(s, 20, 16), s.GoblinJobTile[0]);

		Put(sim, Material.Dirt, 6, 20);
		RunTicks(sim, 200);

		Assert.Equal(0, s.ItemCount(At(s, 20, 16), Material.Grub));
		Assert.Equal(0, s.StoredCount(At(s, 10, 16), Material.Grub));
		Assert.True(s.GoblinGrub[1] >= 70, "Goblin 1 ate the unit.");
		Assert.Equal(1, s.StoredCount(At(s, 10, 16), Material.Dirt));
		Assert.NotEqual(GoblinTask.Fetching, TaskOf(s, 0));
	}

	[Fact]
	public void A_goblin_eating_a_unit_a_hauler_picks_up_first_searches_again()
	{
		var sim = Pair(grub0: 100, grub1: 29);
		var s = sim.State;
		Paint(sim, 10, 16);
		Place(s, 0, 18, 16);
		Put(sim, Material.Grub, 20, 16);

		// The hauler is two tiles away and picks up in 5 ticks; the eater is three away and needs 10.
		RunTicks(sim, 12);

		Assert.Equal(0, s.ItemCount(At(s, 20, 16), Material.Grub));
		Assert.Equal(29, s.GoblinGrub[1]);
		Assert.NotEqual(GoblinTask.Eating, TaskOf(s, 1));
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Grub, s.GoblinCarry[0]);
	}

	[Fact]
	public void Two_goblins_heading_for_the_last_unit_one_eats_and_the_other_searches_again()
	{
		var sim = Pair(grub0: 29, grub1: 28);
		var s = sim.State;
		Place(s, 0, 16, 16);
		Place(s, 1, 17, 16);
		Put(sim, Material.Grub, 16, 18);

		RunTicks(sim, 60);

		Assert.Equal(0, s.ItemCount(At(s, 16, 18), Material.Grub));
		// Goblin 0 is two tiles from the unit and goblin 1 three: goblin 0 finishes first.
		Assert.InRange((int)s.GoblinGrub[0], 70, 79);
		Assert.InRange((int)s.GoblinGrub[1], 26, 28);
		Assert.NotEqual(GoblinTask.Eating, TaskOf(s, 1));

		// More Grub turns up: the loser goes for it.
		Put(sim, Material.Grub, 12, 16);
		RunTicks(sim, 120);
		Assert.Equal(0, s.ItemCount(At(s, 12, 16), Material.Grub));
		Assert.InRange((int)s.GoblinGrub[1], 70, 79);
	}

	[Fact]
	public void Two_goblins_eating_the_last_unit_on_one_tile_the_first_to_finish_eats_it()
	{
		var sim = Pair(grub0: 29, grub1: 29);
		var s = sim.State;
		Place(s, 1, 16, 16);
		Put(sim, Material.Grub, 16, 16);

		RunTicks(sim, 9);
		Assert.Equal(GoblinTask.Eating, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Eating, TaskOf(s, 1));
		RunTicks(sim, 1);

		Assert.Equal(79, s.GoblinGrub[0]);
		Assert.Equal(29, s.GoblinGrub[1]);
		Assert.Equal(0, s.ItemCount(At(s, 16, 16), Material.Grub));
		Assert.NotEqual(GoblinTask.Eating, TaskOf(s, 1));
	}

	// ---- Starving

	[Fact]
	public void A_Starving_goblin_walks_and_works_at_half_speed()
	{
		static int TicksToDig(int grub)
		{
			var sim = Lone(grub);
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

		var fed = TicksToDig(grub: 100);
		var starving = TicksToDig(grub: 0);

		// One step of 3 ticks and 20 ticks of digging.
		Assert.Equal(23, fed);
		Assert.Equal(46, starving);
	}

	[Fact]
	public void A_Starving_goblin_hauls_at_half_speed()
	{
		static int TicksToStore(int grub)
		{
			var sim = Lone(grub);
			Paint(sim, 17, 16);
			Put(sim, Material.Dirt, 16, 16);
			var ticks = 0;
			while (sim.State.StoredCount(sim.State.TileIndex(17, 16, 0), Material.Dirt) == 0 && ticks < 500)
			{
				RunTicks(sim, 1);
				ticks++;
			}
			return ticks;
		}

		// 5 ticks to pick up, 3 to step, 5 to drop, and the claim shares the first tick with the pick-up.
		var fed = TicksToStore(grub: 100);
		var starving = TicksToStore(grub: 0);

		Assert.Equal(2 * fed, starving);
	}

	[Fact]
	public void A_Starving_goblin_still_eats_in_10_ticks()
	{
		var sim = Lone(grub: 0);
		Put(sim, Material.Grub, 16, 16);

		RunTicks(sim, 10);

		Assert.Equal(50, sim.State.GoblinGrub[0]);
		Assert.Equal(GoblinTask.Idle, TaskOf(sim.State, 0));
	}

	[Fact]
	public void A_goblin_that_stays_Starving_for_a_long_time_is_still_alive()
	{
		var sim = Lone(grub: 0);

		RunTicks(sim, 3000);

		Assert.Equal(0, sim.State.GoblinGrub[0]);
		Assert.Equal(1, sim.State.GoblinCount);
	}

	// ---- Saves and replays

	[Fact]
	public void A_save_with_a_goblin_mid_meal_round_trips_and_replays_identically()
	{
		var sim = Simulation.NewWorld(seed: 3);
		var s = sim.State;
		s.GoblinGrub[2] = 29;
		s.GoblinGrub[5] = 20;
		RunTicks(sim, 4);
		Assert.Contains(Enumerable.Range(0, s.GoblinCount), g => TaskOf(s, g) is GoblinTask.Seeking or GoblinTask.Eating);
		RunTicks(sim, 6);
		Assert.Contains(Enumerable.Range(0, s.GoblinCount), g => TaskOf(s, g) == GoblinTask.Eating);

		var json = SimJson.Serialize(sim);
		var restored = SimJson.Deserialize(json);
		Assert.Equal(json, SimJson.Serialize(restored));

		RunTicks(sim, 400);
		RunTicks(restored, 400);

		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
	}

	[Fact]
	public void A_save_with_a_meter_above_100_is_rejected()
	{
		var sim = Simulation.NewWorld(seed: 3);
		sim.State.GoblinGrub[0] = 101;

		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Fact]
	public void A_save_with_the_wrong_number_of_meters_is_rejected()
	{
		var sim = Simulation.NewWorld(seed: 3);
		sim.State.GoblinGrub = new byte[3];

		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	// Grub on tiles plus Grub in hands.
	static int TotalGrub(SimState s)
	{
		var total = 0;
		for (var tile = 0; tile < s.Piles.Length; tile++)
			total += s.ItemCount(tile, Material.Grub);
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.GoblinCarry[g] == (byte)Material.Grub)
				total++;
		return total;
	}

	[Fact]
	public void Seven_craving_Grunts_each_eat_one_unit_of_the_starting_Grub_amid_other_work()
	{
		var sim = Simulation.NewWorld(seed: 12);
		var s = sim.State;
		for (var g = 0; g < s.GoblinCount; g++)
			s.GoblinGrub[g] = 29;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 10, 14, 12));
		Paint(sim, 8, 8);

		RunTicks(sim, 250);

		Assert.Equal(14 - 7, TotalGrub(s));
		for (var g = 0; g < s.GoblinCount; g++)
			Assert.InRange((int)s.GoblinGrub[g], 60, 79);
		AssertNoPileOverCapacity(s);
	}
}
