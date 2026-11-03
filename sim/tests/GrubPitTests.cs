using System.Text.Json;
using System.Text.Json.Nodes;
using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>P3's Grub Pit: painting the Room, the Tend Job, and the Grub a Tend yields.</summary>
public class GrubPitTests
{
	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	static int At(SimState s, int x, int y, int z = 0) => s.TileIndex(x, y, z);

	static bool IsTending(SimState s, int g) => TaskOf(s, g) is GoblinTask.Approaching or GoblinTask.Tending;

	// The lone goblin 0 stands at (16, 16, 0) with full meters, so no Craving gets in the way, and nothing lies on the ground.
	static Simulation Lone()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		sim.State.GoblinGrub[0] = 100;
		sim.State.GoblinGrog[0] = 100;
		return sim;
	}

	static void PaintPit(Simulation sim, int x, int y, int x1 = -1, int y1 = -1, int z = 0) =>
		sim.Apply(new PaintRoom(RoomKind.GrubPit, z, x, y, x1 < 0 ? x : x1, y1 < 0 ? y : y1));

	static void PaintPile(Simulation sim, int x, int y) => sim.Apply(new PaintPile(0, x, y, x, y));

	static void Put(Simulation sim, Material material, int x, int y, int units = 1)
	{
		sim.State.AddItems(sim.State.TileIndex(x, y, 0), material, units);
		sim.RecheckReachability();
	}

	static int CountRooms(SimState s, RoomKind room)
	{
		var count = 0;
		for (var tile = 0; tile < s.TileKinds.Length; tile++)
			if (s.RoomAt(tile) == room)
				count++;
		return count;
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

	// ---- Painting

	[Fact]
	public void A_fresh_world_has_no_Rooms()
	{
		var s = Simulation.NewWorld(seed: 1).State;
		Assert.Equal(0, CountRooms(s, RoomKind.GrubPit));
		Assert.Equal(0, CountRooms(s, RoomKind.Pile));
	}

	[Fact]
	public void Painting_a_rectangle_makes_Grub_Pits_of_its_Floor_tiles_in_any_corner_order()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;

		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 7, 6, 4, 5));

		Assert.Equal(8, CountRooms(s, RoomKind.GrubPit));
		for (var y = 5; y <= 6; y++)
		for (var x = 4; x <= 7; x++)
			Assert.Equal(RoomKind.GrubPit, s.RoomAt(At(s, x, y)));
		// A Grub Pit stores nothing: it is not a Pile.
		Assert.False(s.IsPile(At(s, 4, 5)));
	}

	[Fact]
	public void Painting_clips_to_the_map_and_covers_nothing_when_wholly_off_it()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;

		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 30, 30, 40, 40));
		Assert.Equal(4, CountRooms(s, RoomKind.GrubPit));
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 40, 40, 50, 50));
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, -5, -5, -1, -1));
		Assert.Equal(4, CountRooms(s, RoomKind.GrubPit));
	}

	[Fact]
	public void Painting_skips_Solid_Stairs_and_Open_air_tiles()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.TileKinds[At(s, 6, 5)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 7, 5)] = (byte)TileKind.OpenAir;
		s.TileKinds[At(s, 8, 5)] = (byte)TileKind.Solid;

		PaintPit(sim, 5, 5, 8, 5);

		Assert.Equal(RoomKind.GrubPit, s.RoomAt(At(s, 5, 5)));
		Assert.Equal(1, CountRooms(s, RoomKind.GrubPit));
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 1, 0, 0, 31, 31));
		Assert.Equal(1, CountRooms(s, RoomKind.GrubPit));
	}

	[Fact]
	public void A_tile_holds_one_Room_so_painting_over_a_different_Room_skips_that_tile()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		PaintPile(sim, 5, 5);
		PaintPit(sim, 8, 5);

		PaintPit(sim, 4, 5, 9, 5);
		Assert.Equal(RoomKind.Pile, s.RoomAt(At(s, 5, 5)));
		Assert.Equal(5, CountRooms(s, RoomKind.GrubPit));

		// The other way round: a Pile skips the pits and keeps its own tile.
		sim.Apply(new PaintPile(0, 4, 5, 9, 5));
		Assert.Equal(RoomKind.Pile, s.RoomAt(At(s, 5, 5)));
		Assert.Equal(RoomKind.GrubPit, s.RoomAt(At(s, 8, 5)));
		Assert.Equal(1, CountRooms(s, RoomKind.Pile));
		Assert.Equal(5, CountRooms(s, RoomKind.GrubPit));
	}

	[Fact]
	public void Painting_a_Grub_Pit_over_stock_on_a_tile_leaves_it_loose()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var tile = At(s, 5, 5);
		s.AddItems(tile, Material.Stone, 4);

		PaintPit(sim, 5, 5);

		Assert.Equal(4, s.LooseCount(tile, Material.Stone));
		Assert.Equal(0, s.StoredCount(tile, Material.Stone));
	}

	[Fact]
	public void Painting_again_over_a_Grub_Pit_does_not_restart_what_it_is_spawning()
	{
		var sim = Lone();
		var s = sim.State;
		var pit = At(s, 16, 16);
		PaintPit(sim, 16, 16);
		RunTicks(sim, 20 + 50);
		Assert.Equal(3, s.FarmUnspawned[pit]);
		Assert.Equal(50, s.FarmTicks[pit]);

		PaintPit(sim, 14, 14, 18, 18);

		Assert.Equal(3, s.FarmUnspawned[pit]);
		Assert.Equal(50, s.FarmTicks[pit]);
	}

	[Fact]
	public void A_typed_removal_takes_only_its_own_Room()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		PaintPit(sim, 4, 4, 5, 4);
		PaintPile(sim, 6, 4);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 20, 20, 20, 20));

		sim.Apply(new RemoveRoom(RoomKind.GrubPit, 0, 0, 0, 31, 31));
		Assert.Equal(0, CountRooms(s, RoomKind.GrubPit));
		Assert.Equal(RoomKind.Pile, s.RoomAt(At(s, 6, 4)));
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(At(s, 20, 20)));

		PaintPit(sim, 4, 4, 5, 4);
		sim.Apply(new RemovePile(0, 0, 0, 31, 31));
		Assert.Equal(2, CountRooms(s, RoomKind.GrubPit));
		Assert.Equal(0, CountRooms(s, RoomKind.Pile));
	}

	[Fact]
	public void Cancelling_Designations_leaves_Grub_Pits_alone()
	{
		var sim = Simulation.NewWorld(seed: 1);
		PaintPit(sim, 4, 4);
		sim.Apply(new CancelDesignation(0, 0, 0, 31, 31));
		Assert.Equal(1, CountRooms(sim.State, RoomKind.GrubPit));
	}

	[Fact]
	public void Dig_stairs_clears_a_Grub_Pit_and_the_Grub_it_has_not_yet_spawned()
	{
		var sim = Lone();
		var s = sim.State;
		var pit = At(s, 20, 16);
		PaintPit(sim, 20, 16);
		// A Tend already done, with one Grub out and two to come.
		s.FarmUnspawned[pit] = 2;
		s.FarmTicks[pit] = 77;
		s.AddItems(pit, Material.Grub, 1);
		sim.RecheckReachability();
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 20, 16, 20, 16));

		RunTicks(sim, 200);

		Assert.Equal(TileKind.Stairs, s.KindAt(pit));
		Assert.Equal(RoomKind.None, s.RoomAt(pit));
		Assert.Equal(0, s.FarmUnspawned[pit]);
		Assert.Equal(0, s.FarmTicks[pit]);
		RunTicks(sim, 800);
		Assert.Equal(1, Total(s, Material.Grub));
	}

	[Fact]
	public void Dig_stairs_finishing_under_a_tender_frees_it_and_ends_the_Tend()
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		sim.State.GoblinCount = 2;
		var s = sim.State;
		for (var g = 0; g < 2; g++)
			s.GoblinGrub[g] = s.GoblinGrog[g] = 100;
		sim.RecheckReachability();
		var pit = At(s, 16, 16);
		PaintPit(sim, 16, 16);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 16, 16, 16));
		// Five ticks of digging left, against the tender's twenty.
		s.DigProgress[pit] = 15;

		RunTicks(sim, 3);
		Assert.Equal(GoblinTask.Tending, TaskOf(s, 0));
		RunTicks(sim, 5);

		Assert.Equal(TileKind.Stairs, s.KindAt(pit));
		Assert.Equal(RoomKind.None, s.RoomAt(pit));
		Assert.False(IsTending(s, 0));
		Assert.Equal(0, s.FarmUnspawned[pit]);
	}

	[Fact]
	public void Removing_a_tile_frees_a_goblin_still_walking_to_tend_it()
	{
		var sim = Lone();
		var s = sim.State;
		PaintPit(sim, 26, 16);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Approaching, TaskOf(s, 0));

		sim.Apply(new RemoveRoom(RoomKind.GrubPit, 0, 26, 16, 26, 16));

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	// ---- The Tend Job

	[Fact]
	public void An_untended_Grub_Pit_tile_offers_a_Tend_a_goblin_walks_to_and_works()
	{
		var sim = Lone();
		var s = sim.State;
		var pit = At(s, 21, 16);
		PaintPit(sim, 21, 16);

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Approaching, TaskOf(s, 0));
		Assert.Equal(pit, s.GoblinJobTile[0]);

		while (TaskOf(s, 0) == GoblinTask.Approaching)
			RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Tending, TaskOf(s, 0));
		Assert.Equal(21, s.GoblinX[0]);
		Assert.Equal(16, s.GoblinY[0]);
		Assert.Equal(pit, s.GoblinJobTile[0]);
	}

	[Fact]
	public void A_goblin_already_on_the_tile_starts_tending_at_once()
	{
		var sim = Lone();
		PaintPit(sim, 16, 16);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Tending, TaskOf(sim.State, 0));
	}

	[Fact]
	public void A_Tend_takes_20_ticks_and_yields_exactly_3_Grub_one_per_200_ticks()
	{
		var sim = Lone();
		var s = sim.State;
		var pit = At(s, 16, 16);
		PaintPit(sim, 16, 16);

		long done = -1;
		var spawns = new List<long>();
		var grub = 0;
		for (var i = 0; i < 1000; i++)
		{
			RunTicks(sim, 1);
			if (done < 0 && s.FarmUnspawned[pit] == ProductionRules.GrubPerTend)
				done = s.Tick;
			if (done >= 0)
				Assert.False(IsTending(s, 0), $"Tick {s.Tick}: tended again while the pit still spawned or held Grub.");
			var now = s.ItemCount(pit, Material.Grub);
			if (now > grub)
				spawns.Add(s.Tick);
			grub = now;
		}

		// The goblin claims the Tend on tick 1 and its 20th working tick is tick 20.
		Assert.Equal(20, done);
		Assert.Equal([done + 200, done + 400, done + 600], spawns);
		Assert.Equal(3, grub);
		Assert.Equal(0, s.FarmUnspawned[pit]);
	}

	[Fact]
	public void The_numbers_are_the_GDDs()
	{
		Assert.Equal(20, ProductionRules.TendTicks);
		Assert.Equal(3, ProductionRules.GrubPerTend);
		Assert.Equal(200, ProductionRules.GrubSpawnTicks);
	}

	[Fact]
	public void A_Starving_goblin_tends_at_half_speed()
	{
		var sim = Lone();
		var s = sim.State;
		s.GoblinGrub[0] = 0;
		var pit = At(s, 16, 16);
		PaintPit(sim, 16, 16);

		long done = -1;
		for (var i = 0; i < 100 && done < 0; i++)
		{
			RunTicks(sim, 1);
			if (s.FarmUnspawned[pit] == 3)
				done = s.Tick;
		}

		Assert.Equal(40, done);
	}

	[Fact]
	public void Hauling_the_Grub_away_lets_the_pit_be_tended_again()
	{
		var sim = Lone();
		var s = sim.State;
		var pit = At(s, 16, 16);
		var pile = At(s, 10, 10);
		PaintPit(sim, 16, 16);
		PaintPile(sim, 10, 10);

		var tends = 0;
		var tending = false;
		for (var i = 0; i < 1100; i++)
		{
			RunTicks(sim, 1);
			var now = TaskOf(s, 0) == GoblinTask.Tending;
			if (now && !tending)
				tends++;
			tending = now;
		}

		Assert.Equal(2, tends);
		// Three Grub from the first Tend, and the fourth (the second Tend's first) was spawned and hauled.
		Assert.Equal(4, s.StoredCount(pile, Material.Grub));
		Assert.Equal(0, s.LooseCount(pit, Material.Grub));
	}

	[Fact]
	public void No_Tend_is_offered_while_Grub_lies_on_the_tile_even_with_nothing_spawning()
	{
		var sim = Lone();
		var s = sim.State;
		var pit = At(s, 16, 16);
		s.AddItems(pit, Material.Grub, 1);
		PaintPit(sim, 16, 16);

		RunTicks(sim, 300);

		Assert.False(IsTending(s, 0));
		Assert.Equal(0, s.FarmUnspawned[pit]);
	}

	[Fact]
	public void Grub_landing_on_the_tile_frees_a_goblin_walking_to_tend_it_and_the_Tend_returns_once_hauled()
	{
		var sim = Lone();
		var s = sim.State;
		var pit = At(s, 26, 16);
		PaintPit(sim, 26, 16);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Approaching, TaskOf(s, 0));

		Put(sim, Material.Grub, 26, 16);
		RunTicks(sim, 2);
		Assert.False(IsTending(s, 0));

		RunTicks(sim, 100);
		Assert.False(IsTending(s, 0));

		PaintPile(sim, 16, 20);
		RunTicks(sim, 400);

		Assert.Equal(1, s.StoredCount(At(s, 16, 20), Material.Grub));
		Assert.True(s.FarmUnspawned[pit] > 0, "The pit was not tended once its Grub was hauled.");
	}

	[Fact]
	public void The_spawned_Grub_is_loose_so_a_goblin_with_a_Craving_eats_it_where_it_lies()
	{
		var sim = Lone();
		var s = sim.State;
		var pit = At(s, 16, 16);
		PaintPit(sim, 16, 16);
		RunTicks(sim, 20 + 100);
		// Hungry, but nothing to eat yet: the goblin keeps working until the first Grub spawns.
		s.GoblinGrub[0] = 29;

		RunTicks(sim, 200);

		Assert.True(s.GoblinGrub[0] >= 70, $"The goblin never ate: Grub meter {s.GoblinGrub[0]}.");
		Assert.Equal(0, s.ItemCount(pit, Material.Grub));
		Assert.Equal(2, s.FarmUnspawned[pit]);
	}

	[Fact]
	public void Spawned_Grub_is_a_Haul_a_waiting_goblin_notices_without_any_other_change()
	{
		var sim = Lone();
		var s = sim.State;
		PaintPit(sim, 12, 16);
		PaintPile(sim, 20, 20);

		RunTicks(sim, 700);

		Assert.True(s.StoredCount(At(s, 20, 20), Material.Grub) >= 2);
	}

	// ---- Which Job is nearest

	// Runs one tick and reports the Job tile of goblin 0 and its Task.
	static (GoblinTask Task, int Tile) Claimed(Simulation sim)
	{
		RunTicks(sim, 1);
		return (TaskOf(sim.State, 0), sim.State.GoblinJobTile[0]);
	}

	[Fact]
	public void A_Tend_nearer_than_a_dig_is_claimed_first_and_a_dig_nearer_than_a_Tend_is_claimed_first()
	{
		var near = Lone();
		PaintPit(near, 16, 13);
		near.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 22, 16, 22));
		Assert.Equal((GoblinTask.Approaching, At(near.State, 16, 13)), Claimed(near));

		var far = Lone();
		PaintPit(far, 16, 22);
		far.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 13, 16, 13));
		Assert.Equal((GoblinTask.Walking, At(far.State, 16, 13)), Claimed(far));
	}

	[Fact]
	public void A_Tend_and_a_Haul_compete_by_distance()
	{
		var haulNear = Lone();
		PaintPile(haulNear, 30, 30);
		Put(haulNear, Material.Dirt, 13, 16);
		PaintPit(haulNear, 16, 22);
		Assert.Equal((GoblinTask.Fetching, At(haulNear.State, 13, 16)), Claimed(haulNear));

		var tendNear = Lone();
		PaintPile(tendNear, 30, 30);
		Put(tendNear, Material.Dirt, 16, 25);
		PaintPit(tendNear, 13, 16);
		Assert.Equal((GoblinTask.Approaching, At(tendNear.State, 13, 16)), Claimed(tendNear));
	}

	[Fact]
	public void A_tie_between_a_Tend_and_a_Haul_goes_to_the_lowest_tile_index()
	{
		var pitFirst = Lone();
		PaintPile(pitFirst, 16, 28);
		Put(pitFirst, Material.Dirt, 18, 16);
		PaintPit(pitFirst, 14, 16);
		Assert.Equal((GoblinTask.Approaching, At(pitFirst.State, 14, 16)), Claimed(pitFirst));

		var dirtFirst = Lone();
		PaintPile(dirtFirst, 16, 28);
		Put(dirtFirst, Material.Dirt, 14, 16);
		PaintPit(dirtFirst, 18, 16);
		Assert.Equal((GoblinTask.Fetching, At(dirtFirst.State, 14, 16)), Claimed(dirtFirst));
	}

	[Fact]
	public void Nearest_means_by_path_not_by_straight_line()
	{
		var sim = Lone();
		var s = sim.State;
		// A wall across row 15 from x = 4 to 28 puts the pit two tiles away as the crow flies but a long walk around.
		for (var x = 4; x <= 28; x++)
			s.TileKinds[At(s, x, 15)] = (byte)TileKind.Solid;
		sim.RecheckReachability();
		PaintPit(sim, 16, 13);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 22, 16, 22));

		Assert.Equal((GoblinTask.Walking, At(s, 16, 22)), Claimed(sim));
	}

	[Fact]
	public void A_Grub_Pit_no_goblin_can_reach_offers_no_Tend()
	{
		var sim = Lone();
		var s = sim.State;
		for (var x = 0; x < s.Width; x++)
			s.TileKinds[At(s, x, 12)] = (byte)TileKind.Solid;
		sim.RecheckReachability();
		PaintPit(sim, 16, 8);

		RunTicks(sim, 200);

		Assert.False(IsTending(s, 0));
	}

	[Fact]
	public void Only_one_goblin_claims_a_tile_and_two_tiles_make_two_Jobs()
	{
		var one = NewWorldWithFiveGrunts(seed: 1);
		one.State.GoblinCount = 2;
		one.RecheckReachability();
		PaintPit(one, 16, 16);
		var firstTended = false;
		for (var i = 0; i < 60; i++)
		{
			RunTicks(one, 1);
			firstTended |= IsTending(one.State, 0);
			Assert.False(IsTending(one.State, 1), $"Tick {one.State.Tick}: a second goblin claimed the one tile.");
		}
		Assert.True(firstTended);

		var two = NewWorldWithFiveGrunts(seed: 1);
		two.State.GoblinCount = 2;
		two.RecheckReachability();
		PaintPit(two, 15, 16, 16, 16);
		RunTicks(two, 1);
		Assert.Equal(GoblinTask.Tending, TaskOf(two.State, 0));
		Assert.Equal(GoblinTask.Tending, TaskOf(two.State, 1));
		Assert.NotEqual(two.State.GoblinJobTile[0], two.State.GoblinJobTile[1]);
	}

	[Fact]
	public void Another_goblin_takes_over_a_tile_a_Craving_freed()
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		sim.State.GoblinCount = 2;
		sim.State.GoblinGrub[0] = 100;
		sim.State.GoblinGrog[0] = 100;
		sim.State.GoblinGrub[1] = 100;
		sim.State.GoblinGrog[1] = 100;
		sim.RecheckReachability();
		PaintPit(sim, 16, 16);
		RunTicks(sim, 5);
		Assert.Equal(GoblinTask.Tending, TaskOf(sim.State, 0));

		// Goblin 0 is hungry and food lies close by, so it leaves the Tend; goblin 1 takes the freed Job.
		sim.State.GoblinGrub[0] = 20;
		Put(sim, Material.Grub, 18, 16);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Seeking, TaskOf(sim.State, 0));
		Assert.True(IsTending(sim.State, 1));
	}

	// ---- Interruptions

	[Fact]
	public void A_Craving_interrupting_a_Tend_loses_its_progress_and_the_Job_is_offered_again()
	{
		var sim = Lone();
		var s = sim.State;
		var pit = At(s, 16, 16);
		s.GoblinGrub[0] = 30;
		// The Grub meter drops to 29 on tick 25, after nine working ticks of the Tend.
		s.Tick = 15;
		Put(sim, Material.Grub, 20, 16);
		PaintPit(sim, 16, 16);

		RunTicks(sim, 10);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Equal(0, s.FarmUnspawned[pit]);
		Assert.True(FarmSystem.IsTendOffered(s, pit));

		// After the meal the whole 20 ticks must be worked again.
		while (TaskOf(s, 0) != GoblinTask.Tending)
			RunTicks(sim, 1);
		var tendingTicks = 0;
		while (s.FarmUnspawned[pit] == 0)
		{
			tendingTicks++;
			RunTicks(sim, 1);
		}
		Assert.Equal(ProductionRules.TendTicks, tendingTicks);
	}

	[Fact]
	public void Removing_a_tile_mid_Tend_frees_the_tender_and_yields_nothing()
	{
		var sim = Lone();
		var s = sim.State;
		var pit = At(s, 16, 16);
		PaintPit(sim, 16, 16);
		RunTicks(sim, 10);
		Assert.Equal(GoblinTask.Tending, TaskOf(s, 0));

		sim.Apply(new RemoveRoom(RoomKind.GrubPit, 0, 16, 16, 16, 16));

		Assert.False(IsTending(s, 0));
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
		RunTicks(sim, 800);
		Assert.Equal(0, Total(s, Material.Grub));
		Assert.Equal(0, s.FarmUnspawned[pit]);
	}

	[Fact]
	public void Removing_a_tile_mid_spawn_discards_the_Grub_it_has_not_yet_spawned()
	{
		var sim = Lone();
		var s = sim.State;
		var pit = At(s, 16, 16);
		PaintPit(sim, 16, 16);
		// Tend done on tick 20, first Grub on tick 220.
		RunTicks(sim, 250);
		Assert.Equal(1, s.ItemCount(pit, Material.Grub));
		Assert.Equal(2, s.FarmUnspawned[pit]);

		sim.Apply(new RemoveRoom(RoomKind.GrubPit, 0, 16, 16, 16, 16));
		Assert.Equal(0, s.FarmUnspawned[pit]);
		Assert.Equal(0, s.FarmTicks[pit]);

		RunTicks(sim, 1000);
		Assert.Equal(1, Total(s, Material.Grub));
		// Painting it again starts clean: the old timer is not waiting to fire.
		sim.Apply(new RemoveRoom(RoomKind.GrubPit, 0, 16, 16, 16, 16));
		PaintPit(sim, 16, 16);
		Assert.Equal(0, s.FarmUnspawned[pit]);
	}

	// ---- Rules every tick

	[Fact]
	public void Tasks_hold_their_rules_every_tick_across_a_busy_warren_of_pits_piles_digs_and_removals()
	{
		var sim = Simulation.NewWorld(seed: 5);
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 12, 12, 14, 13));
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 18, 12, 20, 12));
		sim.Apply(new PaintPile(0, 15, 18, 17, 19));
		sim.Apply(new Designate(DesignationKind.Dig, 1, 14, 14, 18, 14));
		var log = new (int Tick, Command Command)[]
		{
			(300, new Designate(DesignationKind.DigStairs, 0, 13, 12, 13, 12)),
			(450, new RemoveRoom(RoomKind.GrubPit, 0, 18, 12, 20, 12)),
			(600, new PaintRoom(RoomKind.GrubPit, 0, 10, 10, 22, 22)),
			(900, new RemoveRoom(RoomKind.GrubPit, 0, 0, 0, 31, 31)),
			(1000, new RemovePile(0, 0, 0, 31, 31)),
		};

		for (var tick = 0; tick < 1500; tick++)
		{
			foreach (var (at, command) in log)
				if (at == tick)
					sim.Apply(command);
			RunTicks(sim, 1);
		}

		Assert.Equal(0, CountRooms(sim.State, RoomKind.GrubPit));
	}

	// ---- FindViolation

	static SimState Tender(out int pit)
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		sim.State.GoblinCount = 2;
		var s = sim.State;
		pit = At(s, 16, 16);
		PaintPit(sim, 16, 16, 17, 16);
		s.GoblinGrub[0] = s.GoblinGrub[1] = 100;
		s.GoblinGrog[0] = s.GoblinGrog[1] = 100;
		return s;
	}

	[Fact]
	public void FindViolation_accepts_a_goblin_tending_a_pit_with_progress()
	{
		var s = Tender(out var pit);
		s.GoblinTask[0] = (byte)GoblinTask.Tending;
		s.GoblinJobTile[0] = pit;
		s.GoblinWorkTicks[0] = (byte)(ProductionRules.TendTicks - 1);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_accepts_a_goblin_walking_to_a_pit()
	{
		var s = Tender(out var pit);
		s.GoblinTask[0] = (byte)GoblinTask.Approaching;
		s.GoblinJobTile[0] = pit + 1;
		s.GoblinX[0] = 3;
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_rejects_tending_a_tile_that_is_not_a_Grub_Pit()
	{
		var s = Tender(out var pit);
		s.GoblinTask[0] = (byte)GoblinTask.Tending;
		s.GoblinJobTile[0] = pit - 1;
		s.GoblinX[0] = 15;
		Assert.NotNull(GoblinTasks.FindViolation(s));

		s.Rooms[pit - 1] = (byte)RoomKind.GrubPit;
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_rejects_a_Tend_of_a_pit_that_is_spawning()
	{
		var s = Tender(out var pit);
		s.GoblinTask[0] = (byte)GoblinTask.Approaching;
		s.GoblinJobTile[0] = pit;
		s.FarmUnspawned[pit] = 1;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_rejects_two_goblins_tending_one_tile_but_not_two_tiles()
	{
		var s = Tender(out var pit);
		s.GoblinTask[0] = s.GoblinTask[1] = (byte)GoblinTask.Approaching;
		s.GoblinJobTile[0] = s.GoblinJobTile[1] = pit;
		Assert.NotNull(GoblinTasks.FindViolation(s));

		s.GoblinJobTile[1] = pit + 1;
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_rejects_a_dig_and_a_Tend_only_when_the_Tasks_clash_not_when_they_share_a_tile()
	{
		var s = Tender(out var pit);
		s.Designations[pit] = (byte)DesignationKind.DigStairs;
		s.GoblinTask[0] = (byte)GoblinTask.Walking;
		s.GoblinJobTile[0] = pit;
		s.GoblinTask[1] = (byte)GoblinTask.Approaching;
		s.GoblinJobTile[1] = pit;
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_rejects_tending_from_elsewhere_and_a_Tend_with_the_wrong_work_ticks()
	{
		var s = Tender(out var pit);
		s.GoblinTask[0] = (byte)GoblinTask.Tending;
		s.GoblinJobTile[0] = pit + 1;
		Assert.NotNull(GoblinTasks.FindViolation(s));

		s.GoblinJobTile[0] = pit;
		s.GoblinWorkTicks[0] = (byte)ProductionRules.TendTicks;
		Assert.NotNull(GoblinTasks.FindViolation(s));

		s.GoblinWorkTicks[0] = 0;
		s.GoblinTask[0] = (byte)GoblinTask.Approaching;
		s.GoblinWorkTicks[0] = 1;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_rejects_a_tender_that_wanders_or_carries()
	{
		var s = Tender(out var pit);
		s.GoblinTask[0] = (byte)GoblinTask.Tending;
		s.GoblinJobTile[0] = pit;
		s.GoblinWanderTile[0] = pit + 3;
		Assert.NotNull(GoblinTasks.FindViolation(s));

		s.GoblinWanderTile[0] = -1;
		s.GoblinCarry[0] = (byte)Material.Dirt;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	// ---- Saves and replays

	static Simulation Replay(int seed, (int Tick, Command Command)[] log, int ticks)
	{
		var sim = Simulation.NewWorld(seed);
		RunLog(sim, log, from: 0, to: ticks);
		return sim;
	}

	static void RunLog(Simulation sim, (int Tick, Command Command)[] log, int from, int to)
	{
		for (var tick = from; tick < to; tick++)
		{
			foreach (var (at, command) in log)
				if (at == tick)
					sim.Apply(command);
			RunTicks(sim, 1);
		}
	}

	[Fact]
	public void Grub_Pits_survive_the_json_round_trip()
	{
		var sim = Simulation.NewWorld(seed: 1);
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 3, 3, 6, 4));
		sim.Apply(new PaintPile(0, 3, 5, 4, 5));

		var restored = SimJson.Deserialize(SimJson.Serialize(sim));

		Assert.Equal(sim.State.Rooms, restored.State.Rooms);
		Assert.Equal(8, CountRooms(restored.State, RoomKind.GrubPit));
		Assert.Equal(2, CountRooms(restored.State, RoomKind.Pile));
		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
	}

	[Fact]
	public void A_save_with_a_tile_mid_spawn_round_trips_and_replays_the_rest_of_the_run_identically()
	{
		var log = new (int Tick, Command Command)[]
		{
			(2, new PaintRoom(RoomKind.GrubPit, 0, 14, 14, 16, 15)),
			(3, new PaintPile(0, 18, 18, 19, 19)),
			(600, new RemoveRoom(RoomKind.GrubPit, 0, 15, 14, 15, 15)),
			(900, new Designate(DesignationKind.DigStairs, 0, 16, 14, 16, 14)),
			(1100, new PaintRoom(RoomKind.GrubPit, 0, 15, 14, 15, 15)),
		};
		var original = Simulation.NewWorld(seed: 12);
		RunLog(original, log, from: 0, to: 250);

		// Some tile is part-way through spawning: units still to come, and a timer part-way round.
		var s = original.State;
		var mid = Enumerable.Range(0, s.FarmUnspawned.Length).Where(t => s.FarmUnspawned[t] > 0 && s.FarmTicks[t] > 0).ToList();
		Assert.NotEmpty(mid);
		var restored = SimJson.Deserialize(SimJson.Serialize(original));
		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));

		RunLog(original, log, from: 250, to: 1500);
		RunLog(restored, log, from: 250, to: 1500);

		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));
		Assert.Equal(SimJson.Serialize(Replay(12, log, 1500)), SimJson.Serialize(original));
	}

	static void AssertRejected(Action<SimState> corrupt)
	{
		var sim = Simulation.NewWorld(seed: 1);
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 5, 5, 5, 5));
		sim.Apply(new PaintPile(0, 6, 5, 6, 5));
		corrupt(sim.State);
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Theory]
	[InlineData(TileKind.Solid)]
	[InlineData(TileKind.Stairs)]
	[InlineData(TileKind.OpenAir)]
	public void A_save_with_a_Grub_Pit_on_a_non_Floor_tile_is_rejected(TileKind kind) =>
		AssertRejected(s => s.TileKinds[At(s, 5, 5)] = (byte)kind);

	[Fact]
	public void A_save_with_an_unknown_Room_or_a_Pile_in_the_Rooms_array_is_rejected()
	{
		AssertRejected(s => s.Rooms[At(s, 5, 5)] = 9);
		AssertRejected(s => s.Rooms[At(s, 7, 5)] = (byte)RoomKind.Pile);
	}

	[Fact]
	public void A_save_with_two_Rooms_on_one_tile_is_rejected() =>
		AssertRejected(s => s.Rooms[At(s, 6, 5)] = (byte)RoomKind.GrubPit);

	[Theory]
	[InlineData("null")]
	[InlineData("[]")]
	[InlineData("[0,0,0]")]
	public void A_save_with_a_missing_or_wrong_length_Rooms_or_pit_array_is_rejected(string value)
	{
		foreach (var name in new[] { "Rooms", "FarmUnspawned", "FarmTicks" })
		{
			var node = JsonNode.Parse(SimJson.Serialize(Simulation.NewWorld(seed: 1)))!;
			node[name] = JsonNode.Parse(value);
			Assert.Throws<JsonException>(() => SimJson.Deserialize(node.ToJsonString()));
		}
	}

	[Fact]
	public void A_save_without_the_Rooms_array_is_rejected_not_migrated()
	{
		var node = JsonNode.Parse(SimJson.Serialize(Simulation.NewWorld(seed: 1)))!.AsObject();
		node.Remove("Rooms");
		Assert.Throws<JsonException>(() => SimJson.Deserialize(node.ToJsonString()));
	}

	[Fact]
	public void A_save_with_unspawned_Grub_beyond_a_Tend_or_off_a_Grub_Pit_is_rejected()
	{
		AssertRejected(s => s.FarmUnspawned[At(s, 5, 5)] = (byte)(ProductionRules.GrubPerTend + 1));
		AssertRejected(s => s.FarmUnspawned[At(s, 9, 9)] = 1);
		AssertRejected(s => s.FarmUnspawned[At(s, 6, 5)] = 1);
	}

	[Fact]
	public void A_save_with_a_spawn_timer_past_its_period_or_running_with_nothing_to_spawn_is_rejected()
	{
		AssertRejected(s =>
		{
			s.FarmUnspawned[At(s, 5, 5)] = 2;
			s.FarmTicks[At(s, 5, 5)] = (byte)ProductionRules.GrubSpawnTicks;
		});
		AssertRejected(s => s.FarmTicks[At(s, 5, 5)] = 4);
	}

	[Fact]
	public void A_save_with_a_goblin_tending_a_tile_that_is_not_a_Grub_Pit_is_rejected()
	{
		AssertRejected(s =>
		{
			s.GoblinTask[0] = (byte)GoblinTask.Approaching;
			s.GoblinJobTile[0] = At(s, 9, 9);
		});
	}

	[Fact]
	public void A_save_with_a_goblin_mid_Tend_is_accepted_and_resumes_where_it_was()
	{
		var sim = Lone();
		PaintPit(sim, 16, 16);
		RunTicks(sim, 10);
		Assert.Equal(GoblinTask.Tending, TaskOf(sim.State, 0));
		Assert.Equal(10, sim.State.GoblinWorkTicks[0]);

		var restored = SimJson.Deserialize(SimJson.Serialize(sim));
		RunTicks(sim, 10);
		RunTicks(restored, 10);

		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
		Assert.Equal(3, restored.State.FarmUnspawned[At(restored.State, 16, 16)]);
	}
}
