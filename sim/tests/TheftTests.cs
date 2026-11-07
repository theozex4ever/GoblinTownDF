using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>P4's theft: a goblin at 70 Chaos may take one Grub or Grog off a Pile, carry it a few steps away and consume it privately.</summary>
public partial class TheftTests
{
	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	static int At(SimState s, int x, int y, int z = 0) => s.TileIndex(x, y, z);

	// A world of the given goblins standing at the given tiles of z0: every Need full, no Chaos, no cooldown, nothing anywhere to eat, drink or haul.
	// Goblins after the first are bystanders that wait still: each holds a unit of Dirt, and the first Pile painted is filled with Dirt, so they
	// have no storage to take it to, never wander, draw nothing from the Rng and can put the unit down on the spot.
	static Simulation Yard(params (int X, int Y)[] at)
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		s.GoblinCount = at.Length;
		for (var g = 0; g < at.Length; g++)
		{
			s.GoblinX[g] = at[g].X;
			s.GoblinY[g] = at[g].Y;
			s.GoblinZ[g] = 0;
			s.GoblinGrub[g] = 100;
			s.GoblinGrog[g] = 100;
			s.GoblinSleep[g] = 100;
			s.GoblinChaos[g] = 0;
			s.GoblinActCooldown[g] = 0;
			if (g > 0)
			{
				s.GoblinTask[g] = (byte)GoblinTask.Carrying;
				s.GoblinCarry[g] = (byte)Material.Dirt;
			}
		}
		sim.RecheckReachability();
		return sim;
	}

	// A Pile tile holding the given units, and, when asked, 30 Dirt so that no goblin waiting with Dirt has anywhere to take it.
	static void Stock(Simulation sim, int x, int y, Material material, int units = 1, bool fullOfDirt = false, int z = 0)
	{
		var s = sim.State;
		var tile = At(s, x, y, z);
		if (!s.IsPile(tile))
			sim.Apply(new PaintPile(z, x, y, x, y));
		s.AddItems(tile, material, units);
		if (fullOfDirt)
			s.AddItems(tile, Material.Dirt, SimState.PileCapacity - s.ItemCount(tile, Material.Dirt));
		sim.RecheckReachability();
	}

	// A generator whose first draws of NextInt(options) are the given ones.
	static SimRandom SeedDrawing(int options, params int[] draws)
	{
		for (var seed = 0; ; seed++)
		{
			var probe = new SimRandom(seed);
			if (draws.All(d => probe.NextInt(options) == d))
				return new SimRandom(seed);
		}
	}

	// Goblin 0 is ready to act out, and the Rng will draw the given choice among the given number of options.
	static void Provoke(Simulation sim, int options, int draw)
	{
		sim.State.GoblinChaos[0] = ChaosRules.ActingOutThreshold;
		sim.State.Rng = SeedDrawing(options, draw);
	}

	// Rows of z0 are walls but for the given runs of Floor, as (y, x0, x1): a maze to make walking distance differ from the straight line.
	static void Corridors(Simulation sim, params (int Y, int X0, int X1)[] runs)
	{
		var s = sim.State;
		for (var y = 0; y < s.Height; y++)
		for (var x = 0; x < s.Width; x++)
			s.TileKinds[At(s, x, y)] = (byte)(runs.Any(r => r.Y == y && x >= r.X0 && x <= r.X1) ? TileKind.Floor : TileKind.Solid);
		sim.RecheckReachability();
	}

	// ---- Choosing to steal

	[Fact]
	public void A_goblin_at_70_Chaos_with_Grub_on_a_Pile_sets_out_to_steal_when_the_draw_picks_theft()
	{
		var sim = Yard((10, 10));
		var s = sim.State;
		Stock(sim, 10, 13, Material.Grub);
		Provoke(sim, options: 2, draw: 1);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sneaking, TaskOf(s, 0));
		Assert.Equal(At(s, 10, 13), s.GoblinJobTile[0]);
		Assert.Equal((byte)Material.Grub, s.GoblinTheftSupply[0]);
		Assert.Equal(At(s, 10, 9), s.GoblinTheftDestination[0]);
		Assert.Equal(Material.None, (Material)s.GoblinCarry[0]);
		// Nothing has moved yet: the supply is still communal stock.
		Assert.Equal(1, s.ItemCount(At(s, 10, 13), Material.Grub));
	}

	[Fact]
	public void The_same_goblin_shirks_when_the_draw_picks_that()
	{
		var sim = Yard((10, 10));
		Stock(sim, 10, 13, Material.Grub);
		Provoke(sim, options: 2, draw: 0);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(sim.State, 0));
	}

	[Theory]
	[InlineData(0, GoblinTask.Shirking)]
	[InlineData(1, GoblinTask.Pursuing)]
	[InlineData(2, GoblinTask.Sneaking)]
	public void With_shirking_a_fight_and_theft_all_eligible_the_draw_is_one_of_three(int draw, GoblinTask expected)
	{
		var sim = Yard((10, 10), (14, 10));
		Stock(sim, 10, 13, Material.Grub, fullOfDirt: true);
		Provoke(sim, options: 3, draw: draw);

		RunTicks(sim, 1);

		Assert.Equal(expected, TaskOf(sim.State, 0));
	}

	[Fact]
	public void Shirking_a_fight_and_theft_are_equally_likely_when_all_are_eligible()
	{
		var counts = new Dictionary<GoblinTask, int>();
		for (var seed = 0; seed < 600; seed++)
		{
			var sim = Yard((10, 10), (14, 10));
			Stock(sim, 10, 13, Material.Grub, fullOfDirt: true);
			sim.State.GoblinChaos[0] = 70;
			sim.State.Rng = new SimRandom(seed);
			sim.Advance(0.1);
			var task = TaskOf(sim.State, 0);
			counts[task] = counts.GetValueOrDefault(task) + 1;
		}

		Assert.Equal(3, counts.Count);
		Assert.All(counts.Values, n => Assert.InRange(n, 160, 240));
	}

	[Fact]
	public void Theft_and_shirking_are_equally_likely_when_there_is_no_one_to_fight()
	{
		var thefts = 0;
		for (var seed = 0; seed < 400; seed++)
		{
			var sim = Yard((10, 10));
			Stock(sim, 10, 13, Material.Grub);
			sim.State.GoblinChaos[0] = 70;
			sim.State.Rng = new SimRandom(seed);
			sim.Advance(0.1);
			if (TaskOf(sim.State, 0) == GoblinTask.Sneaking)
				thefts++;
			else
				Assert.Equal(GoblinTask.Shirking, TaskOf(sim.State, 0));
		}

		Assert.InRange(thefts, 160, 240);
	}

	[Fact]
	public void A_fight_is_still_one_of_two_when_there_is_nothing_to_steal()
	{
		var sim = Yard((10, 10), (14, 10));
		Provoke(sim, options: 2, draw: 1);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Pursuing, TaskOf(sim.State, 0));
	}

	[Fact]
	public void Loose_supplies_are_no_source_so_with_nothing_on_a_Pile_the_goblin_shirks_and_draws_nothing()
	{
		var sim = Yard((10, 10));
		var s = sim.State;
		s.AddItems(At(s, 10, 13), Material.Grub, 3);
		s.AddItems(At(s, 11, 13), Material.Grog, 3);
		Stock(sim, 14, 14, Material.Dirt, 5);
		sim.RecheckReachability();
		s.GoblinChaos[0] = 70;
		var before = s.Rng.State;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal(before, s.Rng.State);
	}

	// ---- Choosing the source

	// The source (the claimed Pile tile) a goblin sets out for after one tick, or -1 if it did not set out to steal.
	static int SourceOf(Simulation sim)
	{
		Provoke(sim, options: 2, draw: 1);
		sim.Advance(0.1);
		return TaskOf(sim.State, 0) == GoblinTask.Sneaking ? sim.State.GoblinJobTile[0] : -1;
	}

	[Fact]
	public void The_source_is_the_nearest_Pile_holding_Grub_or_Grog_by_walking_distance()
	{
		var sim = Yard((10, 10));
		var s = sim.State;
		Stock(sim, 10, 12, Material.Dirt, 5);
		Stock(sim, 10, 15, Material.Grub);
		Stock(sim, 10, 14, Material.Grog);

		Assert.Equal(At(s, 10, 14), SourceOf(sim));
	}

	[Fact]
	public void Equally_near_Piles_go_to_the_lowest_tile_index()
	{
		var sim = Yard((10, 10));
		var s = sim.State;
		Stock(sim, 10, 14, Material.Grub);
		Stock(sim, 14, 10, Material.Grub);

		Assert.Equal(At(s, 14, 10), SourceOf(sim));
	}

	[Fact]
	public void A_Pile_holding_both_supplies_is_taken_for_Grog_before_Grub()
	{
		var sim = Yard((10, 10));
		var s = sim.State;
		Stock(sim, 10, 13, Material.Grub, 5);
		Stock(sim, 10, 13, Material.Grog, 1);

		Assert.Equal(At(s, 10, 13), SourceOf(sim));
		Assert.Equal((byte)Material.Grog, s.GoblinTheftSupply[0]);
	}

	[Fact]
	public void A_Pile_that_cannot_be_walked_to_is_no_source()
	{
		// Two corridors that never meet: the Pile in the other one is out of reach.
		var cut = Yard((10, 10));
		Corridors(cut, (10, 2, 12), (20, 2, 12));
		Stock(cut, 12, 20, Material.Grub);
		Stock(cut, 12, 10, Material.Grog);
		Assert.Equal(At(cut.State, 12, 10), SourceOf(cut));

		var alone = Yard((10, 10));
		Corridors(alone, (10, 2, 12), (20, 2, 12));
		Stock(alone, 12, 20, Material.Grub);
		Assert.Equal(-1, SourceOf(alone));
	}

	// ---- Choosing the destination

	// The destination a goblin sets out for, or -1.
	static int DestinationOf(Simulation sim)
	{
		Provoke(sim, options: 2, draw: 1);
		sim.Advance(0.1);
		return GoblinTasks.IsTheftTask(TaskOf(sim.State, 0)) ? sim.State.GoblinTheftDestination[0] : -1;
	}

	[Fact]
	public void The_destination_is_the_farthest_tile_within_four_steps_of_the_source_and_ties_go_to_the_lowest_tile_index()
	{
		var sim = Yard((10, 10));
		Corridors(sim, (10, 2, 20));
		Stock(sim, 10, 10, Material.Grub);

		// Four steps either way along the corridor: (6, 10) and (14, 10).
		Assert.Equal(At(sim.State, 6, 10), DestinationOf(sim));
	}

	[Fact]
	public void A_Pile_tile_is_never_the_destination()
	{
		var sim = Yard((10, 10));
		Corridors(sim, (10, 2, 20));
		Stock(sim, 10, 10, Material.Grub);
		Stock(sim, 6, 10, Material.Dirt);
		Stock(sim, 7, 10, Material.Dirt);

		Assert.Equal(At(sim.State, 14, 10), DestinationOf(sim));
	}

	[Fact]
	public void Four_steps_are_walked_not_flown_so_a_tile_behind_a_wall_is_too_far()
	{
		var sim = Yard((10, 10));
		// A second corridor above the first, joined to it only far away: its tiles are near the Pile as the crow flies and a long way off on foot.
		Corridors(sim, (10, 2, 20), (8, 8, 20));
		Stock(sim, 10, 10, Material.Grub);

		Assert.Equal(At(sim.State, 6, 10), DestinationOf(sim));
	}

	[Fact]
	public void A_nearer_destination_is_taken_when_the_corridor_is_shorter_than_four_steps()
	{
		var sim = Yard((10, 10));
		Corridors(sim, (10, 2, 12));
		Stock(sim, 10, 10, Material.Grub);

		// The corridor ends at x = 12, two steps east of the Pile, but runs four west.
		Assert.Equal(At(sim.State, 6, 10), DestinationOf(sim));

		var stub = Yard((10, 10));
		Corridors(stub, (10, 8, 12));
		Stock(stub, 10, 10, Material.Grub);
		Assert.Equal(At(stub.State, 8, 10), DestinationOf(stub));
	}

	[Fact]
	public void With_no_tile_outside_a_Pile_within_four_steps_theft_is_unavailable_and_the_goblin_shirks_drawing_nothing()
	{
		var sim = Yard((10, 10));
		var s = sim.State;
		Corridors(sim, (10, 2, 20));
		Stock(sim, 10, 10, Material.Grub);
		for (var x = 6; x <= 14; x++)
			if (x != 10)
				Stock(sim, x, 10, Material.Dirt);
		s.GoblinChaos[0] = 70;
		var before = s.Rng.State;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
		Assert.Equal(before, s.Rng.State);
	}

	[Fact]
	public void The_destination_is_on_the_level_of_the_source()
	{
		var sim = Yard((10, 10));
		var s = sim.State;
		// A small upper floor reached by a stair at (10, 12), with the Pile on it.
		for (var y = 9; y <= 17; y++)
		for (var x = 6; x <= 14; x++)
			s.TileKinds[At(s, x, y, 1)] = (byte)TileKind.Floor;
		s.TileKinds[At(s, 10, 12, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 10, 12, 1)] = (byte)TileKind.Stairs;
		sim.RecheckReachability();
		Stock(sim, 10, 15, Material.Grub, z: 1);

		var destination = DestinationOf(sim);

		Assert.Equal(At(s, 10, 11, 1), destination);
	}

	// ---- The theft, start to finish

	// Goblin 0 at (10, 10) about to steal the one unit of the given supply on the Pile at (10, 13); its destination will be (10, 9). Tick 1 starts the theft.
	static Simulation Heist(Material supply = Material.Grub, int units = 1)
	{
		var sim = Yard((10, 10));
		Stock(sim, 10, 13, supply, units);
		Provoke(sim, options: 2, draw: 1);
		return sim;
	}

	// Runs ticks until the condition holds, at most the limit, and returns how many it took. Fails the test if it never does.
	static int RunUntil(Simulation sim, Func<SimState, bool> condition, int limit = 400)
	{
		for (var i = 0; i < limit; i++)
		{
			if (condition(sim.State))
				return i;
			RunTicks(sim, 1);
		}
		Assert.True(condition(sim.State), "The condition was not met within " + limit + " ticks.");
		return limit;
	}

	// Every unit of a supply there is: on any tile, Pile or loose, or in a goblin's hands.
	static int Total(SimState s, Material material)
	{
		var total = 0;
		for (var tile = 0; tile < s.Piles.Length; tile++)
			total += s.ItemCount(tile, material);
		for (var g = 0; g < s.GoblinCount; g++)
			if ((Material)s.GoblinCarry[g] == material)
				total++;
		return total;
	}

	[Fact]
	public void A_thief_walks_to_the_Pile_at_the_ordinary_pace_then_picks_the_unit_up_in_5_ticks_and_only_then_is_it_gone_from_the_Pile()
	{
		var sim = Heist(units: 3);
		var s = sim.State;
		var pile = At(s, 10, 13);

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sneaking, TaskOf(s, 0));
		RunTicks(sim, 2);
		Assert.Equal((10, 11), (s.GoblinX[0], s.GoblinY[0]));
		RunTicks(sim, 3);
		Assert.Equal((10, 12), (s.GoblinX[0], s.GoblinY[0]));
		RunTicks(sim, 3);
		Assert.Equal((10, 13), (s.GoblinX[0], s.GoblinY[0]));
		Assert.Equal(GoblinTask.Pilfering, TaskOf(s, 0));
		Assert.Equal(3, s.ItemCount(pile, Material.Grub));

		RunTicks(sim, 4);
		Assert.Equal(GoblinTask.Pilfering, TaskOf(s, 0));
		Assert.Equal(3, s.ItemCount(pile, Material.Grub));
		Assert.Equal(Material.None, (Material)s.GoblinCarry[0]);

		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.MakingOff, TaskOf(s, 0));
		Assert.Equal(2, s.ItemCount(pile, Material.Grub));
		Assert.Equal(Material.Grub, (Material)s.GoblinCarry[0]);
		Assert.Equal(-1, s.GoblinJobTile[0]);
	}

	[Fact]
	public void The_pickup_raises_one_transient_event_naming_the_goblin_and_the_supply()
	{
		var sim = Heist(Material.Grog);
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Pilfering);
		Assert.Empty(sim.DrainEvents().OfType<TheftPickedUp>());

		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.MakingOff);

		Assert.Equal(new TheftPickedUp(0, Material.Grog), Assert.Single(sim.DrainEvents().OfType<TheftPickedUp>()));
		RunTicks(sim, 100);
		Assert.Empty(sim.DrainEvents().OfType<TheftPickedUp>());
	}

	[Fact]
	public void A_thief_carries_the_unit_visibly_to_its_destination_at_the_ordinary_pace_and_consumes_it_in_10_ticks()
	{
		var sim = Heist();
		var s = sim.State;
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.MakingOff);

		// Four steps north, one every 3 ticks, the unit in hand the whole way.
		for (var step = 1; step <= 4; step++)
		{
			RunTicks(sim, 2);
			Assert.Equal((10, 13 - step + 1), (s.GoblinX[0], s.GoblinY[0]));
			Assert.Equal(Material.Grub, (Material)s.GoblinCarry[0]);
			RunTicks(sim, 1);
			Assert.Equal((10, 13 - step), (s.GoblinX[0], s.GoblinY[0]));
		}
		Assert.Equal(GoblinTask.Scoffing, TaskOf(s, 0));
		Assert.Equal(Material.Grub, (Material)s.GoblinCarry[0]);
		Assert.Equal(At(s, 10, 9), At(s, s.GoblinX[0], s.GoblinY[0]));

		RunTicks(sim, 9);
		Assert.Equal(GoblinTask.Scoffing, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(Material.None, (Material)s.GoblinCarry[0]);
	}

	[Fact]
	public void The_unit_is_conserved_until_the_instant_it_is_consumed_and_never_appears_twice()
	{
		var sim = Heist(units: 2);
		var s = sim.State;
		Assert.Equal(2, Total(s, Material.Grub));

		RunTicks(sim, 1);
		for (var i = 0; i < 100 && TaskOf(s, 0) != GoblinTask.Idle; i++)
		{
			Assert.Equal(2, Total(s, Material.Grub));
			RunTicks(sim, 1);
		}

		// The one consumed is gone for good, the other still lies on the Pile, and nothing was refunded.
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(1, Total(s, Material.Grub));
		Assert.Equal(1, s.ItemCount(At(s, 10, 13), Material.Grub));
		RunTicks(sim, 200);
		Assert.Equal(1, Total(s, Material.Grub));
	}

	[Theory]
	[InlineData(40, 90)]
	[InlineData(70, 100)]
	public void Consuming_a_stolen_Grub_restores_50_Grub_capped_at_100_and_touches_no_other_meter(int before, int expected)
	{
		var sim = Heist();
		var s = sim.State;
		s.GoblinGrub[0] = (byte)before;
		s.GoblinGrog[0] = 60;
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Scoffing);
		RunTicks(sim, 9);
		var grub = s.GoblinGrub[0];
		var grog = s.GoblinGrog[0];
		Assert.Equal(GoblinTask.Scoffing, TaskOf(s, 0));
		Assert.NotEqual(0, (s.Tick + 1) % NeedRules.GrubDrainTicks);
		Assert.NotEqual(0, (s.Tick + 1) % NeedRules.GrogDrainTicks);

		RunTicks(sim, 1);

		Assert.Equal(Math.Min(NeedRules.MeterMax, grub + NeedRules.EatRestore), s.GoblinGrub[0]);
		Assert.InRange((int)s.GoblinGrub[0], expected - 2, expected);
		Assert.Equal(grog, s.GoblinGrog[0]);
	}

	[Fact]
	public void Consuming_a_stolen_Grog_restores_50_Grog_and_nothing_else()
	{
		var sim = Heist(Material.Grog);
		var s = sim.State;
		s.GoblinGrog[0] = 45;
		s.GoblinGrub[0] = 80;
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Scoffing);
		RunTicks(sim, 9);
		var grub = s.GoblinGrub[0];
		var grog = s.GoblinGrog[0];
		Assert.NotEqual(0, (s.Tick + 1) % NeedRules.GrubDrainTicks);
		Assert.NotEqual(0, (s.Tick + 1) % NeedRules.GrogDrainTicks);

		RunTicks(sim, 1);

		Assert.Equal(grog + NeedRules.EatRestore, s.GoblinGrog[0]);
		Assert.Equal(grub, s.GoblinGrub[0]);
	}

	[Fact]
	public void Finishing_removes_40_Chaos_and_starts_the_300_tick_cooldown_once_with_no_other_reward()
	{
		var sim = Heist();
		var s = sim.State;
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Scoffing);
		RunTicks(sim, 9);
		var chaos = s.GoblinChaos[0];
		Assert.Equal(0, s.GoblinActCooldown[0]);
		var interval = (s.Tick + 1) % ChaosRules.UnmetNeedInterval == 0 ? 1 : 0;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(chaos - ChaosRules.ActRelief - interval, s.GoblinChaos[0]);
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[0]);
		Assert.Equal(-1, s.GoblinTheftDestination[0]);
		Assert.Equal((byte)Material.None, s.GoblinTheftSupply[0]);
		// Shinies, a stash or ownership do not exist: the unit is simply gone.
		Assert.Equal(0, Total(s, Material.Grub));
	}

	[Fact]
	public void Starving_slows_the_thiefs_walking_but_not_the_pickup_or_the_private_consumption()
	{
		var sim = Heist(Material.Grog);
		var s = sim.State;
		// No Grub anywhere, so the Craving cannot be met and the goblin keeps its plan.
		s.GoblinGrub[0] = 0;
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sneaking, TaskOf(s, 0));

		var walk = RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Pilfering);
		// Three steps at 6 ticks, not 3.
		Assert.InRange(walk, 17, 19);
		var start = s.Tick;
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.MakingOff);
		Assert.Equal(5, s.Tick - start);

		var carry = RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Scoffing);
		Assert.InRange(carry, 23, 25);
		start = s.Tick;
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Idle);
		Assert.Equal(10, s.Tick - start);
	}

	// ---- Work abandoned to steal

	[Fact]
	public void A_carrier_puts_its_work_unit_down_loose_on_its_own_tile_to_steal_and_the_unit_is_not_lost()
	{
		var sim = Heist();
		var s = sim.State;
		s.GoblinTask[0] = (byte)GoblinTask.Carrying;
		s.GoblinCarry[0] = (byte)Material.Stone;
		// A Pile for the stone to go to would make it a hauler: this one is full of it instead.
		s.AddItems(At(s, 10, 13), Material.Stone, SimState.PileCapacity);
		sim.RecheckReachability();
		var stone = Total(s, Material.Stone);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sneaking, TaskOf(s, 0));
		Assert.Equal(Material.None, (Material)s.GoblinCarry[0]);
		Assert.Equal(1, s.LooseCount(At(s, 10, 10), Material.Stone));
		Assert.Equal(stone, Total(s, Material.Stone));
	}

	[Fact]
	public void A_digger_who_steals_keeps_the_dig_progress()
	{
		var sim = Heist();
		var s = sim.State;
		var tile = At(s, 10, 8);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 8, 10, 8));
		s.GoblinChaos[0] = 0;
		RunTicks(sim, 8);
		var progress = s.DigProgress[tile];
		Assert.True(progress > 0);
		Assert.Equal(GoblinTask.Digging, TaskOf(s, 0));

		Provoke(sim, options: 2, draw: 1);
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sneaking, TaskOf(s, 0));
		Assert.Equal(progress, s.DigProgress[tile]);
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(tile));
	}

	// ---- A plan that changes before the pickup

	// The state of a thief that has failed: Idle with the cooldown, no plan, no unit in hand, and no relief.
	static void AssertFailed(SimState s, int g, int chaosBefore)
	{
		Assert.Equal(GoblinTask.Idle, TaskOf(s, g));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[g]);
		Assert.Equal(-1, s.GoblinTheftDestination[g]);
		Assert.Equal((byte)Material.None, s.GoblinTheftSupply[g]);
		Assert.Equal(-1, s.GoblinJobTile[g]);
		Assert.Equal((byte)Material.None, s.GoblinCarry[g]);
		Assert.True(s.GoblinChaos[g] >= chaosBefore - 1, "A failed theft gives no relief.");
	}

	[Theory]
	[InlineData(GoblinTask.Sneaking)]
	[InlineData(GoblinTask.Pilfering)]
	public void Losing_the_selected_unit_before_the_pickup_fails_the_attempt_and_moves_nothing(GoblinTask stage)
	{
		var sim = Heist();
		var s = sim.State;
		var pile = At(s, 10, 13);
		Stock(sim, 14, 14, Material.Grub, 5);
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == stage);
		var chaos = s.GoblinChaos[0];

		// Someone else took it: the other Pile's Grub is no replacement.
		s.RemoveItems(pile, Material.Grub, 1);
		RunTicks(sim, 1);

		AssertFailed(s, 0, chaos);
		Assert.Equal(0, s.ItemCount(pile, Material.Grub));
		Assert.Equal(5, s.ItemCount(At(s, 14, 14), Material.Grub));
		Assert.Equal(5, Total(s, Material.Grub));
		Assert.Empty(sim.DrainEvents().OfType<TheftPickedUp>());
	}

	[Fact]
	public void Losing_the_Grog_it_chose_fails_the_attempt_though_Grub_remains_on_the_same_Pile()
	{
		var sim = Heist(Material.Grog);
		var s = sim.State;
		var pile = At(s, 10, 13);
		s.AddItems(pile, Material.Grub, 4);
		RunTicks(sim, 2);
		Assert.Equal((byte)Material.Grog, s.GoblinTheftSupply[0]);

		s.RemoveItems(pile, Material.Grog, 1);
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(4, s.ItemCount(pile, Material.Grub));
	}

	[Fact]
	public void Two_thieves_after_the_last_unit_leave_it_to_the_one_who_picks_it_up_first_and_the_other_fails_with_nothing_moved()
	{
		var sim = Yard((10, 10), (10, 16));
		var s = sim.State;
		s.GoblinTask[1] = (byte)GoblinTask.Idle;
		s.GoblinCarry[1] = (byte)Material.None;
		Stock(sim, 10, 13, Material.Grub);
		s.GoblinChaos[0] = s.GoblinChaos[1] = 70;
		// Goblin 0 has goblin 1 to fight as well, so its draw is one of three; goblin 1, with goblin 0 already acting out, has two.
		s.Rng = new SimRandom(Enumerable.Range(0, 1000).First(seed => { var r = new SimRandom(seed); return r.NextInt(3) == 2 && r.NextInt(2) == 1; }));
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Sneaking, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Sneaking, TaskOf(s, 1));
		Assert.Equal(s.GoblinJobTile[0], s.GoblinJobTile[1]);

		RunUntil(sim, st => (GoblinTask)st.GoblinTask[1] != GoblinTask.Sneaking && (GoblinTask)st.GoblinTask[1] != GoblinTask.Pilfering);

		// Both are equally far; the lower index is first in line at the pickup, and the other finds it gone.
		Assert.Equal(1, Total(s, Material.Grub));
		var thieves = Enumerable.Range(0, 2).Where(g => (Material)s.GoblinCarry[g] == Material.Grub || GoblinTasks.IsTheftTask(TaskOf(s, g))).ToList();
		Assert.Single(thieves);
		var loser = 1 - thieves[0];
		Assert.Equal(GoblinTask.Idle, TaskOf(s, loser));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[loser]);
		Assert.Equal(Material.None, (Material)s.GoblinCarry[loser]);
		Assert.Equal(1, Total(s, Material.Grub));
	}

	[Fact]
	public void A_hungry_goblin_who_eats_the_last_unit_on_the_Pile_first_leaves_the_thief_to_fail_at_the_pickup()
	{
		var sim = Yard((10, 10), (10, 12));
		var s = sim.State;
		s.GoblinTask[1] = (byte)GoblinTask.Idle;
		s.GoblinCarry[1] = (byte)Material.None;
		s.GoblinGrub[1] = 20;
		Stock(sim, 10, 13, Material.Grub);
		Provoke(sim, options: 2, draw: 1);

		RunUntil(sim, st => (GoblinTask)st.GoblinTask[1] == GoblinTask.Eating);
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] != GoblinTask.Sneaking && (GoblinTask)st.GoblinTask[0] != GoblinTask.Pilfering);

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[0]);
		Assert.Equal(Material.None, (Material)s.GoblinCarry[0]);
		Assert.Equal(0, Total(s, Material.Grub));
		// The meal was the hungry goblin's: it restored its Grub, and the thief had no part in it.
		Assert.True(s.GoblinGrub[1] >= 60);
	}

	[Theory]
	[InlineData(GoblinTask.Sneaking)]
	[InlineData(GoblinTask.Pilfering)]
	public void A_source_that_stops_being_a_Pile_before_the_pickup_fails_the_attempt_and_leaves_the_stock_where_it_lies(GoblinTask stage)
	{
		var sim = Heist(units: 3);
		var s = sim.State;
		var pile = At(s, 10, 13);
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == stage);
		var chaos = s.GoblinChaos[0];

		sim.Apply(new RemovePile(0, 10, 13, 10, 13));
		RunTicks(sim, 1);

		AssertFailed(s, 0, chaos);
		// The stock is loose now, as ordinary eating and hauling find it.
		Assert.Equal(3, s.LooseCount(pile, Material.Grub));
		Assert.Equal(3, Total(s, Material.Grub));
	}

	[Theory]
	[InlineData(GoblinTask.Sneaking)]
	[InlineData(GoblinTask.Pilfering)]
	public void A_destination_painted_as_a_Pile_before_the_pickup_fails_the_attempt_and_no_replacement_is_chosen(GoblinTask stage)
	{
		var sim = Heist(units: 3);
		var s = sim.State;
		var pile = At(s, 10, 13);
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == stage);
		var chaos = s.GoblinChaos[0];
		Assert.Equal(At(s, 10, 9), s.GoblinTheftDestination[0]);

		sim.Apply(new PaintPile(0, 10, 9, 10, 9));
		RunTicks(sim, 1);

		AssertFailed(s, 0, chaos);
		Assert.Equal(3, s.ItemCount(pile, Material.Grub));
		// It does not try again for the whole cooldown, at (10, 8), (9, 9) or anywhere else.
		RunTicks(sim, 200);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(3, Total(s, Material.Grub));
	}

	[Fact]
	public void A_destination_that_can_no_longer_be_reached_fails_the_attempt_before_the_pickup()
	{
		var sim = Yard((14, 13));
		var s = sim.State;
		Stock(sim, 10, 13, Material.Grub, 3);
		Provoke(sim, options: 2, draw: 1);
		RunTicks(sim, 2);
		Assert.Equal(At(s, 10, 9), s.GoblinTheftDestination[0]);

		// The tile is still floor and no Pile, but walled in.
		foreach (var (x, y) in new[] { (9, 9), (11, 9), (10, 8), (10, 10) })
			s.TileKinds[At(s, x, y)] = (byte)TileKind.Solid;
		sim.RecheckReachability();
		var chaos = s.GoblinChaos[0];
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Idle, limit: 40);

		AssertFailed(s, 0, chaos);
		Assert.Equal(3, s.ItemCount(At(s, 10, 13), Material.Grub));
	}

	// ---- A plan that changes after the pickup

	// The thief just after the pickup, standing on its source with the destination four steps off at (10, 9).
	static Simulation Loot(int units = 1)
	{
		var sim = Heist(units: units);
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.MakingOff);
		return sim;
	}

	[Fact]
	public void A_destination_painted_as_a_Pile_after_the_pickup_puts_the_unit_back_on_the_Pile_it_stands_on_for_no_relief()
	{
		var sim = Loot(units: SimState.PileCapacity);
		var s = sim.State;
		var pile = At(s, 10, 13);
		Assert.Equal(SimState.PileCapacity - 1, s.ItemCount(pile, Material.Grub));
		var chaos = s.GoblinChaos[0];
		var grub = s.GoblinGrub[0];

		sim.Apply(new PaintPile(0, 10, 9, 10, 9));
		RunTicks(sim, 1);

		AssertFailed(s, 0, chaos);
		Assert.Equal(SimState.PileCapacity, s.ItemCount(pile, Material.Grub));
		Assert.Equal(SimState.PileCapacity, Total(s, Material.Grub));
		// The unit is not consumed, so nothing is restored: the meter only drains.
		Assert.True(s.GoblinGrub[0] <= grub);
	}

	[Fact]
	public void A_unit_put_down_away_from_a_Pile_lies_loose_for_ordinary_hauling_and_eating()
	{
		var sim = Loot();
		var s = sim.State;
		RunTicks(sim, 6);
		Assert.Equal((10, 11), (s.GoblinX[0], s.GoblinY[0]));
		var chaos = s.GoblinChaos[0];

		sim.Apply(new PaintPile(0, 10, 9, 10, 9));
		RunTicks(sim, 1);

		AssertFailed(s, 0, chaos);
		Assert.Equal(1, s.LooseCount(At(s, 10, 11), Material.Grub));
		Assert.Equal(1, Total(s, Material.Grub));
	}

	[Fact]
	public void A_destination_that_becomes_unreachable_while_carrying_puts_the_unit_down_legally_and_ends_without_relief()
	{
		var sim = Yard((14, 13));
		var s = sim.State;
		Stock(sim, 10, 13, Material.Grub, 3);
		Provoke(sim, options: 2, draw: 1);
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.MakingOff);
		var chaos = s.GoblinChaos[0];

		foreach (var (x, y) in new[] { (9, 9), (11, 9), (10, 8), (10, 10) })
			s.TileKinds[At(s, x, y)] = (byte)TileKind.Solid;
		sim.RecheckReachability();
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Idle, limit: 40);

		AssertFailed(s, 0, chaos);
		Assert.Equal(3, Total(s, Material.Grub));
	}

	[Fact]
	public void A_spot_that_becomes_a_Pile_under_a_thief_who_is_eating_ends_the_theft_and_stores_the_unit_there_with_no_relief()
	{
		var sim = Loot();
		var s = sim.State;
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Scoffing);
		RunTicks(sim, 3);
		var chaos = s.GoblinChaos[0];
		var spot = At(s, 10, 9);
		var grub = s.GoblinGrub[0];

		sim.Apply(new PaintPile(0, 10, 9, 10, 9));
		RunTicks(sim, 1);

		AssertFailed(s, 0, chaos);
		Assert.Equal(1, s.ItemCount(spot, Material.Grub));
		Assert.True(s.GoblinGrub[0] <= grub);
	}

	// Two tiles are all the thief can reach: the Pile it stole from at (10, 10) and the spot beside it at (11, 10), where it stood to begin with.
	static Simulation TwoTiles(out int source, out int spot)
	{
		var sim = Yard((11, 10));
		var s = sim.State;
		source = At(s, 10, 10);
		spot = At(s, 11, 10);
		for (var tile = 0; tile < s.Width * s.Height; tile++)
			if (tile != source && tile != spot)
				s.TileKinds[tile] = (byte)TileKind.Solid;
		sim.RecheckReachability();
		Stock(sim, 10, 10, Material.Grub, 2);
		Provoke(sim, options: 2, draw: 1);
		return sim;
	}

	// The thief has the unit, and while it was off the Pile a hauler refilled the Pile and the spot became a full Pile too: nowhere legal to put the unit.
	static Simulation Cornered(out int source, out int spot)
	{
		var sim = TwoTiles(out source, out spot);
		var s = sim.State;
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.MakingOff);
		s.AddItems(source, Material.Grub, SimState.PileCapacity - s.ItemCount(source, Material.Grub));
		sim.Apply(new PaintPile(0, 11, 10, 11, 10));
		s.AddItems(spot, Material.Grub, SimState.PileCapacity - s.ItemCount(spot, Material.Grub));
		sim.RecheckReachability();
		// The next tick finds the plan broken and the unit with nowhere to go.
		RunTicks(sim, 1);
		return sim;
	}

	[Fact]
	public void With_no_legal_place_to_put_the_unit_down_a_failed_thief_keeps_it_and_waits_without_consuming_or_relief()
	{
		var sim = Cornered(out var source, out var spot);
		var s = sim.State;
		var chaos = s.GoblinChaos[0];
		var grub = s.GoblinGrub[0];
		var total = Total(s, Material.Grub);
		Assert.Equal(SimState.PileCapacity * 2 + 1, total);

		RunTicks(sim, 60);

		Assert.Equal(GoblinTask.Stalled, TaskOf(s, 0));
		Assert.Equal(Material.Grub, (Material)s.GoblinCarry[0]);
		Assert.Equal(0, s.GoblinActCooldown[0]);
		Assert.Equal(0, s.GoblinWorkTicks[0]);
		Assert.True(s.GoblinGrub[0] <= grub);
		Assert.True(s.GoblinChaos[0] >= chaos - 6);
		Assert.Equal(total, Total(s, Material.Grub));
		Assert.Equal(SimState.PileCapacity, s.ItemCount(source, Material.Grub));
		Assert.Equal(SimState.PileCapacity, s.ItemCount(spot, Material.Grub));
	}

	[Fact]
	public void A_stalled_thief_puts_the_unit_down_the_moment_a_legal_place_opens_and_only_then_starts_its_cooldown()
	{
		var sim = Cornered(out var source, out var spot);
		var s = sim.State;
		RunTicks(sim, 20);
		Assert.Equal(GoblinTask.Stalled, TaskOf(s, 0));
		var chaos = s.GoblinChaos[0];

		sim.Apply(new RemovePile(0, 11, 10, 11, 10));
		RunTicks(sim, 1);

		AssertFailed(s, 0, chaos);
		// The spot is floor again, so the unit lies there loose beside the stock that was loose with it.
		Assert.Equal(SimState.PileCapacity + 1, s.LooseCount(spot, Material.Grub));
		Assert.Equal(SimState.PileCapacity, s.ItemCount(source, Material.Grub));
		Assert.Equal(SimState.PileCapacity * 2 + 1, Total(s, Material.Grub));
	}

	[Fact]
	public void A_stalled_thief_stores_the_unit_when_a_Pile_gains_room_and_never_overfills_it()
	{
		var sim = Cornered(out var source, out var spot);
		var s = sim.State;
		RunTicks(sim, 20);
		Assert.Equal(GoblinTask.Stalled, TaskOf(s, 0));

		s.RemoveItems(source, Material.Grub, 1);
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[0]);
		Assert.Equal(SimState.PileCapacity, s.ItemCount(source, Material.Grub));
		Assert.Equal(SimState.PileCapacity, s.ItemCount(spot, Material.Grub));
		Assert.Equal(SimState.PileCapacity * 2, Total(s, Material.Grub));
	}

	// ---- A Need interrupts a theft

	public static TheoryData<GoblinTask> Stages => new() { GoblinTask.Sneaking, GoblinTask.Pilfering, GoblinTask.MakingOff, GoblinTask.Scoffing, GoblinTask.Stalled };

	// A thief in the given stage, with loose Grub lying at (14, 14) in reach of a Craving.
	static Simulation InStage(GoblinTask stage)
	{
		Simulation sim;
		if (stage == GoblinTask.Stalled)
			sim = Cornered(out _, out _);
		else
		{
			sim = Heist(units: 3);
			RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == stage);
			sim.State.AddItems(At(sim.State, 14, 14), Material.Grub, 2);
			sim.RecheckReachability();
		}
		Assert.Equal(stage, TaskOf(sim.State, 0));
		return sim;
	}

	[Theory]
	[MemberData(nameof(Stages))]
	public void A_satisfiable_Craving_ends_the_theft_at_any_stage_for_no_relief_with_the_unit_put_down_and_the_cooldown_started(GoblinTask stage)
	{
		var sim = InStage(stage);
		var s = sim.State;
		var chaos = s.GoblinChaos[0];
		var grub = Total(s, Material.Grub);
		var grog = s.GoblinGrog[0];
		var open = stage == GoblinTask.Stalled;
		if (open)
			sim.Apply(new RemovePile(0, 11, 10, 11, 10));

		s.GoblinGrub[0] = 20;
		RunTicks(sim, 1);

		// Seeking, or Eating at once when the thief stands on a Pile holding Grub.
		Assert.Equal(Need.Grub, GoblinTasks.NeedOf(TaskOf(s, 0)));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[0]);
		Assert.Equal(Material.None, (Material)s.GoblinCarry[0]);
		Assert.Equal(-1, s.GoblinTheftDestination[0]);
		Assert.Equal((byte)Material.None, s.GoblinTheftSupply[0]);
		Assert.True(s.GoblinChaos[0] >= chaos - 1, "An interrupted theft gives no relief.");
		// Whatever it was carrying lies in the warren; nothing was consumed and nothing was refunded.
		Assert.Equal(grub, Total(s, Material.Grub));
		Assert.Equal(grog, s.GoblinGrog[0]);
		Assert.Equal(20, s.GoblinGrub[0]);
	}

	[Theory]
	[MemberData(nameof(Stages))]
	public void Collapse_ends_the_theft_at_any_stage_for_no_relief(GoblinTask stage)
	{
		var sim = InStage(stage);
		var s = sim.State;
		var chaos = s.GoblinChaos[0];
		var grub = Total(s, Material.Grub);
		if (stage == GoblinTask.Stalled)
			sim.Apply(new RemovePile(0, 11, 10, 11, 10));

		s.GoblinSleep[0] = 0;
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[0]);
		Assert.Equal(Material.None, (Material)s.GoblinCarry[0]);
		Assert.True(s.GoblinChaos[0] >= chaos - 1);
		Assert.Equal(grub, Total(s, Material.Grub));
	}

	[Fact]
	public void An_interrupted_theft_never_resumes_and_the_meal_that_interrupted_it_is_all_that_is_consumed()
	{
		var sim = InStage(GoblinTask.Scoffing);
		var s = sim.State;
		var grub = Total(s, Material.Grub);
		sim.DrainEvents();
		s.GoblinChaos[0] = 100;
		s.GoblinGrub[0] = 20;

		RunTicks(sim, 250);

		Assert.Equal(grub - 1, Total(s, Material.Grub));
		Assert.True(s.GoblinGrub[0] >= 60);
		Assert.DoesNotContain(Enumerable.Range(0, 1), g => GoblinTasks.IsTheftTask(TaskOf(s, g)));
		Assert.Empty(sim.DrainEvents().OfType<TheftPickedUp>());
	}

	[Fact]
	public void A_Craving_nothing_in_reach_can_meet_does_not_interrupt_the_theft()
	{
		var sim = Heist(Material.Grog);
		var s = sim.State;
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Scoffing);
		s.GoblinGrub[0] = 20;
		var grog = s.GoblinGrog[0];

		RunTicks(sim, 10);

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal(Math.Min(100, grog + NeedRules.EatRestore), s.GoblinGrog[0]);
	}

	public enum LastTickNeed
	{
		Sleep,
		Grub,
		Grog,
	}

	// The tick a thief consuming its unit would finish on is moved onto a tick that the given Need drains on, and its meter is set so that the drain on that very tick is
	// what brings the Need below its threshold: the Need arises on the last tick, before the goblin's own turn. (Collapse cannot arise this way: Sleep below 30 is a Craving
	// that is always satisfiable, so it has interrupted long before the meter reaches 0.)
	[Theory]
	[InlineData(LastTickNeed.Sleep, 45)]
	[InlineData(LastTickNeed.Grub, 50)]
	[InlineData(LastTickNeed.Grog, 70)]
	public void A_Need_that_arises_on_the_last_tick_of_private_consumption_interrupts_it_with_no_relief(LastTickNeed need, long lastTick)
	{
		var sim = InStage(GoblinTask.Scoffing);
		var s = sim.State;
		s.Tick = lastTick - 10;
		s.GoblinGrub[0] = 100;
		s.GoblinGrog[0] = 100;
		s.GoblinSleep[0] = 100;
		switch (need)
		{
			case LastTickNeed.Sleep: s.GoblinSleep[0] = 30; break;
			case LastTickNeed.Grub: s.GoblinGrub[0] = 30; break;
			case LastTickNeed.Grog: s.GoblinGrog[0] = 30; break;
		}
		s.AddItems(At(s, 14, 14), Material.Grog, 2);
		sim.RecheckReachability();
		var chaos = s.GoblinChaos[0];
		var grub = Total(s, Material.Grub);
		RunTicks(sim, 9);
		Assert.Equal(GoblinTask.Scoffing, TaskOf(s, 0));
		Assert.Equal(9, s.GoblinWorkTicks[0]);

		RunTicks(sim, 1);

		Assert.Equal(need switch { LastTickNeed.Sleep => Need.Sleep, LastTickNeed.Grub => Need.Grub, _ => Need.Grog }, GoblinTasks.NeedOf(TaskOf(s, 0)));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[0]);
		Assert.True(s.GoblinChaos[0] >= chaos - 1, "No relief.");
		Assert.Equal(grub, Total(s, Material.Grub));
		Assert.Equal(Material.None, (Material)s.GoblinCarry[0]);
	}

	[Fact]
	public void A_stalled_thief_with_a_Craving_stays_put_until_a_place_opens_then_puts_the_unit_down_and_goes_to_meet_it()
	{
		var sim = Cornered(out var source, out _);
		var s = sim.State;
		RunTicks(sim, 5);
		Assert.Equal(GoblinTask.Stalled, TaskOf(s, 0));

		s.GoblinGrub[0] = 20;
		RunTicks(sim, 20);
		Assert.Equal(GoblinTask.Stalled, TaskOf(s, 0));
		Assert.Equal(0, s.GoblinActCooldown[0]);
		Assert.Equal(Material.Grub, (Material)s.GoblinCarry[0]);

		s.RemoveItems(source, Material.Grub, 1);
		RunTicks(sim, 1);

		Assert.Equal(Need.Grub, GoblinTasks.NeedOf(TaskOf(s, 0)));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[0]);
		Assert.Equal(Material.None, (Material)s.GoblinCarry[0]);
		Assert.Equal(SimState.PileCapacity * 2, Total(s, Material.Grub));
	}

	[Fact]
	public void A_satisfiable_Craving_comes_before_starting_a_theft()
	{
		var sim = Heist();
		var s = sim.State;
		s.GoblinGrub[0] = 20;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(0, s.GoblinActCooldown[0]);
	}

	[Fact]
	public void A_sleeper_at_70_Chaos_does_not_begin_a_theft_until_it_wakes()
	{
		var sim = Heist();
		var s = sim.State;
		s.GoblinSleep[0] = 0;
		RunTicks(sim, 5);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));

		RunTicks(sim, 100);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(1, Total(s, Material.Grub));
	}

	[Theory]
	[MemberData(nameof(Stages))]
	public void A_goblin_in_any_stage_of_a_theft_starts_no_other_act_and_is_set_upon_by_nobody(GoblinTask stage)
	{
		for (var seed = 0; seed < 24; seed++)
		{
			var sim = InStage(stage);
			var s = sim.State;
			s.GoblinCount = 2;
			s.GoblinX[1] = 14;
			s.GoblinY[1] = 16;
			if (stage == GoblinTask.Stalled)
				(s.GoblinX[1], s.GoblinY[1]) = (11, 10);
			(s.GoblinGrub[1], s.GoblinGrog[1], s.GoblinSleep[1]) = (100, 100, 100);
			s.GoblinTask[1] = (byte)GoblinTask.Idle;
			s.GoblinChaos[1] = 100;
			s.GoblinActCooldown[1] = 0;
			s.Rng = new SimRandom(seed);
			sim.RecheckReachability();

			// Goblin 1 may shirk or steal itself, but the thief is no target, so it never fights it; the thief starts nothing else.
			RunTicks(sim, 1);

			Assert.NotEqual(GoblinTask.Pursuing, TaskOf(s, 1));
			Assert.True(GoblinTasks.IsTheftTask(TaskOf(s, 0)), $"Seed {seed}: the thief is {TaskOf(s, 0)}.");
		}
	}

	// ---- Chaos while stealing

	[Fact]
	public void The_Need_a_thief_is_privately_restoring_adds_no_Chaos_while_it_is_consumed_but_any_other_low_Need_does()
	{
		// Grog is stolen; with none left anywhere the Craving for it cannot be met, so only the Chaos rules decide.
		var sim = Heist(Material.Grog);
		var s = sim.State;
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.MakingOff);
		s.GoblinGrog[0] = 20;
		var walking = s.GoblinChaos[0];
		RunUntil(sim, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Scoffing);
		var arrived = s.GoblinChaos[0];
		// Walking with the Need unmet it was adding.
		Assert.True(arrived >= walking, "An unmet Need adds Chaos while the goblin walks.");

		var start = s.GoblinChaos[0];
		RunTicks(sim, 9);
		Assert.Equal(GoblinTask.Scoffing, TaskOf(s, 0));
		// A whole interval has passed while the thief was consuming the unit: the Grog is being restored, so it adds nothing, and being low it lets nothing fade either.
		Assert.Equal(start, s.GoblinChaos[0]);

		// Another low Need is not being restored, so it still adds.
		var other = Heist(Material.Grog);
		RunUntil(other, st => (GoblinTask)st.GoblinTask[0] == GoblinTask.Scoffing);
		var o = other.State;
		o.GoblinChaos[0] = 50;
		o.GoblinGrog[0] = 20;
		o.GoblinGrub[0] = 20; // no Grub in reach, so this Craving stays unmet
		var before = o.GoblinChaos[0];
		RunTicks(other, 9);
		Assert.Equal(before + 1, o.GoblinChaos[0]);
	}
}
