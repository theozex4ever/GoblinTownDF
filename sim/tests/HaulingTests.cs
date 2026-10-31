using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

public class HaulingTests
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

	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	static int Stored(SimState s, int x, int y) => s.StoredCount(s.TileIndex(x, y, 0), Material.Dirt);

	static int Loose(SimState s, int x, int y) => s.LooseCount(s.TileIndex(x, y, 0), Material.Dirt);

	static (int X, int Y) At(SimState s, int g) => (s.GoblinX[g], s.GoblinY[g]);

	static bool HaulTask(GoblinTask task) => task is GoblinTask.Fetching or GoblinTask.PickingUp or GoblinTask.Carrying or GoblinTask.Dropping;

	[Fact]
	public void A_goblin_carries_a_loose_unit_to_a_Pile_and_the_unit_is_stored_there()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		Paint(sim, 20, 14);

		RunTicks(sim, 40);

		Assert.Equal(0, Loose(s, 16, 14));
		Assert.Equal(0, s.ItemCount(s.TileIndex(16, 14, 0), Material.Dirt));
		Assert.Equal(1, Stored(s, 20, 14));
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.TileItems.Sum());
	}

	[Fact]
	public void Walking_to_the_unit_picking_up_carrying_and_dropping_off_run_on_the_tick_counts()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		Paint(sim, 20, 14);

		// Two steps at 3 ticks each reach the unit on tick 6.
		RunTicks(sim, 5);
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal((16, 14), At(s, 0));
		Assert.Equal(GoblinTask.PickingUp, TaskOf(s, 0));

		// Picking up takes 5 ticks; the unit leaves the tile as it ends.
		RunTicks(sim, 4);
		Assert.Equal(GoblinTask.PickingUp, TaskOf(s, 0));
		Assert.Equal(1, Loose(s, 16, 14));
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
		Assert.Equal(0, Loose(s, 16, 14));

		// Carrying still takes 3 ticks per tile.
		RunTicks(sim, 2);
		Assert.Equal((16, 14), At(s, 0));
		RunTicks(sim, 1);
		Assert.Equal((17, 14), At(s, 0));
		RunTicks(sim, 2);
		Assert.Equal((17, 14), At(s, 0));
		RunTicks(sim, 1);
		Assert.Equal((18, 14), At(s, 0));

		// Arrival on the Pile at tick 23, then 5 ticks of dropping.
		RunTicks(sim, 6);
		Assert.Equal((20, 14), At(s, 0));
		Assert.Equal(GoblinTask.Dropping, TaskOf(s, 0));
		RunTicks(sim, 4);
		Assert.Equal(GoblinTask.Dropping, TaskOf(s, 0));
		Assert.Equal(0, Stored(s, 20, 14));
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(1, Stored(s, 20, 14));
	}

	[Fact]
	public void A_goblin_standing_on_the_unit_starts_picking_up_at_once()
	{
		var sim = WorldWithLooseUnit(16, 16);
		Paint(sim, 20, 14);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.PickingUp, TaskOf(sim.State, 0));
	}

	[Fact]
	public void A_unit_is_carried_to_the_nearest_Pile_tile_by_path_length()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		Paint(sim, 10, 14);
		Paint(sim, 19, 14);

		RunTicks(sim, 60);

		Assert.Equal(1, Stored(s, 19, 14));
		Assert.Equal(0, Stored(s, 10, 14));
	}

	[Fact]
	public void Equally_near_Pile_tiles_go_to_the_lowest_tile_index()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		Paint(sim, 18, 14);
		Paint(sim, 14, 14);

		RunTicks(sim, 60);

		Assert.Equal(1, Stored(s, 14, 14));
		Assert.Equal(0, Stored(s, 18, 14));
	}

	[Fact]
	public void A_stone_unit_is_carried_as_stone()
	{
		var sim = WorldWithLooseUnit(16, 14, Material.Stone);
		var s = sim.State;
		Paint(sim, 20, 14);

		RunTicks(sim, 11);
		Assert.Equal((byte)Material.Stone, s.GoblinCarry[0]);
		RunTicks(sim, 30);

		Assert.Equal(1, s.StoredCount(s.TileIndex(20, 14, 0), Material.Stone));
		Assert.Equal(0, s.ItemCount(s.TileIndex(20, 14, 0), Material.Dirt));
	}

	[Fact]
	public void A_Pile_on_another_level_is_reached_over_Stairs()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.GoblinCount = 1;
		s.TileKinds[s.TileIndex(16, 15, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[s.TileIndex(16, 15, 1)] = (byte)TileKind.Stairs;
		s.TileKinds[s.TileIndex(16, 14, 1)] = (byte)TileKind.Floor;
		s.AddItems(s.TileIndex(16, 16, 0), Material.Dirt, 1);
		sim.Apply(new PaintPile(1, 16, 14, 16, 14));

		RunTicks(sim, 60);

		Assert.Equal(1, s.StoredCount(s.TileIndex(16, 14, 1), Material.Dirt));
		Assert.Equal(1, s.TileItems.Sum());
		Assert.Equal(1, s.GoblinZ[0]);
	}

	[Fact]
	public void A_nearer_loose_unit_is_hauled_before_a_farther_Designation()
	{
		var sim = WorldWithLooseUnit(16, 14);
		Paint(sim, 10, 16);
		// The dig is three steps from a tile it can dig from; the unit is two.
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 20, 16, 20, 16));

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Fetching, TaskOf(sim.State, 0));
	}

	[Fact]
	public void A_nearer_Designation_is_dug_before_a_farther_loose_unit()
	{
		var sim = WorldWithLooseUnit(16, 12);
		Paint(sim, 10, 16);
		// The dig is two steps from a tile it can dig from; the unit is four.
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 19, 16, 19));

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Walking, TaskOf(sim.State, 0));
	}

	[Fact]
	public void A_tie_between_a_Designation_and_a_loose_unit_goes_to_the_lower_tile_index()
	{
		var lowDig = WorldWithLooseUnit(16, 14);
		Paint(lowDig, 10, 16);
		lowDig.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 13, 16, 13));
		var lowHaul = WorldWithLooseUnit(16, 14);
		Paint(lowHaul, 10, 16);
		lowHaul.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 19, 16, 19));

		RunTicks(lowDig, 1);
		RunTicks(lowHaul, 1);

		Assert.Equal(GoblinTask.Walking, TaskOf(lowDig.State, 0));
		Assert.Equal(GoblinTask.Fetching, TaskOf(lowHaul.State, 0));
	}

	[Fact]
	public void Two_goblins_never_claim_the_same_loose_unit()
	{
		var sim = WorldWithLooseUnit(16, 14, oneGrunt: false);
		var s = sim.State;
		Paint(sim, 20, 14);

		for (var tick = 0; tick < 80; tick++)
		{
			RunTicks(sim, 1);
			var haulers = Enumerable.Range(0, s.GoblinCount).Count(g => TaskOf(s, g) is GoblinTask.Fetching or GoblinTask.PickingUp);
			Assert.True(haulers <= 1, $"Tick {s.Tick}: {haulers} goblins are hauling one unit.");
		}

		Assert.Equal(1, Stored(s, 20, 14));
		Assert.Equal(1, s.TileItems.Sum());
	}

	[Fact]
	public void Goblins_haul_different_units_at_the_same_time()
	{
		var sim = WorldWithLooseUnit(16, 14, oneGrunt: false);
		var s = sim.State;
		s.AddItems(s.TileIndex(12, 16, 0), Material.Dirt, 1);
		sim.RecheckReachability();
		Paint(sim, 20, 20);

		RunTicks(sim, 2);

		Assert.Equal(2, Enumerable.Range(0, s.GoblinCount).Count(g => TaskOf(s, g) is GoblinTask.Fetching));
		RunTicks(sim, 120);
		Assert.Equal(2, Stored(s, 20, 20));
	}

	[Fact]
	public void A_dig_and_a_haul_on_the_same_tile_are_claimed_by_two_goblins()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.GoblinCount = 2;
		var tile = s.TileIndex(16, 15, 0);
		s.AddItems(tile, Material.Dirt, 1);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 15, 16, 15));
		Paint(sim, 25, 25);

		RunTicks(sim, 200);

		Assert.Equal(TileKind.Stairs, s.KindAt(tile));
		Assert.Equal(2, Stored(s, 25, 25));
		Assert.Equal(2, s.TileItems.Sum());
	}

	[Fact]
	public void With_no_Pile_the_units_stay_put_and_the_goblin_digs_instead()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		var target = s.TileIndex(16, 18, 0);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 18, 16, 18));

		for (var tick = 0; tick < 120; tick++)
		{
			RunTicks(sim, 1);
			Assert.False(HaulTask(TaskOf(s, 0)), $"Tick {s.Tick}: the goblin hauls with no Pile.");
		}

		Assert.Equal(TileKind.Stairs, s.KindAt(target));
		Assert.Equal(1, Loose(s, 16, 14));
		Assert.False(s.IsHaulReachable(s.TileIndex(16, 14, 0)));
	}

	[Fact]
	public void Painting_a_Pile_makes_a_waiting_Haul_reachable_without_anything_else()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		RunTicks(sim, 100);
		Assert.Equal(1, Loose(s, 16, 14));

		Paint(sim, 20, 14);

		Assert.True(s.IsHaulReachable(s.TileIndex(16, 14, 0)));
		RunTicks(sim, 100);
		Assert.Equal(1, Stored(s, 20, 14));
		Assert.Equal(0, Loose(s, 16, 14));
	}

	[Fact]
	public void A_Haul_waits_when_the_Pile_cannot_be_reached()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		// A Pile in a walled-off pocket nobody can enter.
		var pocket = s.TileIndex(2, 2, 0);
		foreach (var wall in new[] { (1, 2), (3, 2), (2, 1), (2, 3) })
			s.TileKinds[s.TileIndex(wall.Item1, wall.Item2, 0)] = (byte)TileKind.Solid;
		Paint(sim, 2, 2);

		RunTicks(sim, 80);

		Assert.True(s.IsPile(pocket));
		Assert.False(s.IsHaulReachable(s.TileIndex(16, 14, 0)));
		Assert.Equal(1, Loose(s, 16, 14));
	}

	[Fact]
	public void A_dug_tile_with_a_Pile_painted_makes_a_Haul_reachable()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		var pocket = s.TileIndex(2, 2, 0);
		var wall = s.TileIndex(3, 2, 0);
		foreach (var (x, y) in new[] { (1, 2), (3, 2), (2, 1), (2, 3) })
			s.TileKinds[s.TileIndex(x, y, 0)] = (byte)TileKind.Solid;
		Paint(sim, 2, 2);
		sim.RecheckReachability();
		Assert.False(s.IsHaulReachable(s.TileIndex(16, 14, 0)));
		s.Designations[wall] = (byte)DesignationKind.Dig;
		sim.RecheckReachability();

		RunTicks(sim, 400);

		Assert.Equal(TileKind.Floor, s.KindAt(wall));
		Assert.True(s.IsPile(pocket));
		// Both the dug wall's unit and the first unit end on the pocket Pile.
		Assert.Equal(2, s.StoredCount(pocket, Material.Dirt));
	}

	[Fact]
	public void A_carrying_goblin_finishes_its_delivery_when_a_nearer_Designation_appears()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		Paint(sim, 20, 14);
		RunTicks(sim, 12);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		var next = s.TileIndex(17, 15, 0);

		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 17, 15, 17, 15));
		while (TaskOf(s, 0) is GoblinTask.Carrying or GoblinTask.Dropping)
		{
			Assert.Equal(0, s.DigProgress[next]);
			RunTicks(sim, 1);
		}

		Assert.Equal(1, Stored(s, 20, 14));
		RunTicks(sim, 80);
		Assert.Equal(TileKind.Stairs, s.KindAt(next));
	}

	[Fact]
	public void Cancelling_a_Designation_does_not_release_a_goblin_hauling_from_that_tile()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.GoblinCount = 2;
		var tile = s.TileIndex(16, 15, 0);
		s.AddItems(tile, Material.Dirt, 1);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 15, 16, 15));
		Paint(sim, 25, 25);
		RunTicks(sim, 1);
		// Goblin 0 is next to the tile and digs it; goblin 1 takes the unit lying on it.
		Assert.True(TaskOf(s, 0) is GoblinTask.Walking or GoblinTask.Digging);
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 1));

		sim.Apply(new CancelDesignation(0, 16, 15, 16, 15));

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Fetching, TaskOf(s, 1));
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void Painting_a_Pile_over_the_unit_being_fetched_frees_the_goblin()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		Paint(sim, 20, 14);
		RunTicks(sim, 8);
		Assert.Equal(GoblinTask.PickingUp, TaskOf(s, 0));

		Paint(sim, 16, 14);

		Assert.Null(GoblinTasks.FindViolation(s));
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		RunTicks(sim, 80);
		Assert.Equal(1, s.TileItems.Sum());
		Assert.Equal(1, Stored(s, 16, 14));
	}

	[Fact]
	public void A_Pile_removed_while_dropping_sends_the_goblin_to_the_next_nearest()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		Paint(sim, 20, 14);
		Paint(sim, 20, 20);
		RunTicks(sim, 25);
		Assert.Equal(GoblinTask.Dropping, TaskOf(s, 0));

		sim.Apply(new RemovePile(0, 20, 14, 20, 14));

		Assert.Null(GoblinTasks.FindViolation(s));
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		RunTicks(sim, 80);
		Assert.Equal(1, Stored(s, 20, 20));
		Assert.Equal(1, s.TileItems.Sum());
	}

	[Fact]
	public void A_Pile_removed_while_carrying_with_none_left_puts_the_unit_down_where_the_goblin_stands()
	{
		var sim = WorldWithLooseUnit(16, 14);
		var s = sim.State;
		Paint(sim, 20, 14);
		RunTicks(sim, 15);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));

		sim.Apply(new RemovePile(0, 20, 14, 20, 14));
		RunTicks(sim, 3);

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.TileItems.Sum());
		Assert.Equal(1, s.LooseCount(s.TileIndex(s.GoblinX[0], s.GoblinY[0], 0), Material.Dirt));
		RunTicks(sim, 80);
		Assert.Equal(1, s.TileItems.Sum());
		Assert.False(HaulTask(TaskOf(s, 0)));
	}

	[Fact]
	public void Digging_stairs_out_from_under_a_dropping_goblin_sends_it_on_carrying()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.GoblinCount = 2;
		var pile = s.TileIndex(16, 16, 0);
		(s.GoblinX[1], s.GoblinY[1]) = (17, 16);
		Paint(sim, 16, 16);
		Paint(sim, 25, 25);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 16, 16, 16));
		s.DigProgress[pile] = 18;
		GoblinTasks.BeginJob(s, 1, pile, digging: true);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		GoblinTasks.StartDropping(s, 0);

		RunTicks(sim, 2);

		Assert.Equal(TileKind.Stairs, s.KindAt(pile));
		Assert.False(s.IsPile(pile));
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		RunTicks(sim, 100);
		// The carried unit, and the one the dug tile dropped.
		Assert.Equal(2, Stored(s, 25, 25));
	}

	[Fact]
	public void A_save_taken_mid_haul_round_trips_and_replays_identically()
	{
		foreach (var ticks in new[] { 3, 8, 15, 25 })
		{
			var sim = WorldWithLooseUnit(16, 14);
			Paint(sim, 20, 14);
			RunTicks(sim, ticks);
			var restored = SimJson.Deserialize(SimJson.Serialize(sim));

			Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
			RunTicks(sim, 40);
			RunTicks(restored, 40);
			Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
			Assert.Equal(1, Stored(restored.State, 20, 14));
		}
	}

	[Fact]
	public void The_same_seed_and_command_log_replay_a_haul_to_identical_state()
	{
		static Simulation Run()
		{
			var sim = Simulation.NewWorld(seed: 5);
			sim.Apply(new PaintPile(0, 10, 10, 11, 11));
			sim.Apply(new Designate(DesignationKind.DigStairs, 0, 14, 14, 18, 15));
			RunTicks(sim, 300);
			return sim;
		}

		var first = Run();
		var second = Run();

		Assert.Equal(SimJson.Serialize(first), SimJson.Serialize(second));
		Assert.True(first.State.Piles.Zip(first.State.TileItems.Chunk(2), (p, c) => p != 0 ? c.Sum() : 0).Sum() > 0,
			"Nothing was hauled to the Pile.");
	}

	[Fact]
	public void A_save_where_an_idle_goblin_carries_a_unit_is_rejected()
	{
		var sim = Simulation.NewWorld(seed: 1);
		sim.State.GoblinCarry[0] = (byte)Material.Dirt;

		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}
}
