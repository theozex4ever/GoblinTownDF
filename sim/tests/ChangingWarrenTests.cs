using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>Hauling while the player reshapes the warren: Piles removed mid-haul, tiles holding several loose units, stairs dug on a Pile.</summary>
public class ChangingWarrenTests
{
	static void Paint(Simulation sim, int x, int y) => sim.Apply(new PaintPile(0, x, y, x, y));

	static void Remove(Simulation sim, int x, int y) => sim.Apply(new RemovePile(0, x, y, x, y));

	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	static int Stored(SimState s, int x, int y, Material material = Material.Dirt) => s.StoredCount(s.TileIndex(x, y, 0), material);

	static int HaulersOn(SimState s, int tile) =>
		Enumerable.Range(0, s.GoblinCount).Count(g => GoblinTasks.IsFetchTask(TaskOf(s, g)) && s.GoblinJobTile[g] == tile);

	// Units on the map plus units in goblins' hands.
	static int Units(SimState s) => s.TileItems.Sum() + s.GoblinCarry.Take(s.GoblinCount).Count(c => c != (byte)Material.None);

	[Fact]
	public void Removing_the_target_Pile_while_carrying_sends_the_goblin_to_the_next_nearest_Pile()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		s.AddItems(s.TileIndex(16, 14, 0), Material.Dirt, 1);
		Paint(sim, 20, 14);
		Paint(sim, 20, 20);
		RunTicks(sim, 15);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));

		Remove(sim, 20, 14);

		Assert.Null(GoblinTasks.FindViolation(s));
		RunTicks(sim, 100);
		Assert.Equal(1, Stored(s, 20, 20));
		Assert.Equal(1, Units(s));
	}

	[Fact]
	public void Removing_every_Pile_while_fetching_or_picking_up_frees_the_goblin_and_while_carrying_leaves_it_waiting_with_the_unit()
	{
		foreach (var ticks in new[] { 3, 8, 15, 20 })
		{
			var sim = NewWorldWithOneGrunt(seed: 1);
			var s = sim.State;
			var unit = s.TileIndex(16, 14, 0);
			s.AddItems(unit, Material.Stone, 1);
			Paint(sim, 20, 14);
			RunTicks(sim, ticks);
			var carrying = TaskOf(s, 0) == GoblinTask.Carrying;

			Remove(sim, 20, 14);
			RunTicks(sim, 120);

			Assert.Equal(1, Units(s));
			if (carrying)
			{
				// A carrier keeps its unit and waits for storage; it does not put it down.
				Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
				Assert.Equal((byte)Material.Stone, s.GoblinCarry[0]);
				Assert.Equal(0, s.TileItems.Sum());
			}
			else
			{
				Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
				Assert.True(TaskOf(s, 0) is GoblinTask.Idle or GoblinTask.Wandering);
				Assert.Equal(1, s.LooseCount(unit, Material.Stone));
				// A Pile makes the Haul claimable again.
				Assert.False(s.IsHaulReachable(unit));
			}
			Paint(sim, 25, 25);
			RunTicks(sim, 200);
			Assert.Equal(1, Stored(s, 25, 25, Material.Stone));
		}
	}

	[Fact]
	public void Removing_the_last_Pile_under_a_dropping_goblin_leaves_it_carrying_and_waiting_for_storage()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		s.AddItems(s.TileIndex(16, 14, 0), Material.Dirt, 1);
		Paint(sim, 20, 14);
		RunTicks(sim, 25);
		Assert.Equal(GoblinTask.Dropping, TaskOf(s, 0));

		Remove(sim, 20, 14);
		RunTicks(sim, 60);

		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 0));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[0]);
		Assert.Equal(0, s.TileItems.Sum());
		Assert.Equal(1, Units(s));
		Paint(sim, 25, 25);
		RunTicks(sim, 200);
		Assert.Equal(1, Stored(s, 25, 25));
	}

	[Fact]
	public void A_tile_with_three_loose_units_is_hauled_in_three_trips_and_no_more_than_three_goblins_claim_it()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(12, 12, 0);
		s.AddItems(tile, Material.Dirt, 2);
		s.AddItems(tile, Material.Stone, 1);
		Paint(sim, 25, 25);
		sim.RecheckReachability();

		var most = 0;
		for (var i = 0; i < 300; i++)
		{
			RunTicks(sim, 1);
			most = Math.Max(most, HaulersOn(s, tile));
			Assert.True(HaulersOn(s, tile) <= s.ItemCount(tile, Material.Dirt) + s.ItemCount(tile, Material.Stone));
		}

		Assert.Equal(3, most);
		Assert.Equal(2, Stored(s, 25, 25));
		Assert.Equal(1, Stored(s, 25, 25, Material.Stone));
		Assert.Equal(3, Units(s));
	}

	[Fact]
	public void With_more_goblins_than_units_the_spare_goblins_do_not_claim_the_tile()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(12, 12, 0);
		s.AddItems(tile, Material.Dirt, 2);
		Paint(sim, 25, 25);
		sim.RecheckReachability();

		RunTicks(sim, 2);

		Assert.Equal(2, HaulersOn(s, tile));
		Assert.Equal(3, Enumerable.Range(0, s.GoblinCount).Count(g => TaskOf(s, g) is GoblinTask.Idle or GoblinTask.Wandering));
	}

	[Fact]
	public void A_removed_Pile_tiles_stock_becomes_loose_units_that_are_each_hauled_to_another_Pile()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var old = s.TileIndex(14, 14, 0);
		Paint(sim, 14, 14);
		s.AddItems(old, Material.Dirt, 3);
		Paint(sim, 25, 25);

		Remove(sim, 14, 14);
		Assert.Equal(3, s.LooseCount(old, Material.Dirt));
		RunTicks(sim, 400);

		Assert.Equal(3, Stored(s, 25, 25));
		Assert.Equal(0, s.ItemCount(old, Material.Dirt));
		Assert.Equal(3, Units(s));
	}

	[Fact]
	public void Painting_a_Pile_over_a_tile_with_several_claims_frees_every_claiming_goblin()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(12, 12, 0);
		s.AddItems(tile, Material.Dirt, 2);
		s.AddItems(tile, Material.Stone, 1);
		Paint(sim, 25, 25);
		sim.RecheckReachability();
		RunTicks(sim, 2);
		Assert.Equal(3, HaulersOn(s, tile));

		Paint(sim, 12, 12);

		Assert.Null(GoblinTasks.FindViolation(s));
		Assert.Equal(0, HaulersOn(s, tile));
		RunTicks(sim, 100);
		Assert.Equal(3, Units(s));
	}

	[Fact]
	public void Digging_stairs_on_a_Pile_tile_makes_its_stock_and_the_dug_unit_loose_each_with_its_own_Haul()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(16, 15, 0);
		Paint(sim, 16, 15);
		s.AddItems(tile, Material.Stone, 2);
		Paint(sim, 25, 25);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 15, 16, 15));

		RunTicks(sim, 20);

		Assert.Equal(TileKind.Stairs, s.KindAt(tile));
		Assert.False(s.IsPile(tile));
		Assert.Equal(2, s.LooseCount(tile, Material.Stone));
		Assert.Equal(1, s.LooseCount(tile, Material.Dirt));
		Assert.True(s.IsHaulReachable(tile));

		RunTicks(sim, 400);
		Assert.Equal(2, Stored(s, 25, 25, Material.Stone));
		Assert.Equal(1, Stored(s, 25, 25, Material.Dirt));
	}

	[Fact]
	public void FindViolation_allows_one_claim_per_loose_unit_and_flags_more()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(12, 12, 0);
		s.AddItems(tile, Material.Dirt, 1);
		s.AddItems(tile, Material.Stone, 1);
		Paint(sim, 25, 25);
		GoblinTasks.BeginFetch(s, 0, tile, pickingUp: false);
		GoblinTasks.BeginFetch(s, 1, tile, pickingUp: false);
		Assert.Null(GoblinTasks.FindViolation(s));

		GoblinTasks.BeginFetch(s, 2, tile, pickingUp: false);

		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void A_long_run_of_random_Commands_loses_and_invents_no_units_and_never_overfills_a_Pile()
	{
		var waits = 0;
		foreach (var seed in new[] { 1, 2, 3 })
		{
			var sim = Simulation.NewWorld(seed);
			var s = sim.State;
			s.AddItems(s.TileIndex(10, 10, 0), Material.Stone, 3);
			s.AddItems(s.TileIndex(20, 12, 0), Material.Dirt, 2);
			sim.RecheckReachability();
			var rng = new Random(seed);
			var dug = 0;
			var start = Units(s);

			for (var tick = 0; tick < 3000; tick++)
			{
				if (tick % 7 == 0)
					sim.Apply(RandomCommand(rng));
				sim.Advance(0.1);
				dug += sim.DrainEvents().OfType<TileDug>().Count();
				Assert.True(GoblinTasks.FindViolation(s) is null, $"Seed {seed} tick {s.Tick}: {GoblinTasks.FindViolation(s)}");
				Assert.True(Units(s) == start + dug, $"Seed {seed} tick {s.Tick}: {Units(s)} units, expected {start + dug}.");
				for (var tile = 0; tile < s.Piles.Length; tile++)
					if (s.IsPile(tile))
						Assert.True(s.ItemCount(tile, Material.Dirt) <= SimState.PileCapacity && s.ItemCount(tile, Material.Stone) <= SimState.PileCapacity, $"Seed {seed} tick {s.Tick}: Pile tile {tile} is overfull.");
				for (var g = 0; g < s.GoblinCount; g++)
					// A carrier with no Pile to take its unit keeps it and waits.
					if (TaskOf(s, g) == GoblinTask.Carrying && !s.HasStorageRoom((Material)s.GoblinCarry[g]))
						waits++;
			}

			Assert.True(dug > 0, "The run dug nothing.");
			SimJson.Deserialize(SimJson.Serialize(sim));
		}

		Assert.True(waits > 0, "No carrier ever waited for storage, so the run never reached the waiting path.");
	}

	[Fact]
	public void Replaying_the_same_random_Commands_gives_identical_state()
	{
		static string Run()
		{
			var sim = Simulation.NewWorld(seed: 9);
			var rng = new Random(9);
			for (var tick = 0; tick < 1500; tick++)
			{
				if (tick % 7 == 0)
					sim.Apply(RandomCommand(rng));
				sim.Advance(0.1);
			}
			return SimJson.Serialize(sim);
		}

		Assert.Equal(Run(), Run());
	}

	// Clustered near the centre so the Commands overlap goblins, Piles, loose units and each other.
	static Command RandomCommand(Random rng)
	{
		var z = rng.Next(3) == 0 ? rng.Next(3) : 0;
		var x = 11 + rng.Next(11);
		var y = 11 + rng.Next(11);
		var x1 = x + rng.Next(4);
		var y1 = y + rng.Next(4);
		return rng.Next(6) switch
		{
			0 => new Designate(DesignationKind.Dig, z, x, y, x1, y1),
			1 or 2 => new Designate(DesignationKind.DigStairs, z, x, y, x1, y1),
			3 => new CancelDesignation(z, x, y, x1, y1),
			4 => new PaintPile(z, x, y, x + rng.Next(2), y + rng.Next(2)),
			// Wide, so a removal often takes every Pile at once.
			_ => new RemovePile(z, x - 10, y - 10, x + 10, y + 10),
		};
	}
}
