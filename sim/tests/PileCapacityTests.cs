using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>Per-material Pile capacity: 30 units of each material on a Pile tile, no combined limit, loose items uncapped.</summary>
public class PileCapacityTests
{
	// The lone goblin stands at (16, 16, 0). A unit at (16, 14) is two steps away, a Pile at (20, 14) four more.
	static Simulation WorldWithLooseUnit(int x, int y, Material material = Material.Dirt, bool oneGrunt = true)
	{
		var sim = oneGrunt ? NewWorldWithOneGrunt(seed: 1) : Simulation.NewWorld(seed: 1);
		sim.State.AddItems(sim.State.TileIndex(x, y, 0), material, 1);
		sim.RecheckReachability();
		return sim;
	}

	static void Paint(Simulation sim, int x, int y) => sim.Apply(new PaintPile(0, x, y, x, y));

	// Paints a Pile and stocks it, then lets the sim see the new stock.
	static void PaintStocked(Simulation sim, int x, int y, Material material, int units)
	{
		Paint(sim, x, y);
		sim.State.AddItems(sim.State.TileIndex(x, y, 0), material, units);
		sim.RecheckReachability();
	}

	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	static int Count(SimState s, int x, int y, Material material) => s.ItemCount(s.TileIndex(x, y, 0), material);

	[Fact]
	public void A_unit_goes_to_a_farther_Pile_when_the_nearest_one_is_full_of_its_material()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		PaintStocked(sim, 20, 14, Material.Dirt, 30);
		Paint(sim, 25, 25);

		RunTicks(sim, 120);

		Assert.Equal(30, Count(s, 20, 14, Material.Dirt));
		Assert.Equal(1, Count(s, 25, 25, Material.Dirt));
		Assert.Equal(0, Count(s, 16, 14, Material.Dirt));
	}

	[Fact]
	public void A_Pile_full_of_dirt_still_takes_stone_and_each_material_stops_at_30()
	{
		var sim = WorldWithLooseUnit(16, 14, Material.Stone);
		var s = sim.State;
		PaintStocked(sim, 20, 14, Material.Dirt, 30);
		PaintStocked(sim, 20, 14, Material.Stone, 29);

		RunTicks(sim, 80);

		Assert.Equal(30, Count(s, 20, 14, Material.Dirt));
		Assert.Equal(30, Count(s, 20, 14, Material.Stone));
		Assert.Equal(0, Count(s, 16, 14, Material.Stone));
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void With_every_Pile_full_of_its_material_the_unit_stays_loose_and_greyed_and_the_goblin_digs_instead()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		PaintStocked(sim, 20, 14, Material.Dirt, 30);
		var target = s.TileIndex(16, 18, 0);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 18, 16, 18));

		for (var tick = 0; tick < 120; tick++)
		{
			RunTicks(sim, 1);
			Assert.False(TaskOf(s, 0) is GoblinTask.Fetching or GoblinTask.PickingUp or GoblinTask.Carrying or GoblinTask.Dropping, $"Tick {s.Tick}: hauling with nowhere to put the unit.");
		}

		Assert.Equal(TileKind.Stairs, s.KindAt(target));
		var unit = s.TileIndex(16, 14, 0);
		Assert.Equal(1, s.LooseCount(unit, Material.Dirt));
		Assert.False(s.IsHaulReachable(unit));
		Assert.False(s.IsHaulReachable(unit, Material.Dirt));
		Assert.Equal(30, Count(s, 20, 14, Material.Dirt));
	}

	[Fact]
	public void Painting_a_Pile_with_room_makes_a_Haul_waiting_on_full_Piles_reachable_again()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		PaintStocked(sim, 20, 14, Material.Dirt, 30);
		RunTicks(sim, 50);
		var unit = s.TileIndex(16, 14, 0);
		Assert.False(s.IsHaulReachable(unit));

		Paint(sim, 25, 25);

		Assert.True(s.IsHaulReachable(unit));
		RunTicks(sim, 200);
		Assert.Equal(1, Count(s, 25, 25, Material.Dirt));
		Assert.Equal(30, Count(s, 20, 14, Material.Dirt));
	}

	[Fact]
	public void A_carrier_whose_destination_fills_keeps_its_unit_and_waits_taking_no_other_Job_until_storage_appears()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		Paint(sim, 20, 14);
		RunTicks(sim, 12);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));

		// Someone else fills the destination.
		s.AddItems(s.TileIndex(20, 14, 0), Material.Dirt, 30);
		sim.RecheckReachability();
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 18, 16, 18));
		for (var tick = 0; tick < 200; tick++)
		{
			RunTicks(sim, 1);
			Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
			Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
		}
		Assert.Equal(TileKind.Floor, s.KindAt(s.TileIndex(16, 18, 0)));
		Assert.Equal(30, Count(s, 20, 14, Material.Dirt));

		Paint(sim, 25, 25);
		RunTicks(sim, 300);

		// The carried unit, and the one the dug stairs dropped.
		Assert.Equal(2, Count(s, 25, 25, Material.Dirt));
		Assert.Equal(30, Count(s, 20, 14, Material.Dirt));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(TileKind.Stairs, s.KindAt(s.TileIndex(16, 18, 0)));
	}

	[Fact]
	public void The_goblin_that_finishes_dropping_first_takes_the_last_slot_and_the_other_keeps_its_unit_and_waits()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var pile = s.TileIndex(20, 14, 0);
		PaintStocked(sim, 20, 14, Material.Dirt, 29);
		foreach (var g in new[] { 0, 1 })
		{
			(s.GoblinX[g], s.GoblinY[g]) = (20, 14);
			GoblinTasks.FinishPickUp(s, g, Material.Dirt);
			GoblinTasks.StartDropping(s, g);
		}
		sim.RecheckReachability();

		RunTicks(sim, 5);

		// Both finished on the same tick; goblin index order decides.
		Assert.Equal(30, s.ItemCount(pile, Material.Dirt));
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 1));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[1]);
		RunTicks(sim, 60);
		Assert.Equal(30, s.ItemCount(pile, Material.Dirt));
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 1));
	}

	[Fact]
	public void Two_goblins_racing_for_the_last_slot_never_overfill_and_the_loser_reroutes_to_another_Pile()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.GoblinCount = 2;
		s.AddItems(s.TileIndex(14, 16, 0), Material.Dirt, 1);
		s.AddItems(s.TileIndex(18, 16, 0), Material.Dirt, 1);
		PaintStocked(sim, 16, 12, Material.Dirt, 29);
		Paint(sim, 16, 24);

		for (var tick = 0; tick < 300; tick++)
		{
			RunTicks(sim, 1);
			Assert.True(s.ItemCount(s.TileIndex(16, 12, 0), Material.Dirt) <= SimState.PileCapacity, $"Tick {s.Tick}: the Pile is overfull.");
		}

		Assert.Equal(30, Count(s, 16, 12, Material.Dirt));
		Assert.Equal(1, Count(s, 16, 24, Material.Dirt));
		Assert.Equal(0, s.GoblinCarry.Take(2).Count(c => c != 0));
	}

	[Fact]
	public void Painting_skips_a_whole_tile_holding_more_than_30_of_any_material_and_paints_the_rest()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var over = s.TileIndex(10, 10, 0);
		var overStone = s.TileIndex(11, 10, 0);
		var exact = s.TileIndex(12, 10, 0);
		var empty = s.TileIndex(13, 10, 0);
		s.AddItems(over, Material.Dirt, 45);
		s.AddItems(over, Material.Stone, 5);
		s.AddItems(overStone, Material.Stone, 31);
		s.AddItems(exact, Material.Dirt, 30);
		s.AddItems(exact, Material.Stone, 30);

		sim.Apply(new PaintPile(0, 10, 10, 13, 10));

		Assert.False(s.IsPile(over));
		Assert.False(s.IsPile(overStone));
		Assert.True(s.IsPile(exact));
		Assert.True(s.IsPile(empty));
		// Nothing was moved or deleted.
		Assert.Equal(45, s.LooseCount(over, Material.Dirt));
		Assert.Equal(5, s.LooseCount(over, Material.Stone));
		Assert.Equal(31, s.LooseCount(overStone, Material.Stone));
		Assert.Equal(30, s.StoredCount(exact, Material.Dirt));
		Assert.Equal(30, s.StoredCount(exact, Material.Stone));
	}

	[Fact]
	public void A_skipped_over_capacity_tile_is_hauled_a_unit_at_a_time_into_Piles_with_room()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		var over = s.TileIndex(16, 15, 0);
		s.AddItems(over, Material.Dirt, 31);
		sim.Apply(new PaintPile(0, 16, 15, 16, 15));
		Paint(sim, 17, 15);

		RunTicks(sim, 6 * 20 * 3);

		Assert.False(s.IsPile(over));
		Assert.True(s.LooseCount(over, Material.Dirt) < 31);
		Assert.True(Count(s, 17, 15, Material.Dirt) > 0);
		Assert.Equal(31, s.TileItems.Sum() + (s.GoblinCarry[0] != 0 ? 1 : 0));
	}

	[Fact]
	public void Blocked_dirt_does_not_stop_stone_from_the_same_tile_being_hauled()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var source = s.TileIndex(14, 16, 0);
		s.AddItems(source, Material.Dirt, 1);
		s.AddItems(source, Material.Stone, 1);
		PaintStocked(sim, 20, 14, Material.Dirt, 30);

		Assert.True(s.IsHaulReachable(source));
		Assert.False(s.IsHaulReachable(source, Material.Dirt));
		Assert.True(s.IsHaulReachable(source, Material.Stone));
		var claimants = 0;
		for (var tick = 0; tick < 250; tick++)
		{
			RunTicks(sim, 1);
			claimants = Math.Max(claimants, Enumerable.Range(0, s.GoblinCount).Count(g => GoblinTasks.IsFetchTask(TaskOf(s, g)) && s.GoblinJobTile[g] == source));
		}

		// Only the stone had anywhere to go, so only one goblin ever claimed the tile, and the dirt is still there.
		Assert.Equal(1, claimants);
		Assert.Equal(1, Count(s, 20, 14, Material.Stone));
		Assert.Equal(30, Count(s, 20, 14, Material.Dirt));
		Assert.Equal(1, s.LooseCount(source, Material.Dirt));
		Assert.Equal(0, s.LooseCount(source, Material.Stone));
		Assert.Equal(0, s.GoblinCarry.Take(s.GoblinCount).Count(c => c != 0));
	}

	[Fact]
	public void A_goblin_fetching_dirt_whose_storage_fills_hauls_the_stone_instead()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		var source = s.TileIndex(16, 14, 0);
		s.AddItems(source, Material.Dirt, 1);
		s.AddItems(source, Material.Stone, 1);
		PaintStocked(sim, 20, 14, Material.Dirt, 29);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 0));

		// Dirt storage fills while the goblin is on its way.
		s.AddItems(s.TileIndex(20, 14, 0), Material.Dirt, 1);
		sim.RecheckReachability();
		RunTicks(sim, 12);

		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Stone, s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(source, Material.Dirt));
	}

	[Fact]
	public void The_goblin_that_fills_a_materials_last_slot_makes_the_rest_of_that_material_wait()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.AddItems(s.TileIndex(14, 16, 0), Material.Dirt, 3);
		PaintStocked(sim, 20, 14, Material.Dirt, 29);

		RunTicks(sim, 400);

		// Three goblins took the three units at once; the first drop took the last slot and the other two wait holding theirs.
		Assert.Equal(30, Count(s, 20, 14, Material.Dirt));
		Assert.Equal(2, s.GoblinCarry.Take(s.GoblinCount).Count(c => c == (byte)Material.Dirt));
		Assert.Equal(0, s.LooseTotal(s.TileIndex(14, 16, 0)));
		Assert.DoesNotContain(Enumerable.Range(0, s.GoblinCount).Select(g => TaskOf(s, g)), task => task is GoblinTask.Fetching or GoblinTask.PickingUp);
	}

	[Fact]
	public void Loose_items_above_30_are_not_capped()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(10, 10, 0);

		s.AddItems(tile, Material.Dirt, 50);
		sim.RecheckReachability();
		RunTicks(sim, 100);

		Assert.True(s.TileItems.Sum() == 50);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void A_Pile_removed_under_a_stocked_tile_leaves_its_full_stock_loose()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		PaintStocked(sim, 20, 14, Material.Dirt, 30);
		PaintStocked(sim, 20, 14, Material.Stone, 30);

		sim.Apply(new RemovePile(0, 20, 14, 20, 14));

		Assert.Equal(30, s.LooseCount(s.TileIndex(20, 14, 0), Material.Dirt));
		Assert.Equal(30, s.LooseCount(s.TileIndex(20, 14, 0), Material.Stone));
	}

	[Fact]
	public void A_save_with_a_Pile_tile_over_30_of_any_material_is_rejected()
	{
		foreach (var material in new[] { Material.Dirt, Material.Stone })
		{
			var sim = Simulation.NewWorld(seed: 1);
			PaintStocked(sim, 20, 14, material, 31);

			var error = Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
			Assert.Contains("Pile", error.Message);
		}
	}

	[Fact]
	public void A_save_with_30_of_each_material_on_a_Pile_and_over_30_loose_is_accepted()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		PaintStocked(sim, 20, 14, Material.Dirt, 30);
		PaintStocked(sim, 20, 14, Material.Stone, 30);
		s.AddItems(s.TileIndex(10, 10, 0), Material.Dirt, 45);
		s.AddItems(s.TileIndex(10, 10, 0), Material.Stone, 99);

		var restored = SimJson.Deserialize(SimJson.Serialize(sim));

		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
	}

	[Fact]
	public void A_save_taken_while_a_carrier_waits_round_trips_and_resumes_when_storage_appears()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		Paint(sim, 20, 14);
		RunTicks(sim, 12);
		s.AddItems(s.TileIndex(20, 14, 0), Material.Dirt, 30);
		sim.RecheckReachability();
		RunTicks(sim, 40);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));

		var restored = SimJson.Deserialize(SimJson.Serialize(sim));

		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
		Paint(sim, 25, 25);
		Paint(restored, 25, 25);
		RunTicks(sim, 100);
		RunTicks(restored, 100);
		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
		Assert.Equal(1, Count(restored.State, 25, 25, Material.Dirt));
		Assert.Equal(GoblinTask.Idle, TaskOf(restored.State, 0));
	}

	// Two goblins in rooms split by a Solid wall at x=10 on z0: goblin 0 at (5, 5) on the west, goblin 1 at (20, 5) on the east.
	// Round-tripped through a save, as a loaded state with disconnected regions would be.
	static Simulation SplitWarren(Action<Simulation> stock)
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.GoblinCount = 2;
		(s.GoblinX[0], s.GoblinY[0]) = (5, 5);
		(s.GoblinX[1], s.GoblinY[1]) = (20, 5);
		for (var y = 0; y < s.Height; y++)
			s.TileKinds[s.TileIndex(10, y, 0)] = (byte)TileKind.Solid;
		sim.RecheckReachability();
		stock(sim);
		return SimJson.Deserialize(SimJson.Serialize(sim));
	}

	static readonly (int X, int Y) Source = (5, 6);

	[Fact]
	public void Storage_in_another_region_does_not_make_blocked_dirt_eligible_so_the_local_stone_is_still_hauled()
	{
		var sim = SplitWarren(sim =>
		{
			var s = sim.State;
			s.AddItems(s.TileIndex(Source.X, Source.Y, 0), Material.Dirt, 1);
			s.AddItems(s.TileIndex(Source.X, Source.Y, 0), Material.Stone, 1);
			PaintStocked(sim, 5, 7, Material.Dirt, 30);
			Paint(sim, 20, 7);
		});
		var s = sim.State;
		var source = s.TileIndex(Source.X, Source.Y, 0);
		Assert.False(s.IsHaulReachable(source, Material.Dirt));
		Assert.True(s.IsHaulReachable(source, Material.Stone));

		RunTicks(sim, 300);

		Assert.Equal(1, Count(s, 5, 7, Material.Stone));
		Assert.Equal(1, s.LooseCount(source, Material.Dirt));
		Assert.Equal(0, s.GoblinCarry.Take(2).Count(c => c != 0));
		Assert.Equal(0, Count(s, 20, 7, Material.Dirt));
	}

	[Fact]
	public void A_unit_whose_only_storage_is_in_another_region_waits_greyed_even_though_a_goblin_lives_there()
	{
		var sim = SplitWarren(sim =>
		{
			var s = sim.State;
			s.AddItems(s.TileIndex(Source.X, Source.Y, 0), Material.Dirt, 1);
			PaintStocked(sim, 5, 7, Material.Dirt, 30);
			Paint(sim, 20, 7);
		});
		var s = sim.State;
		var source = s.TileIndex(Source.X, Source.Y, 0);

		Assert.False(s.IsHaulReachable(source));
		for (var tick = 0; tick < 300; tick++)
		{
			RunTicks(sim, 1);
			Assert.DoesNotContain(Enumerable.Range(0, 2).Select(g => TaskOf(s, g)), task => task is GoblinTask.Fetching or GoblinTask.PickingUp or GoblinTask.Carrying or GoblinTask.Dropping);
		}

		Assert.Equal(1, s.LooseCount(source, Material.Dirt));
		Assert.Equal(0, Count(s, 20, 7, Material.Dirt));
	}

	[Fact]
	public void Digging_through_the_wall_joins_the_regions_and_the_waiting_unit_is_hauled_to_the_far_Pile()
	{
		var sim = SplitWarren(sim =>
		{
			var s = sim.State;
			s.AddItems(s.TileIndex(Source.X, Source.Y, 0), Material.Dirt, 1);
			PaintStocked(sim, 5, 7, Material.Dirt, 30);
			Paint(sim, 20, 7);
		});
		var s = sim.State;
		RunTicks(sim, 50);
		Assert.Equal(1, s.LooseCount(s.TileIndex(Source.X, Source.Y, 0), Material.Dirt));

		sim.Apply(new Designate(DesignationKind.Dig, 0, 10, 6, 10, 6));
		RunTicks(sim, 1500);

		Assert.Equal(TileKind.Floor, s.KindAt(s.TileIndex(10, 6, 0)));
		// The unit that was waiting, and the one the wall dropped.
		Assert.Equal(2, Count(s, 20, 7, Material.Dirt));
		Assert.Equal(30, Count(s, 5, 7, Material.Dirt));
		Assert.Equal(0, s.GoblinCarry.Take(2).Count(c => c != 0));
	}

	[Fact]
	public void A_carrier_whose_region_has_no_room_waits_and_resumes_when_a_dug_tile_joins_the_region_with_room()
	{
		var sim = SplitWarren(sim =>
		{
			var s = sim.State;
			PaintStocked(sim, 5, 7, Material.Dirt, 29);
			Paint(sim, 20, 7);
			s.AddItems(s.TileIndex(Source.X, Source.Y, 0), Material.Dirt, 2);
			sim.RecheckReachability();
		});
		var s = sim.State;

		RunTicks(sim, 400);

		// One unit took the last local slot; the other waits on the source tile, not in a goblin's hands.
		Assert.Equal(30, Count(s, 5, 7, Material.Dirt));
		Assert.Equal(1, s.LooseCount(s.TileIndex(Source.X, Source.Y, 0), Material.Dirt));
		Assert.Equal(0, s.GoblinCarry.Take(2).Count(c => c != 0));
		sim.Apply(new Designate(DesignationKind.Dig, 0, 10, 6, 10, 6));
		RunTicks(sim, 1500);
		Assert.Equal(2, Count(s, 20, 7, Material.Dirt));
	}
}
