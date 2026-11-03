using System.Text.Json;
using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>P3's Fungus Farm: painting it below the Surface, and the one Fungus a Tend yields after its growth timer.</summary>
public class FungusFarmTests
{
	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	static int At(SimState s, int x, int y, int z = 1) => s.TileIndex(x, y, z);

	static bool IsTending(SimState s, int g) => TaskOf(s, g) is GoblinTask.Approaching or GoblinTask.Tending;

	// Turns a rectangle of a level into Floor: a hollowed-out room, standing in for what the goblins would dig.
	static void Hollow(Simulation sim, int z, int x0, int y0, int x1, int y1)
	{
		var s = sim.State;
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
			s.TileKinds[At(s, x, y, z)] = (byte)TileKind.Floor;
		sim.RecheckReachability();
	}

	// Stairs from the Surface down to z1 at (16, 15), and z1 Floor around its foot from (10, 15) to (22, 18).
	static void LinkUnderground(Simulation sim)
	{
		var s = sim.State;
		s.TileKinds[At(s, 16, 15, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 16, 15, 1)] = (byte)TileKind.Stairs;
		Hollow(sim, 1, 10, 16, 22, 18);
	}

	// Goblin 0 alone at (16, 16, 0) with full meters, so no Craving gets in the way, and Floor on z1 below the stairs.
	static Simulation Lone()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		sim.State.GoblinGrub[0] = 100;
		sim.State.GoblinGrog[0] = 100;
		LinkUnderground(sim);
		return sim;
	}

	static void PaintFarm(Simulation sim, int x, int y, int x1 = -1, int y1 = -1, int z = 1) =>
		sim.Apply(new PaintRoom(RoomKind.FungusFarm, z, x, y, x1 < 0 ? x : x1, y1 < 0 ? y : y1));

	static void PaintPit(Simulation sim, int x, int y, int z = 1) =>
		sim.Apply(new PaintRoom(RoomKind.GrubPit, z, x, y, x, y));

	static void PaintPile(Simulation sim, int x, int y, int z = 0) => sim.Apply(new PaintPile(z, x, y, x, y));

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
	public void Fungus_Farms_paint_on_Floor_at_z1_or_deeper_and_a_z0_tile_is_skipped()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		Hollow(sim, 1, 4, 4, 7, 5);
		Hollow(sim, 2, 4, 4, 5, 4);

		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 0, 0, 0, 31, 31));
		Assert.Equal(0, CountRooms(s, RoomKind.FungusFarm));

		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 1, 7, 5, 4, 4));
		Assert.Equal(8, CountRooms(s, RoomKind.FungusFarm));
		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 2, 0, 0, 31, 31));
		Assert.Equal(10, CountRooms(s, RoomKind.FungusFarm));
		for (var x = 4; x <= 7; x++)
			Assert.Equal(RoomKind.FungusFarm, s.RoomAt(At(s, x, 4, 1)));
		// A farm stores nothing: it is not a Pile.
		Assert.False(s.IsPile(At(s, 4, 4, 1)));
	}

	[Fact]
	public void Painting_clips_to_the_map_and_skips_Solid_Stairs_and_Open_air_tiles()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		Hollow(sim, 1, 29, 29, 31, 31);
		s.TileKinds[At(s, 30, 30)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 31, 30)] = (byte)TileKind.OpenAir;

		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 1, 28, 28, 40, 40));
		Assert.Equal(7, CountRooms(s, RoomKind.FungusFarm));
		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 1, 40, 40, 50, 50));
		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 1, -5, -5, -1, -1));
		Assert.Equal(7, CountRooms(s, RoomKind.FungusFarm));
		Assert.Equal(RoomKind.None, s.RoomAt(At(s, 28, 28)));
	}

	[Fact]
	public void A_tile_holds_one_Room_so_a_farm_and_a_different_Room_skip_each_other()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		Hollow(sim, 1, 4, 5, 9, 5);
		sim.Apply(new PaintPile(1, 5, 5, 5, 5));
		PaintPit(sim, 8, 5);
		sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 1, 9, 5, 9, 5));

		PaintFarm(sim, 4, 5, 9, 5);
		Assert.Equal(RoomKind.Pile, s.RoomAt(At(s, 5, 5)));
		Assert.Equal(RoomKind.GrubPit, s.RoomAt(At(s, 8, 5)));
		Assert.Equal(RoomKind.SleepingHeap, s.RoomAt(At(s, 9, 5)));
		Assert.Equal(3, CountRooms(s, RoomKind.FungusFarm));

		// The other way round: the others skip the farm tiles.
		sim.Apply(new PaintPile(1, 4, 5, 7, 5));
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 1, 4, 5, 7, 5));
		sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 1, 4, 5, 7, 5));
		Assert.Equal(3, CountRooms(s, RoomKind.FungusFarm));
		Assert.Equal(1, CountRooms(s, RoomKind.Pile));
	}

	[Fact]
	public void Painting_a_farm_over_stock_on_a_tile_leaves_it_loose()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		Hollow(sim, 1, 5, 5, 5, 5);
		s.AddItems(At(s, 5, 5), Material.Stone, 4);

		PaintFarm(sim, 5, 5);

		Assert.Equal(4, s.LooseCount(At(s, 5, 5), Material.Stone));
		Assert.Equal(0, s.StoredCount(At(s, 5, 5), Material.Stone));
	}

	[Fact]
	public void A_typed_removal_takes_only_farms_and_stairs_clear_the_farm_and_its_growing_Fungus()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		Hollow(sim, 1, 4, 4, 8, 4);
		PaintFarm(sim, 4, 4, 5, 4);
		PaintPit(sim, 6, 4);
		sim.Apply(new PaintPile(1, 7, 4, 7, 4));
		sim.Apply(new Designate(DesignationKind.DigStairs, 1, 8, 4, 8, 4));

		sim.Apply(new RemoveRoom(RoomKind.FungusFarm, 0, 0, 0, 31, 31));
		Assert.Equal(2, CountRooms(s, RoomKind.FungusFarm));
		sim.Apply(new RemoveRoom(RoomKind.GrubPit, 1, 0, 0, 31, 31));
		sim.Apply(new RemovePile(1, 0, 0, 31, 31));
		Assert.Equal(2, CountRooms(s, RoomKind.FungusFarm));
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(At(s, 8, 4)));
		sim.Apply(new CancelDesignation(1, 0, 0, 31, 31));
		Assert.Equal(2, CountRooms(s, RoomKind.FungusFarm));

		sim.Apply(new RemoveRoom(RoomKind.FungusFarm, 1, 0, 0, 31, 31));
		Assert.Equal(0, CountRooms(s, RoomKind.FungusFarm));
	}

	[Fact]
	public void Dig_stairs_clears_a_farm_and_the_Fungus_still_growing()
	{
		var sim = Lone();
		var s = sim.State;
		var farm = At(s, 20, 17);
		PaintFarm(sim, 20, 17);
		s.FarmUnspawned[farm] = 1;
		s.FarmTicks[farm] = 100;
		(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]) = (19, 17, 1);
		sim.Apply(new Designate(DesignationKind.DigStairs, 1, 20, 17, 20, 17));

		RunTicks(sim, 400);

		Assert.Equal(TileKind.Stairs, s.KindAt(farm));
		Assert.Equal(RoomKind.None, s.RoomAt(farm));
		Assert.Equal(0, s.FarmUnspawned[farm]);
		Assert.Equal(0, s.FarmTicks[farm]);
		Assert.Equal(0, Total(s, Material.Fungus));
	}

	// ---- The Tend and the growth timer

	[Fact]
	public void The_numbers_are_the_GDDs()
	{
		Assert.Equal(1, ProductionRules.FungusPerTend);
		Assert.Equal(300, ProductionRules.FungusGrowTicks);
		Assert.Equal(1, ProductionRules.FungusFarmMinZ);
		Assert.Equal(20, ProductionRules.TendTicks);
	}

	[Fact]
	public void A_Tend_takes_20_ticks_and_yields_exactly_1_Fungus_300_ticks_later_and_no_more()
	{
		var sim = Lone();
		var s = sim.State;
		// The goblin starts on the Surface above the farm and takes the stairs: tick 1 is its first step.
		var farm = At(s, 16, 17);
		PaintFarm(sim, 16, 17);

		long done = -1, grown = -1;
		for (var i = 0; i < 1500; i++)
		{
			RunTicks(sim, 1);
			if (done < 0 && s.FarmUnspawned[farm] == 1)
				done = s.Tick;
			if (done >= 0)
				Assert.False(IsTending(s, 0), $"Tick {s.Tick}: tended again while the Fungus grew or lay unhauled.");
			if (grown < 0 && s.ItemCount(farm, Material.Fungus) > 0)
				grown = s.Tick;
		}

		Assert.True(done > 0);
		Assert.Equal(done + 300, grown);
		Assert.Equal(1, s.ItemCount(farm, Material.Fungus));
		Assert.Equal(1, Total(s, Material.Fungus));
		Assert.Equal(0, s.FarmUnspawned[farm]);
		Assert.Equal(0, s.FarmTicks[farm]);
	}

	[Fact]
	public void The_Tend_itself_is_20_working_ticks_and_the_timer_runs_past_a_byte()
	{
		var sim = Lone();
		var s = sim.State;
		// Stand the goblin on the farm tile, so the Tend starts on the first tick.
		s.GoblinZ[0] = 1;
		s.GoblinX[0] = 16;
		s.GoblinY[0] = 17;
		var farm = At(s, 16, 17);
		PaintFarm(sim, 16, 17);

		RunTicks(sim, 19);
		Assert.Equal(GoblinTask.Tending, TaskOf(s, 0));
		Assert.Equal(0, s.FarmUnspawned[farm]);
		RunTicks(sim, 1);
		Assert.Equal(1, s.FarmUnspawned[farm]);
		Assert.False(IsTending(s, 0));

		RunTicks(sim, 256);
		Assert.Equal(256, s.FarmTicks[farm]);
		RunTicks(sim, 43);
		Assert.Equal(299, s.FarmTicks[farm]);
		Assert.Equal(0, s.ItemCount(farm, Material.Fungus));
		RunTicks(sim, 1);
		Assert.Equal(1, s.ItemCount(farm, Material.Fungus));
		Assert.Equal(0, s.FarmTicks[farm]);
	}

	[Fact]
	public void The_Fungus_lies_loose_and_is_hauled_to_a_Pile_after_which_the_farm_is_tended_again()
	{
		var sim = Lone();
		var s = sim.State;
		var farm = At(s, 16, 17);
		var pile = At(s, 10, 10, 0);
		PaintFarm(sim, 16, 17);
		PaintPile(sim, 10, 10);

		var tends = 0;
		var tending = false;
		for (var i = 0; i < 1500; i++)
		{
			RunTicks(sim, 1);
			var now = TaskOf(s, 0) == GoblinTask.Tending;
			if (now && !tending)
				tends++;
			tending = now;
			Assert.Equal(0, s.StoredCount(farm, Material.Fungus));
		}

		// Each Tend waited for the last Fungus to be hauled: every one but the latest has been stored.
		Assert.True(tends >= 3, $"Only {tends} Tends.");
		Assert.InRange(s.StoredCount(pile, Material.Fungus), tends - 1, tends);
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void No_Tend_is_offered_while_Fungus_lies_on_the_tile_even_with_nothing_growing_and_Grub_does_not_block_it()
	{
		var withFungus = Lone();
		var s = withFungus.State;
		var farm = At(s, 16, 17);
		s.AddItems(farm, Material.Fungus, 1);
		PaintFarm(withFungus, 16, 17);
		RunTicks(withFungus, 300);
		Assert.False(IsTending(s, 0));
		Assert.Equal(0, s.FarmUnspawned[farm]);

		// The crop is what counts: a Grub lying on a farm tile does not stop the Tend.
		var withGrub = Lone();
		var g = withGrub.State;
		g.AddItems(At(g, 16, 17), Material.Grub, 1);
		withGrub.RecheckReachability();
		PaintFarm(withGrub, 16, 17);
		RunTicks(withGrub, 100);
		Assert.True(g.FarmUnspawned[At(g, 16, 17)] == 1 || IsTending(g, 0));
	}

	[Fact]
	public void A_Starving_goblin_tends_at_half_speed()
	{
		var sim = Lone();
		var s = sim.State;
		s.GoblinGrub[0] = 0;
		s.GoblinZ[0] = 1;
		s.GoblinX[0] = 16;
		s.GoblinY[0] = 17;
		var farm = At(s, 16, 17);
		PaintFarm(sim, 16, 17);

		long done = -1;
		for (var i = 0; i < 100 && done < 0; i++)
		{
			RunTicks(sim, 1);
			if (s.FarmUnspawned[farm] == 1)
				done = s.Tick;
		}

		Assert.Equal(40, done);
	}

	[Fact]
	public void A_Craving_interrupting_a_Tend_loses_its_progress_and_the_Job_is_offered_again()
	{
		var sim = Lone();
		var s = sim.State;
		var farm = At(s, 16, 17);
		s.GoblinZ[0] = 1;
		s.GoblinX[0] = 16;
		s.GoblinY[0] = 17;
		s.GoblinGrub[0] = 30;
		// The Grub meter drops to 29 on tick 25, after nine working ticks of the Tend.
		s.Tick = 15;
		s.AddItems(At(s, 20, 17), Material.Grub, 1);
		sim.RecheckReachability();
		PaintFarm(sim, 16, 17);

		RunTicks(sim, 10);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Equal(0, s.FarmUnspawned[farm]);
		Assert.True(FarmSystem.IsTendOffered(s, farm));

		while (TaskOf(s, 0) != GoblinTask.Tending)
			RunTicks(sim, 1);
		var tendingTicks = 0;
		while (s.FarmUnspawned[farm] == 0)
		{
			tendingTicks++;
			RunTicks(sim, 1);
		}
		Assert.Equal(ProductionRules.TendTicks, tendingTicks);
	}

	[Fact]
	public void Another_goblin_takes_over_a_farm_tile_a_Craving_freed()
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		sim.State.GoblinCount = 2;
		var s = sim.State;
		for (var g = 0; g < 2; g++)
		{
			s.GoblinGrub[g] = s.GoblinGrog[g] = 100;
			s.GoblinZ[g] = 1;
			s.GoblinX[g] = 16 + g;
			s.GoblinY[g] = 17;
		}
		LinkUnderground(sim);
		PaintFarm(sim, 16, 17);
		RunTicks(sim, 5);
		Assert.Equal(GoblinTask.Tending, TaskOf(s, 0));

		s.GoblinGrub[0] = 20;
		s.AddItems(At(s, 18, 17), Material.Grub, 1);
		sim.RecheckReachability();
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.True(IsTending(s, 1));
	}

	[Fact]
	public void Removing_a_tile_mid_Tend_frees_the_tender_and_yields_nothing()
	{
		var sim = Lone();
		var s = sim.State;
		var farm = At(s, 16, 17);
		s.GoblinZ[0] = 1;
		s.GoblinX[0] = 16;
		s.GoblinY[0] = 17;
		PaintFarm(sim, 16, 17);
		RunTicks(sim, 10);
		Assert.Equal(GoblinTask.Tending, TaskOf(s, 0));

		sim.Apply(new RemoveRoom(RoomKind.FungusFarm, 1, 16, 17, 16, 17));

		Assert.False(IsTending(s, 0));
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.Null(GoblinTasks.FindViolation(s));
		RunTicks(sim, 700);
		Assert.Equal(0, Total(s, Material.Fungus));
		Assert.Equal(0, s.FarmUnspawned[farm]);
	}

	[Fact]
	public void Removing_a_tile_mid_growth_discards_the_Fungus_and_repainting_starts_clean()
	{
		var sim = Lone();
		var s = sim.State;
		var farm = At(s, 16, 17);
		s.GoblinZ[0] = 1;
		s.GoblinX[0] = 16;
		s.GoblinY[0] = 17;
		PaintFarm(sim, 16, 17);
		RunTicks(sim, 20 + 270);
		Assert.Equal(1, s.FarmUnspawned[farm]);
		Assert.Equal(270, s.FarmTicks[farm]);

		sim.Apply(new RemoveRoom(RoomKind.FungusFarm, 1, 16, 17, 16, 17));
		Assert.Equal(0, s.FarmUnspawned[farm]);
		Assert.Equal(0, s.FarmTicks[farm]);

		RunTicks(sim, 600);
		Assert.Equal(0, Total(s, Material.Fungus));
		PaintFarm(sim, 16, 17);
		Assert.Equal(0, s.FarmUnspawned[farm]);
		Assert.Equal(0, s.FarmTicks[farm]);
	}

	[Fact]
	public void Removing_a_tile_after_the_Fungus_grew_leaves_it_loose()
	{
		var sim = Lone();
		var s = sim.State;
		var farm = At(s, 16, 17);
		s.GoblinZ[0] = 1;
		s.GoblinX[0] = 16;
		s.GoblinY[0] = 17;
		PaintFarm(sim, 16, 17);
		RunTicks(sim, 20 + 300);
		Assert.Equal(1, s.ItemCount(farm, Material.Fungus));

		sim.Apply(new RemoveRoom(RoomKind.FungusFarm, 1, 16, 17, 16, 17));
		RunTicks(sim, 300);

		Assert.Equal(1, s.LooseCount(farm, Material.Fungus));
	}

	// ---- Fungus is not eaten

	[Fact]
	public void A_goblin_craving_Grub_does_not_eat_Fungus()
	{
		var sim = Lone();
		var s = sim.State;
		s.GoblinGrub[0] = 10;
		s.GoblinZ[0] = 1;
		s.GoblinX[0] = 15;
		s.GoblinY[0] = 17;
		var fungus = At(s, 16, 17);
		s.AddItems(fungus, Material.Fungus, 3);
		sim.RecheckReachability();

		RunTicks(sim, 400);

		Assert.Equal(3, s.ItemCount(fungus, Material.Fungus));
		Assert.True(s.GoblinGrub[0] < 10, $"The Grub meter climbed to {s.GoblinGrub[0]}.");
		Assert.NotEqual(GoblinTask.Eating, TaskOf(s, 0));
		Assert.NotEqual(GoblinTask.Seeking, TaskOf(s, 0));
	}

	[Fact]
	public void Fungus_in_a_Pile_is_not_eaten_either_and_Grub_still_is()
	{
		var sim = Lone();
		var s = sim.State;
		PaintPile(sim, 16, 16);
		var pile = At(s, 16, 16, 0);
		s.AddItems(pile, Material.Fungus, 4);
		s.GoblinGrub[0] = 10;
		sim.RecheckReachability();
		RunTicks(sim, 200);
		Assert.Equal(4, s.StoredCount(pile, Material.Fungus));
		Assert.True(s.GoblinGrub[0] < 10);

		s.AddItems(pile, Material.Grub, 1);
		sim.RecheckReachability();
		RunTicks(sim, 100);
		Assert.Equal(0, s.StoredCount(pile, Material.Grub));
		Assert.Equal(4, s.StoredCount(pile, Material.Fungus));
	}

	// ---- One job list

	[Fact]
	public void A_Grub_Pit_and_a_Fungus_Farm_both_produce_and_each_crop_is_hauled()
	{
		var sim = Lone();
		var s = sim.State;
		var farm = At(s, 14, 17);
		var pit = At(s, 18, 17);
		var pile = At(s, 10, 10, 0);
		PaintFarm(sim, 14, 17);
		PaintPit(sim, 18, 17);
		PaintPile(sim, 10, 10);

		var grubSeen = 0;
		var fungusSeen = 0;
		for (var i = 0; i < 1500; i++)
		{
			RunTicks(sim, 1);
			Assert.True(s.ItemCount(farm, Material.Grub) == 0 && s.ItemCount(pit, Material.Fungus) == 0);
			grubSeen = Math.Max(grubSeen, s.StoredCount(pile, Material.Grub));
			fungusSeen = Math.Max(fungusSeen, s.StoredCount(pile, Material.Fungus));
		}

		Assert.True(grubSeen >= 3, $"Only {grubSeen} Grub reached the Pile.");
		Assert.True(fungusSeen >= 2, $"Only {fungusSeen} Fungus reached the Pile.");
	}

	[Fact]
	public void A_Fungus_Farm_Tend_joins_the_one_job_list_nearest_first()
	{
		var farmNear = Lone();
		farmNear.State.GoblinZ[0] = 1;
		farmNear.State.GoblinX[0] = 16;
		farmNear.State.GoblinY[0] = 17;
		PaintFarm(farmNear, 14, 17);
		PaintPit(farmNear, 21, 17);
		RunTicks(farmNear, 1);
		Assert.Equal((GoblinTask.Approaching, At(farmNear.State, 14, 17)), (TaskOf(farmNear.State, 0), farmNear.State.GoblinJobTile[0]));

		var pitNear = Lone();
		pitNear.State.GoblinZ[0] = 1;
		pitNear.State.GoblinX[0] = 16;
		pitNear.State.GoblinY[0] = 17;
		PaintFarm(pitNear, 11, 17);
		PaintPit(pitNear, 18, 17);
		RunTicks(pitNear, 1);
		Assert.Equal((GoblinTask.Approaching, At(pitNear.State, 18, 17)), (TaskOf(pitNear.State, 0), pitNear.State.GoblinJobTile[0]));
	}

	[Fact]
	public void A_Grub_Pit_keeps_its_three_Grub_and_its_200_tick_timer()
	{
		var sim = Lone();
		var s = sim.State;
		s.GoblinZ[0] = 1;
		s.GoblinX[0] = 16;
		s.GoblinY[0] = 17;
		var pit = At(s, 16, 17);
		PaintPit(sim, 16, 17);

		RunTicks(sim, 20 + 250);

		Assert.Equal(1, s.ItemCount(pit, Material.Grub));
		Assert.Equal(2, s.FarmUnspawned[pit]);
		Assert.Equal(50, s.FarmTicks[pit]);
		Assert.Equal(0, s.ItemCount(pit, Material.Fungus));
	}

	// ---- From the Surface to z1 and back

	[Fact]
	public void Goblins_take_the_stairs_down_to_tend_a_farm_dug_on_z1_and_haul_its_Fungus_to_a_Pile_on_the_Surface()
	{
		var sim = NewWorldWithOneGrunt(seed: 2);
		var s = sim.State;
		s.GoblinGrub[0] = s.GoblinGrog[0] = s.GoblinSleep[0] = 100;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 15, 16, 15));
		RunUntil(sim, () => s.KindAt(At(s, 16, 15, 0)) == TileKind.Stairs, 200);
		sim.Apply(new Designate(DesignationKind.DigStairs, 1, 16, 15, 16, 15));
		RunUntil(sim, () => s.KindAt(At(s, 16, 15)) == TileKind.Stairs, 400);
		sim.Apply(new Designate(DesignationKind.Dig, 1, 16, 16, 17, 16));
		RunUntil(sim, () => s.KindAt(At(s, 16, 16)) == TileKind.Floor && s.KindAt(At(s, 17, 16)) == TileKind.Floor, 600);
		// The dug dirt is hauled up to a Pile on the Surface before anything else is asked of the goblin.
		PaintPile(sim, 12, 12);
		RunUntil(sim, () => s.LooseCount(At(s, 16, 16), Material.Dirt) + s.LooseCount(At(s, 17, 16), Material.Dirt) == 0
			&& GoblinTask.Idle == TaskOf(s, 0), 1500);

		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 0, 0, 0, 31, 31));
		Assert.Equal(0, CountRooms(s, RoomKind.FungusFarm));
		PaintFarm(sim, 16, 16, 17, 16);
		Assert.Equal(2, CountRooms(s, RoomKind.FungusFarm));

		var sawZ1 = false;
		var pile = At(s, 12, 12, 0);
		RunUntil(sim, () =>
		{
			sawZ1 |= s.GoblinZ[0] == 1;
			return s.StoredCount(pile, Material.Fungus) >= 1;
		}, 3000);

		Assert.True(sawZ1, "The goblin never went underground.");
		Assert.Equal(1, s.StoredCount(pile, Material.Fungus));
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	static void RunUntil(Simulation sim, Func<bool> done, int limit)
	{
		for (var i = 0; i < limit; i++)
		{
			if (done())
				return;
			RunTicks(sim, 1);
		}
		Assert.True(done(), $"Gave up after {limit} ticks.");
	}

	// ---- Rules every tick

	[Fact]
	public void Tasks_hold_their_rules_every_tick_across_farms_pits_piles_and_removals()
	{
		var sim = Simulation.NewWorld(seed: 5);
		var s = sim.State;
		s.TileKinds[At(s, 16, 15, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 16, 15, 1)] = (byte)TileKind.Stairs;
		Hollow(sim, 1, 10, 16, 22, 20);
		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 1, 12, 17, 14, 18));
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 1, 18, 17, 20, 17));
		sim.Apply(new PaintPile(0, 15, 18, 17, 19));
		var log = new (int Tick, Command Command)[]
		{
			(300, new Designate(DesignationKind.DigStairs, 1, 13, 17, 13, 17)),
			(450, new RemoveRoom(RoomKind.FungusFarm, 1, 12, 18, 14, 18)),
			(600, new PaintRoom(RoomKind.FungusFarm, 1, 10, 16, 22, 20)),
			(900, new RemoveRoom(RoomKind.FungusFarm, 1, 0, 0, 31, 31)),
			(1000, new RemovePile(0, 0, 0, 31, 31)),
		};

		for (var tick = 0; tick < 1500; tick++)
		{
			foreach (var (at, command) in log)
				if (at == tick)
					sim.Apply(command);
			RunTicks(sim, 1);
		}

		Assert.Equal(0, CountRooms(s, RoomKind.FungusFarm));
	}

	// ---- FindViolation

	static SimState Tender(out int farm)
	{
		var sim = NewWorldWithFiveGrunts(seed: 1);
		sim.State.GoblinCount = 2;
		var s = sim.State;
		LinkUnderground(sim);
		farm = At(s, 16, 17);
		PaintFarm(sim, 16, 17, 17, 17);
		for (var g = 0; g < 2; g++)
		{
			s.GoblinGrub[g] = s.GoblinGrog[g] = 100;
			s.GoblinZ[g] = 1;
			s.GoblinX[g] = 16 - g;
			s.GoblinY[g] = 17;
		}
		return s;
	}

	[Fact]
	public void FindViolation_accepts_tending_a_farm_and_rejects_tending_one_that_is_growing()
	{
		var s = Tender(out var farm);
		s.GoblinTask[0] = (byte)GoblinTask.Tending;
		s.GoblinJobTile[0] = farm;
		s.GoblinX[0] = 16;
		s.GoblinWorkTicks[0] = (byte)(ProductionRules.TendTicks - 1);
		Assert.Null(GoblinTasks.FindViolation(s));

		s.FarmUnspawned[farm] = 1;
		Assert.NotNull(GoblinTasks.FindViolation(s));
	}

	[Fact]
	public void FindViolation_rejects_tending_a_tile_that_holds_no_farm()
	{
		var s = Tender(out var farm);
		s.GoblinTask[0] = (byte)GoblinTask.Approaching;
		s.GoblinJobTile[0] = farm - 1;
		Assert.NotNull(GoblinTasks.FindViolation(s));
		s.GoblinJobTile[0] = farm + 1;
		Assert.Null(GoblinTasks.FindViolation(s));
	}

	// ---- Saves and replays

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

	static Simulation Dug(int seed)
	{
		var sim = Simulation.NewWorld(seed);
		var s = sim.State;
		s.TileKinds[At(s, 16, 15, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 16, 15, 1)] = (byte)TileKind.Stairs;
		Hollow(sim, 1, 12, 16, 20, 19);
		return sim;
	}

	[Fact]
	public void A_save_with_a_tile_mid_growth_past_a_byte_round_trips_and_replays_identically()
	{
		var log = new (int Tick, Command Command)[]
		{
			(2, new PaintRoom(RoomKind.FungusFarm, 1, 14, 17, 16, 18)),
			(3, new PaintPile(0, 18, 18, 19, 19)),
			(4, new PaintRoom(RoomKind.GrubPit, 1, 18, 17, 19, 17)),
			(700, new RemoveRoom(RoomKind.FungusFarm, 1, 15, 17, 15, 18)),
			(1000, new Designate(DesignationKind.DigStairs, 1, 16, 17, 16, 17)),
			(1200, new PaintRoom(RoomKind.FungusFarm, 1, 15, 17, 15, 18)),
		};
		var original = Dug(seed: 12);
		RunLog(original, log, from: 0, to: 330);

		// Some farm tile is part-way round its timer, past the 255 a byte holds.
		var s = original.State;
		Assert.Contains(Enumerable.Range(0, s.FarmUnspawned.Length), t => s.RoomAt(t) == RoomKind.FungusFarm && s.FarmUnspawned[t] > 0 && s.FarmTicks[t] > 255);
		var json = SimJson.Serialize(original);
		var restored = SimJson.Deserialize(json);
		Assert.Equal(json, SimJson.Serialize(restored));

		RunLog(original, log, from: 330, to: 1800);
		RunLog(restored, log, from: 330, to: 1800);
		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));

		var fresh = Dug(seed: 12);
		RunLog(fresh, log, from: 0, to: 1800);
		Assert.Equal(SimJson.Serialize(fresh), SimJson.Serialize(original));
	}

	static void AssertRejected(Action<SimState> corrupt)
	{
		var sim = Dug(seed: 1);
		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 1, 14, 17, 14, 17));
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 1, 15, 17, 15, 17));
		corrupt(sim.State);
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Fact]
	public void A_save_with_the_valid_farm_state_is_accepted()
	{
		var sim = Dug(seed: 1);
		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 1, 14, 17, 14, 17));
		var s = sim.State;
		s.FarmUnspawned[At(s, 14, 17)] = 1;
		s.FarmTicks[At(s, 14, 17)] = ProductionRules.FungusGrowTicks - 1;
		SimJson.Deserialize(SimJson.Serialize(sim));
	}

	[Fact]
	public void A_save_with_a_Fungus_Farm_on_the_Surface_is_rejected()
	{
		AssertRejected(s =>
		{
			var surface = At(s, 5, 5, 0);
			s.Rooms[surface] = (byte)RoomKind.FungusFarm;
		});
		// A Grub Pit on the Surface remains fine.
		var sim = Simulation.NewWorld(seed: 1);
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 5, 5, 5, 5));
		SimJson.Deserialize(SimJson.Serialize(sim));
	}

	[Theory]
	[InlineData(TileKind.Solid)]
	[InlineData(TileKind.Stairs)]
	[InlineData(TileKind.OpenAir)]
	public void A_save_with_a_Fungus_Farm_on_a_non_Floor_tile_is_rejected(TileKind kind) =>
		AssertRejected(s => s.TileKinds[At(s, 14, 17)] = (byte)kind);

	[Fact]
	public void A_save_with_two_Rooms_on_one_farm_tile_is_rejected() =>
		AssertRejected(s => s.Piles[At(s, 14, 17)] = 1);

	[Fact]
	public void A_save_with_Fungus_to_grow_beyond_a_Tend_or_off_a_farm_is_rejected()
	{
		AssertRejected(s => s.FarmUnspawned[At(s, 14, 17)] = (byte)(ProductionRules.FungusPerTend + 1));
		AssertRejected(s => s.FarmUnspawned[At(s, 13, 17)] = 1);
	}

	[Fact]
	public void A_save_with_a_growth_timer_past_its_period_or_running_with_nothing_growing_is_rejected()
	{
		AssertRejected(s =>
		{
			s.FarmUnspawned[At(s, 14, 17)] = 1;
			s.FarmTicks[At(s, 14, 17)] = (ushort)ProductionRules.FungusGrowTicks;
		});
		AssertRejected(s => s.FarmTicks[At(s, 14, 17)] = 4);
		// Each Room has its own period: 250 is mid-growth for a farm and past a pit's 200.
		AssertRejected(s =>
		{
			s.FarmUnspawned[At(s, 15, 17)] = 3;
			s.FarmTicks[At(s, 15, 17)] = 250;
		});
	}

	[Fact]
	public void A_save_with_a_goblin_tending_a_farm_that_is_growing_is_rejected()
	{
		AssertRejected(s =>
		{
			s.FarmUnspawned[At(s, 14, 17)] = 1;
			s.GoblinTask[0] = (byte)GoblinTask.Approaching;
			s.GoblinJobTile[0] = At(s, 14, 17);
		});
	}

	[Theory]
	[InlineData("null")]
	[InlineData("[]")]
	[InlineData("[0,0,0]")]
	[InlineData("\"AAAA\"")]
	public void A_save_with_a_missing_or_wrong_length_or_old_style_farm_array_is_rejected(string value)
	{
		foreach (var name in new[] { "FarmUnspawned", "FarmTicks" })
		{
			var node = System.Text.Json.Nodes.JsonNode.Parse(SimJson.Serialize(Simulation.NewWorld(seed: 1)))!;
			node[name] = System.Text.Json.Nodes.JsonNode.Parse(value);
			Assert.Throws<JsonException>(() => SimJson.Deserialize(node.ToJsonString()));
		}
	}
}
