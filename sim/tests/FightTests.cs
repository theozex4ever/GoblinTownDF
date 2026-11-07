using System.Text.Json;
using System.Text.Json.Nodes;
using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>P4's fights: a goblin at 70 Chaos picks a nonlethal fight, pursues its target, scuffles for 30 ticks and both fighters recover.</summary>
public class FightTests
{
	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	static int At(SimState s, int x, int y, int z = 0) => s.TileIndex(x, y, z);

	// A world of the given goblins standing at the given tiles of z0: every Need full, no Chaos, no cooldown, nothing to eat, drink or haul
	// anywhere. Goblins after the first are bystanders that wait still: each holds a unit of Dirt and has no Pile to take it to, so it never
	// wanders, draws nothing from the Rng and can put the unit down on the spot.
	static Simulation Arena(params (int X, int Y)[] at)
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
				Wait(s, g);
		}
		sim.RecheckReachability();
		return sim;
	}

	static void Wait(SimState s, int g)
	{
		s.GoblinTask[g] = (byte)GoblinTask.Carrying;
		s.GoblinCarry[g] = (byte)Material.Dirt;
	}

	// Goblin g is ready to act out, and the Rng will draw a fight (draw 1 of 2) for it and for each goblin after it that is given a draw.
	static void Provoke(Simulation sim, params int[] goblins)
	{
		foreach (var g in goblins)
			sim.State.GoblinChaos[g] = ChaosRules.ActingOutThreshold;
		sim.State.Rng = SeedDrawing(Enumerable.Repeat(1, goblins.Length).ToArray());
	}

	// A generator whose first draws of NextInt(2) are the given ones.
	static SimRandom SeedDrawing(params int[] draws)
	{
		for (var seed = 0; ; seed++)
		{
			var probe = new SimRandom(seed);
			if (draws.All(d => probe.NextInt(2) == d))
				return new SimRandom(seed);
		}
	}

	static Simulation Fight(params (int X, int Y)[] at)
	{
		var sim = Arena(at);
		Provoke(sim, 0);
		return sim;
	}

	// ---- Choosing to fight

	[Fact]
	public void A_goblin_at_70_Chaos_with_a_target_pursues_it_when_the_draw_picks_a_fight()
	{
		var sim = Fight((10, 10), (14, 10));
		var s = sim.State;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Pursuing, TaskOf(s, 0));
		Assert.Equal(1, s.GoblinFightPartner[0]);
		Assert.Equal(ChaosRules.PursuitTicks, s.GoblinFightTicks[0]);
		// The target keeps working and knows nothing yet.
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 1));
		Assert.Equal(-1, s.GoblinFightPartner[1]);
		Assert.Empty(sim.DrainEvents().OfType<ScuffleStarted>());
		Assert.Empty(sim.DrainEvents().OfType<ShirkStarted>());
	}

	[Fact]
	public void The_same_goblin_shirks_when_the_draw_picks_that_and_with_no_target_shirking_draws_nothing()
	{
		var shirks = Arena((10, 10), (14, 10));
		shirks.State.GoblinChaos[0] = 70;
		shirks.State.Rng = SeedDrawing(0);
		RunTicks(shirks, 1);
		Assert.Equal(GoblinTask.Shirking, TaskOf(shirks.State, 0));

		var alone = Arena((10, 10));
		alone.State.GoblinChaos[0] = 70;
		var before = alone.State.Rng.State;
		RunTicks(alone, 1);
		Assert.Equal(GoblinTask.Shirking, TaskOf(alone.State, 0));
		Assert.Equal(before, alone.State.Rng.State);
	}

	[Fact]
	public void Fight_and_shirk_are_equally_likely_when_both_are_eligible()
	{
		var fights = 0;
		for (var seed = 0; seed < 400; seed++)
		{
			var sim = Arena((10, 10), (14, 10));
			sim.State.GoblinChaos[0] = 70;
			sim.State.Rng = new SimRandom(seed);
			sim.Advance(0.1);
			if (TaskOf(sim.State, 0) == GoblinTask.Pursuing)
				fights++;
			else
				Assert.Equal(GoblinTask.Shirking, TaskOf(sim.State, 0));
		}

		Assert.InRange(fights, 160, 240);
	}

	// ---- Choosing a target

	// The goblin Pursuing after one tick, or -1. Some tests fabricate states no run reaches, so the Task rules are not asked here.
	static int TargetOf(Simulation sim)
	{
		sim.Advance(0.1);
		var s = sim.State;
		return TaskOf(s, 0) == GoblinTask.Pursuing ? s.GoblinFightPartner[0] : -1;
	}

	[Fact]
	public void The_target_is_the_nearest_eligible_goblin_by_walking_distance()
	{
		Assert.Equal(2, TargetOf(Fight((10, 10), (20, 10), (13, 10), (16, 10))));
	}

	[Fact]
	public void Equally_near_targets_go_to_the_lowest_goblin_index_whatever_their_places()
	{
		Assert.Equal(1, TargetOf(Fight((10, 10), (12, 10), (8, 10))));
		Assert.Equal(1, TargetOf(Fight((10, 10), (8, 10), (12, 10))));
	}

	[Fact]
	public void Two_goblins_on_one_tile_go_to_the_lower_index()
	{
		Assert.Equal(1, TargetOf(Fight((10, 10), (13, 10), (13, 10))));
	}

	[Fact]
	public void A_goblin_on_another_level_is_not_a_target_however_near_the_stairs_make_it()
	{
		var sim = Fight((10, 10), (12, 10), (25, 10));
		var s = sim.State;
		// Stairs at (11, 10) climb to a floor on z1 where goblin 1 stands: three steps away, against fifteen to goblin 2.
		s.TileKinds[At(s, 11, 10, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 11, 10, 1)] = (byte)TileKind.Stairs;
		s.TileKinds[At(s, 12, 10, 1)] = (byte)TileKind.Floor;
		s.GoblinZ[1] = 1;
		sim.RecheckReachability();

		Assert.Equal(2, TargetOf(sim));
	}

	[Fact]
	public void A_goblin_no_path_leads_to_is_not_a_target()
	{
		var sim = Fight((10, 10), (14, 14), (25, 10));
		var s = sim.State;
		foreach (var (x, y) in new[] { (13, 14), (15, 14), (14, 13), (14, 15) })
			s.TileKinds[At(s, x, y)] = (byte)TileKind.Solid;
		sim.RecheckReachability();

		Assert.Equal(2, TargetOf(sim));
	}

	[Fact]
	public void With_only_unreachable_goblins_the_goblin_shirks()
	{
		var sim = Fight((10, 10), (14, 14));
		var s = sim.State;
		foreach (var (x, y) in new[] { (13, 14), (15, 14), (14, 13), (14, 15) })
			s.TileKinds[At(s, x, y)] = (byte)TileKind.Solid;
		sim.RecheckReachability();

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 0));
	}

	public static TheoryData<string> Ineligible => new() { "sleeping", "seeking", "heap", "shirking", "pursuing", "scuffled", "recovering", "cooldown" };

	// Makes goblin 1 what the name says. The far goblin 2 is the only one left to pick.
	static void Make(Simulation sim, int g, string kind)
	{
		var s = sim.State;
		s.GoblinTask[g] = (byte)GoblinTask.Idle;
		s.GoblinCarry[g] = (byte)Material.None;
		switch (kind)
		{
			case "sleeping":
				s.GoblinSleep[g] = 50;
				s.GoblinTask[g] = (byte)GoblinTask.Sleeping;
				break;
			case "seeking":
				s.GoblinGrub[g] = 10;
				s.GoblinTask[g] = (byte)GoblinTask.Seeking;
				break;
			case "heap":
				s.GoblinSleep[g] = 10;
				s.GoblinTask[g] = (byte)GoblinTask.SeekingHeap;
				break;
			case "shirking":
				s.GoblinTask[g] = (byte)GoblinTask.Shirking;
				break;
			case "pursuing":
				s.GoblinTask[g] = (byte)GoblinTask.Pursuing;
				s.GoblinFightPartner[g] = 2;
				s.GoblinFightTicks[g] = 50;
				break;
			case "scuffled":
			case "recovering":
				// Fought goblin 2 itself, so the pair is whole.
				s.GoblinTask[g] = (byte)(kind == "scuffled" ? GoblinTask.Scuffled : GoblinTask.Recovering);
				s.GoblinFightPartner[g] = 3;
				s.GoblinFightTicks[g] = 10;
				s.GoblinActCooldown[g] = (ushort)(kind == "recovering" ? 300 : 0);
				break;
			case "cooldown":
				s.GoblinActCooldown[g] = 5;
				Wait(s, g);
				break;
		}
	}

	[Theory]
	[MemberData(nameof(Ineligible))]
	public void A_sleeping_seeking_acting_out_or_cooling_down_goblin_is_never_chosen_as_the_target(string kind)
	{
		var sim = Fight((10, 10), (12, 10), (25, 10), (26, 10));
		Make(sim, 1, kind);
		Assert.Equal(2, TargetOf(sim));
	}

	[Fact]
	public void A_goblin_with_no_legal_place_to_put_its_unit_down_is_not_a_target()
	{
		var sim = Fight((16, 16), (17, 16), (30, 30));
		var s = sim.State;
		// Goblin 1 and the only other tile it and goblin 0 can reach are full Piles of Dirt, and it holds a unit of Dirt.
		var here = At(s, 17, 16);
		var beside = At(s, 16, 16);
		for (var tile = 0; tile < s.Width * s.Height; tile++)
			if (tile != here && tile != beside && tile != At(s, 30, 30))
				s.TileKinds[tile] = (byte)TileKind.Solid;
		foreach (var tile in new[] { here, beside })
		{
			s.AddItems(tile, Material.Dirt, SimState.PileCapacity);
			s.Piles[tile] = 1;
		}
		sim.RecheckReachability();
		// Goblin 2 is walled off, so goblin 1 would be the only target if its drop were legal.
		Assert.Equal(-1, TargetOf(sim));
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 1));
	}

	// ---- Pursuit and contact

	[Fact]
	public void The_pursuer_walks_at_the_ordinary_pace_and_makes_contact_on_an_orthogonal_neighbour()
	{
		var sim = Fight((10, 10), (14, 10));
		var s = sim.State;

		RunTicks(sim, 5);
		Assert.Equal(GoblinTask.Pursuing, TaskOf(s, 0));
		Assert.Equal((11, 10), (s.GoblinX[0], s.GoblinY[0]));
		Assert.Empty(sim.DrainEvents().OfType<ScuffleStarted>());

		RunTicks(sim, 1);
		Assert.Equal((12, 10), (s.GoblinX[0], s.GoblinY[0]));
		Assert.Equal(GoblinTask.Pursuing, TaskOf(s, 0));

		RunTicks(sim, 3);
		Assert.Equal((13, 10), (s.GoblinX[0], s.GoblinY[0]));
		Assert.Equal(GoblinTask.Scuffling, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Scuffled, TaskOf(s, 1));
		Assert.Contains(new ScuffleStarted(0, 1), sim.DrainEvents());
	}

	[Fact]
	public void Contact_on_the_same_tile_is_contact_at_once()
	{
		var sim = Fight((10, 10), (10, 10));
		var s = sim.State;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Scuffling, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Scuffled, TaskOf(s, 1));
		Assert.Contains(new ScuffleStarted(0, 1), sim.DrainEvents());
	}

	[Fact]
	public void Contact_pairs_the_fighters_and_gives_both_a_scuffle_clock_of_30()
	{
		var sim = Fight((10, 10), (10, 11));
		var s = sim.State;

		RunTicks(sim, 1);

		Assert.Equal((1, 0), (s.GoblinFightPartner[0], s.GoblinFightPartner[1]));
		Assert.Equal(ChaosRules.ScuffleTicks, s.GoblinFightTicks[0]);
		Assert.Equal(ChaosRules.ScuffleTicks, s.GoblinFightTicks[1]);
		// Neither moves again.
		RunTicks(sim, 25);
		Assert.Equal((10, 10), (s.GoblinX[0], s.GoblinY[0]));
		Assert.Equal((10, 11), (s.GoblinX[1], s.GoblinY[1]));
	}

	[Fact]
	public void The_target_keeps_working_until_contact_then_puts_its_unit_down_and_gains_15_Chaos_once()
	{
		var sim = Fight((10, 10), (14, 10));
		var s = sim.State;

		RunTicks(sim, 8);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 1));
		Assert.Equal(0, s.GoblinChaos[1]);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Scuffled, TaskOf(s, 1));
		Assert.Equal((byte)Material.None, s.GoblinCarry[1]);
		Assert.Equal(1, s.ItemCount(At(s, 14, 10), Material.Dirt));
		Assert.Equal(ChaosRules.VictimGain, s.GoblinChaos[1]);

		RunTicks(sim, 25);
		// Content again, it only fades.
		Assert.True(s.GoblinChaos[1] <= ChaosRules.VictimGain);
	}

	[Fact]
	public void A_pursuit_that_runs_100_ticks_without_contact_fails_the_attempt_for_the_instigator_alone()
	{
		var sim = Fight((0, 0), (31, 31));
		var s = sim.State;
		s.GoblinChaos[0] = 80;

		RunTicks(sim, 100);
		Assert.Equal(GoblinTask.Pursuing, TaskOf(s, 0));
		Assert.Equal(0, s.GoblinActCooldown[0]);

		RunTicks(sim, 1);
		Assert.NotEqual(GoblinTask.Pursuing, TaskOf(s, 0));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[0]);
		Assert.Equal(-1, s.GoblinFightPartner[0]);
		Assert.Equal(0, s.GoblinFightTicks[0]);
		// No relief for a failed attempt: ten content intervals took 10 off, nothing else.
		Assert.Equal(70, s.GoblinChaos[0]);
		// The target is a waiting carrier, so only the five waiting intervals touched it: no victim's 15.
		Assert.Equal(5, s.GoblinChaos[1]);
		Assert.Empty(sim.DrainEvents().OfType<ScuffleStarted>());
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 1));
	}

	[Fact]
	public void A_target_that_became_ineligible_by_contact_fails_the_attempt_with_no_replacement()
	{
		var sim = Fight((10, 10), (14, 10), (15, 12));
		var s = sim.State;
		RunTicks(sim, 4);
		Assert.Equal(1, s.GoblinFightPartner[0]);
		// Goblin 1 falls asleep. Goblin 2 is an eligible bystander that must be left alone, not chosen in its place.
		s.GoblinCarry[1] = (byte)Material.None;
		s.GoblinSleep[1] = 50;
		s.GoblinTask[1] = (byte)GoblinTask.Sleeping;

		RunTicks(sim, 10);

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		// The attempt failed at contact, on the sixth tick of the walk, and the cooldown has counted down since.
		Assert.InRange(s.GoblinActCooldown[0], ChaosRules.CooldownTicks - 10, ChaosRules.CooldownTicks);
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 2));
		Assert.Equal(0, s.GoblinChaos[1]);
		Assert.Empty(sim.DrainEvents().OfType<ScuffleStarted>());
	}

	// ---- Two goblins reaching one target

	[Fact]
	public void When_two_pursuers_reach_one_target_together_the_lower_index_scuffles_and_the_other_fails()
	{
		var sim = Arena((10, 10), (18, 10), (14, 10));
		var s = sim.State;
		Provoke(sim, 0, 1);

		RunTicks(sim, 9);

		Assert.Equal(GoblinTask.Scuffling, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Scuffled, TaskOf(s, 2));
		Assert.Equal(0, s.GoblinFightPartner[2]);
		// Goblin 1 reached it on the same tick, found it taken and fails without picking again.
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 1));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[1]);
		Assert.Equal(ChaosRules.VictimGain, s.GoblinChaos[2]);
		Assert.Single(sim.DrainEvents().OfType<ScuffleStarted>());
	}

	[Fact]
	public void The_lower_index_wins_the_contact_when_it_is_the_target_that_comes_first_in_the_order_too()
	{
		var sim = Arena((14, 10), (10, 10), (18, 10));
		var s = sim.State;
		// Goblin 0 is the target, so the pursuers are 1 and 2; goblin 1 comes first.
		Wait(s, 0);
		Provoke(sim, 1, 2);

		RunTicks(sim, 9);

		Assert.Equal(GoblinTask.Scuffling, TaskOf(s, 1));
		Assert.Equal(GoblinTask.Scuffled, TaskOf(s, 0));
		Assert.Equal(1, s.GoblinFightPartner[0]);
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 2));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[2]);
	}

	[Fact]
	public void A_goblin_being_pursued_is_not_chosen_for_a_second_fight_but_may_not_start_one_either_while_pursuing()
	{
		var sim = Arena((10, 10), (14, 10));
		var s = sim.State;
		Provoke(sim, 0);
		RunTicks(sim, 1);
		// The pursuer cannot be picked as a target by the goblin it hunts, which now acts out too: with nobody else it shirks.
		s.GoblinCarry[1] = (byte)Material.None;
		s.GoblinTask[1] = (byte)GoblinTask.Idle;
		s.GoblinChaos[1] = 100;
		s.Rng = SeedDrawing(1);
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 1));
		Assert.Equal(GoblinTask.Pursuing, TaskOf(s, 0));
	}

	// ---- A scuffle that runs its course

	// Goblin 0 and goblin 1 on one tile, a scuffle begun on tick 1. Goblin 0 starts at 100 Chaos so relief shows.
	static Simulation Scuffle(int first = 0, int second = 1)
	{
		var at = new (int X, int Y)[Math.Max(first, second) + 1];
		Array.Fill(at, (20, 20));
		var sim = Arena(at);
		var s = sim.State;
		// Whichever goblin is first acts; Arena made the others waiting carriers, so goblin 0 is made one as needed.
		for (var g = 0; g < at.Length; g++)
			if (g != first)
				Wait(s, g);
		s.GoblinTask[first] = (byte)GoblinTask.Idle;
		s.GoblinCarry[first] = (byte)Material.None;
		s.GoblinChaos[first] = 100;
		s.Rng = SeedDrawing(1);
		sim.RecheckReachability();
		return sim;
	}

	[Fact]
	public void A_scuffle_lasts_30_ticks_then_the_instigator_alone_is_relieved_of_40_Chaos()
	{
		var sim = Scuffle();
		var s = sim.State;

		RunTicks(sim, 30);
		// Contact on tick 1; ticks 1 to 30 are the thirty ticks of scuffling.
		Assert.Equal(GoblinTask.Scuffling, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Scuffled, TaskOf(s, 1));
		// Three content intervals have faded it so far, with no relief yet.
		Assert.Equal(97, s.GoblinChaos[0]);
		var victim = s.GoblinChaos[1];

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Recovering, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Recovering, TaskOf(s, 1));
		// Three content intervals fell on the way before the 40 of relief: 100 - 3 - 40.
		Assert.Equal(57, s.GoblinChaos[0]);
		Assert.Equal(victim, s.GoblinChaos[1]);
		Assert.Equal((1, 0), (s.GoblinFightPartner[0], s.GoblinFightPartner[1]));
	}

	[Fact]
	public void Both_fighters_start_100_ticks_of_recovery_and_a_300_tick_cooldown_together_the_moment_it_ends()
	{
		var sim = Scuffle();
		var s = sim.State;

		RunTicks(sim, 31);

		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[0]);
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[1]);
		Assert.Equal(ChaosRules.RecoveryTicks, s.GoblinFightTicks[0]);
		Assert.Equal(ChaosRules.RecoveryTicks, s.GoblinFightTicks[1]);

		// Recovery is the next 100 ticks, then work; the cooldown outlasts it by 200.
		RunTicks(sim, 99);
		Assert.Equal(GoblinTask.Recovering, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Recovering, TaskOf(s, 1));
		RunTicks(sim, 1);
		Assert.NotEqual(GoblinTask.Recovering, TaskOf(s, 0));
		Assert.NotEqual(GoblinTask.Recovering, TaskOf(s, 1));
		Assert.Equal(200, s.GoblinActCooldown[0]);
		Assert.Equal(200, s.GoblinActCooldown[1]);
	}

	[Fact]
	public void Fighters_take_no_work_and_do_not_wander_while_recovering_then_take_it_at_once()
	{
		var sim = Scuffle();
		var s = sim.State;
		RunTicks(sim, 31);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 20, 18, 20, 18));

		RunTicks(sim, 99);
		Assert.Equal(GoblinTask.Recovering, TaskOf(s, 0));
		Assert.Equal((20, 20), (s.GoblinX[0], s.GoblinY[0]));
		Assert.Equal(-1, s.GoblinJobTile[0]);

		RunTicks(sim, 1);
		// Goblin 0 is first in line for the dig, and goblin 1 is let go in the same tick.
		Assert.Equal(GoblinTask.Walking, TaskOf(s, 0));
	}

	[Fact]
	public void The_scuffle_ends_on_the_same_tick_whichever_fighter_has_the_lower_index()
	{
		foreach (var (instigator, victim) in new[] { (0, 1), (1, 0) })
		{
			var sim = Scuffle(first: instigator, second: victim);
			var s = sim.State;

			RunTicks(sim, 30);
			Assert.Equal(GoblinTask.Scuffling, TaskOf(s, instigator));
			RunTicks(sim, 1);

			Assert.Equal(GoblinTask.Recovering, TaskOf(s, instigator));
			Assert.Equal(GoblinTask.Recovering, TaskOf(s, victim));
			Assert.Equal(ChaosRules.RecoveryTicks, s.GoblinFightTicks[instigator]);
			Assert.Equal(ChaosRules.RecoveryTicks, s.GoblinFightTicks[victim]);
			RunTicks(sim, 100);
			Assert.NotEqual(GoblinTask.Recovering, TaskOf(s, instigator));
			Assert.NotEqual(GoblinTask.Recovering, TaskOf(s, victim));
		}
	}

	// ---- A scuffle that is interrupted

	static void EndsForBoth(Simulation sim, int interrupted, int other, GoblinTask expected)
	{
		var s = sim.State;
		Assert.Equal(expected, TaskOf(s, interrupted));
		Assert.Equal(GoblinTask.Idle, TaskOf(s, other));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[interrupted]);
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[other]);
		Assert.Equal((-1, -1), (s.GoblinFightPartner[interrupted], s.GoblinFightPartner[other]));
		Assert.Equal((0, 0), (s.GoblinFightTicks[interrupted], s.GoblinFightTicks[other]));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void A_satisfiable_Craving_in_either_fighter_ends_the_scuffle_for_both_with_no_relief_and_no_recovery(bool instigatorCraves)
	{
		var sim = Scuffle();
		var s = sim.State;
		sim.State.AddItems(At(s, 20, 22), Material.Grub, 1);
		sim.RecheckReachability();
		RunTicks(sim, 10);
		var victimChaos = s.GoblinChaos[1];
		var instigatorChaos = s.GoblinChaos[0];
		Assert.Equal(ChaosRules.VictimGain - 1, victimChaos);

		var craver = instigatorCraves ? 0 : 1;
		s.GoblinGrub[craver] = 20;
		RunTicks(sim, 1);

		EndsForBoth(sim, craver, 1 - craver, GoblinTask.Seeking);
		// The victim keeps what it gained and the instigator gets no relief.
		Assert.Equal(victimChaos, s.GoblinChaos[1]);
		Assert.Equal(instigatorChaos, s.GoblinChaos[0]);
		// The scuffle never resumes and neither fighter recovers.
		RunTicks(sim, 200);
		Assert.DoesNotContain(Enumerable.Range(0, 2), g => TaskOf(s, g) == GoblinTask.Recovering);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Collapse_in_either_fighter_ends_the_scuffle_for_both(bool instigatorCollapses)
	{
		var sim = Scuffle();
		var s = sim.State;
		RunTicks(sim, 10);

		var collapser = instigatorCollapses ? 0 : 1;
		s.GoblinSleep[collapser] = 0;
		RunTicks(sim, 1);

		EndsForBoth(sim, collapser, 1 - collapser, GoblinTask.Sleeping);
		Assert.Equal(ChaosRules.VictimGain - 1, s.GoblinChaos[1]);
	}

	[Fact]
	public void An_unsatisfiable_Craving_does_not_end_a_scuffle()
	{
		var sim = Scuffle();
		var s = sim.State;
		s.GoblinGrub[0] = 20;
		s.GoblinGrub[1] = 20;

		RunTicks(sim, 30);

		Assert.Equal(GoblinTask.Scuffling, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Scuffled, TaskOf(s, 1));
	}

	[Fact]
	public void A_Craving_interrupting_the_pursuit_starts_the_cooldown_of_the_instigator_alone()
	{
		var sim = Fight((10, 10), (14, 10));
		var s = sim.State;
		sim.State.AddItems(At(s, 10, 14), Material.Grub, 1);
		sim.RecheckReachability();
		RunTicks(sim, 4);
		s.GoblinGrub[0] = 20;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 0));
		Assert.Equal(ChaosRules.CooldownTicks, s.GoblinActCooldown[0]);
		Assert.Equal(0, s.GoblinActCooldown[1]);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 1));
		Assert.Equal(0, s.GoblinChaos[1]);
	}

	// ---- A Need that arises on the scuffle's last tick

	// A scuffle that begins on tick endTick - 30 and so reaches its last tick, the tick that would end it, on endTick. The sim's tick counter is
	// moved, not the Needs: each Need drains on its own fixed ticks, so the boundary is reached by running the sim.
	static Simulation ScuffleEndingAt(long endTick, int instigator, int victim)
	{
		var sim = Scuffle(instigator, victim);
		sim.State.Tick = endTick - 31;
		return sim;
	}

	public enum LastTickNeed
	{
		Sleep,
		Grub,
		Grog,
		Collapse,
	}

	// The tick each Need's meter is taken below its threshold on by the drain itself (Collapse is made by the test's own hand, on the last tick too).
	static long LastTickOf(LastTickNeed need) => need switch
	{
		LastTickNeed.Grub => 50,
		LastTickNeed.Grog => 70,
		_ => 45,
	};

	// Gives the goblin a meter that the drain on the scuffle's last tick takes below the Craving threshold, with a unit within reach to meet it.
	static void ArmLastTickNeed(Simulation sim, LastTickNeed need, int craver)
	{
		var s = sim.State;
		s.AddItems(At(s, 20, 22), Material.Grub, 1);
		s.AddItems(At(s, 20, 22), Material.Grog, 1);
		sim.RecheckReachability();
		switch (need)
		{
			case LastTickNeed.Sleep:
				s.GoblinSleep[craver] = 30; // Sleep drains on tick 45, and on no other in the scuffle
				break;
			case LastTickNeed.Grub:
				s.GoblinGrub[craver] = 31; // Grub drains on ticks 25 and 50: 30 for the scuffle, 29 on its last tick
				break;
			case LastTickNeed.Grog:
				s.GoblinGrog[craver] = 30; // Grog drains on tick 70, and on no other in the scuffle
				break;
		}
	}

	[Theory]
	[InlineData(0, true, LastTickNeed.Sleep)]
	[InlineData(0, false, LastTickNeed.Sleep)]
	[InlineData(1, true, LastTickNeed.Sleep)]
	[InlineData(1, false, LastTickNeed.Sleep)]
	[InlineData(0, true, LastTickNeed.Grub)]
	[InlineData(1, false, LastTickNeed.Grub)]
	[InlineData(0, false, LastTickNeed.Grog)]
	[InlineData(1, true, LastTickNeed.Grog)]
	[InlineData(0, true, LastTickNeed.Collapse)]
	[InlineData(0, false, LastTickNeed.Collapse)]
	[InlineData(1, true, LastTickNeed.Collapse)]
	[InlineData(1, false, LastTickNeed.Collapse)]
	public void A_Need_that_arises_on_the_last_tick_of_a_scuffle_ends_it_for_both_with_no_relief_and_no_recovery(int instigator, bool instigatorCraves, LastTickNeed need)
	{
		var victim = 1 - instigator;
		var craver = instigatorCraves ? instigator : victim;
		var endTick = LastTickOf(need);
		var sim = ScuffleEndingAt(endTick, instigator, victim);
		ArmLastTickNeed(sim, need, craver);
		var s = sim.State;

		// The scuffle is on, with one tick still to run, and the Need has not yet arisen.
		RunTicks(sim, 30);
		Assert.Equal(endTick - 1, s.Tick);
		Assert.Equal(GoblinTask.Scuffling, TaskOf(s, instigator));
		Assert.Equal(GoblinTask.Scuffled, TaskOf(s, victim));
		Assert.Equal(1, s.GoblinFightTicks[instigator]);
		Assert.Equal(1, s.GoblinFightTicks[victim]);
		if (need == LastTickNeed.Collapse)
			s.GoblinSleep[craver] = 0;
		else
			Assert.False(NeedRules.IsCraving(need switch { LastTickNeed.Sleep => s.GoblinSleep[craver], LastTickNeed.Grub => s.GoblinGrub[craver], _ => s.GoblinGrog[craver] }));
		var victimChaos = s.GoblinChaos[victim];
		var instigatorChaos = s.GoblinChaos[instigator];
		// The +15 has faded a point on each content interval since, and nowhere else.
		Assert.InRange(victimChaos, ChaosRules.VictimGain - 4, ChaosRules.VictimGain);

		// The save taken on that last-but-one tick must carry on exactly as the live run does.
		var restored = Restore(sim);
		RunTicks(sim, 1);
		RunTicks(restored, 1);
		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));

		foreach (var run in new[] { sim, restored })
		{
			var r = run.State;
			var expected = need switch
			{
				LastTickNeed.Grub => GoblinTask.Seeking,
				LastTickNeed.Grog => GoblinTask.SeekingGrog,
				_ => GoblinTask.Sleeping,
			};
			EndsForBoth(run, craver, 1 - craver, expected);
			// No relief for the instigator, none of the victim's +15 taken back, and no recovery for anyone.
			Assert.InRange(r.GoblinChaos[instigator], instigatorChaos - 1, 100);
			Assert.InRange(r.GoblinChaos[victim], victimChaos - 1, 100);
			RunTicks(run, 150);
			Assert.DoesNotContain(Enumerable.Range(0, 2), g => TaskOf(r, g) == GoblinTask.Recovering);
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public void Both_fighters_in_Need_on_the_last_tick_of_a_scuffle_each_set_out_to_meet_it_with_no_relief_and_no_recovery(int instigator)
	{
		var victim = 1 - instigator;
		var sim = ScuffleEndingAt(45, instigator, victim);
		var s = sim.State;
		s.GoblinSleep[0] = 30;
		s.GoblinSleep[1] = 30;
		RunTicks(sim, 30);
		var instigatorChaos = s.GoblinChaos[instigator];

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 0));
		Assert.Equal(GoblinTask.Sleeping, TaskOf(s, 1));
		Assert.Equal((ChaosRules.CooldownTicks, ChaosRules.CooldownTicks), (s.GoblinActCooldown[0], s.GoblinActCooldown[1]));
		Assert.InRange(s.GoblinChaos[instigator], instigatorChaos - 1, 100);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public void A_Craving_nothing_can_meet_on_the_last_tick_of_a_scuffle_does_not_stop_it_finishing_with_relief_and_recovery(int instigator)
	{
		var victim = 1 - instigator;
		var sim = ScuffleEndingAt(50, instigator, victim);
		var s = sim.State;
		// No Grub anywhere: the Craving that arises on tick 50 cannot be met.
		s.GoblinGrub[victim] = 31;
		s.GoblinGrub[instigator] = 31;
		RunTicks(sim, 30);
		var instigatorChaos = s.GoblinChaos[instigator];
		var victimChaos = s.GoblinChaos[victim];

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Recovering, TaskOf(s, instigator));
		Assert.Equal(GoblinTask.Recovering, TaskOf(s, victim));
		Assert.InRange(s.GoblinChaos[instigator], 0, instigatorChaos - ChaosRules.ActRelief + 5);
		Assert.True(s.GoblinChaos[victim] >= victimChaos - 1);
		Assert.Equal((ChaosRules.RecoveryTicks, ChaosRules.RecoveryTicks), (s.GoblinFightTicks[0], s.GoblinFightTicks[1]));
	}

	// ---- Recovery interrupted

	[Fact]
	public void A_Need_interrupts_recovery_without_resuming_it_or_restarting_the_cooldown_and_the_partner_rests_on()
	{
		var sim = Scuffle();
		var s = sim.State;
		RunTicks(sim, 31 + 20);
		sim.State.AddItems(At(s, 20, 22), Material.Grub, 1);
		sim.RecheckReachability();
		Assert.Equal(280, s.GoblinActCooldown[1]);
		s.GoblinGrub[1] = 20;

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Seeking, TaskOf(s, 1));
		Assert.Equal(279, s.GoblinActCooldown[1]);
		Assert.Equal(-1, s.GoblinFightPartner[1]);
		Assert.Equal(GoblinTask.Recovering, TaskOf(s, 0));
		Assert.Equal(1, s.GoblinFightPartner[0]);

		RunTicks(sim, 60);
		Assert.NotEqual(GoblinTask.Recovering, TaskOf(s, 1));
		Assert.Equal(GoblinTask.Recovering, TaskOf(s, 0));
		Assert.True(s.GoblinActCooldown[1] < 280);
	}

	// ---- Cooldown

	[Fact]
	public void Fighters_in_their_cooldown_do_not_start_another_fight_and_cannot_be_targeted_though_Chaos_still_rises()
	{
		var sim = Scuffle();
		var s = sim.State;
		RunTicks(sim, 31 + 100);
		Assert.Equal(200, s.GoblinActCooldown[0]);
		s.GoblinChaos[0] = 100;
		s.GoblinChaos[1] = 100;
		s.GoblinGrog[0] = 10;

		RunTicks(sim, 150);

		// Neither acts out despite 100 Chaos, and the Chaos it holds is not spent.
		Assert.DoesNotContain(Enumerable.Range(0, 2), g => GoblinTasks.IsActingOutTask(TaskOf(s, g)));
		Assert.True(s.GoblinChaos[0] >= 99);
		Assert.True(s.GoblinActCooldown[0] > 0);
	}

	[Fact]
	public void A_third_goblin_at_70_Chaos_skips_a_fighter_in_its_cooldown_even_when_it_is_the_nearest()
	{
		var sim = Scuffle();
		var s = sim.State;
		s.GoblinCount = 3;
		s.GoblinX[2] = 30;
		s.GoblinY[2] = 30;
		Wait(s, 2);
		RunTicks(sim, 31 + 100);
		Assert.True(s.GoblinActCooldown[1] > 0);
		s.GoblinChaos[2] = 70;
		s.GoblinTask[2] = (byte)GoblinTask.Idle;
		s.GoblinCarry[2] = (byte)Material.None;
		s.Rng = SeedDrawing(1);
		sim.RecheckReachability();

		RunTicks(sim, 1);

		// Both rested goblins are in cooldown, so goblin 2 has nobody to fight and shirks.
		Assert.Equal(GoblinTask.Shirking, TaskOf(s, 2));
	}

	// ---- Starvation

	[Fact]
	public void Starvation_slows_the_walk_of_a_pursuit_to_half_pace()
	{
		var sim = Fight((10, 10), (14, 10));
		var s = sim.State;
		s.GoblinGrub[0] = 0;

		// Three steps at six ticks each, instead of three ticks each.
		RunTicks(sim, 17);
		Assert.Equal(GoblinTask.Pursuing, TaskOf(s, 0));
		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Scuffling, TaskOf(s, 0));
		Assert.Equal((13, 10), (s.GoblinX[0], s.GoblinY[0]));
	}

	[Fact]
	public void Starvation_does_not_stretch_the_pursuit_the_scuffle_or_the_recovery()
	{
		var timeout = Fight((0, 0), (31, 31));
		timeout.State.GoblinGrub[0] = 0;
		RunTicks(timeout, 100);
		Assert.Equal(GoblinTask.Pursuing, TaskOf(timeout.State, 0));
		RunTicks(timeout, 1);
		Assert.NotEqual(GoblinTask.Pursuing, TaskOf(timeout.State, 0));

		var sim = Scuffle();
		var s = sim.State;
		s.GoblinGrub[0] = 0;
		s.GoblinGrub[1] = 0;
		RunTicks(sim, 30);
		Assert.Equal(GoblinTask.Scuffling, TaskOf(s, 0));
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Recovering, TaskOf(s, 0));
		RunTicks(sim, 99);
		Assert.Equal(GoblinTask.Recovering, TaskOf(s, 1));
		RunTicks(sim, 1);
		Assert.NotEqual(GoblinTask.Recovering, TaskOf(s, 0));
		Assert.NotEqual(GoblinTask.Recovering, TaskOf(s, 1));
	}

	// ---- Fights carrying material

	[Fact]
	public void A_pursuer_carrying_a_unit_puts_it_down_before_it_sets_out()
	{
		var sim = Arena((10, 10), (14, 10));
		var s = sim.State;
		s.GoblinTask[0] = (byte)GoblinTask.Carrying;
		s.GoblinCarry[0] = (byte)Material.Dirt;
		Provoke(sim, 0);

		RunTicks(sim, 1);

		Assert.Equal(GoblinTask.Pursuing, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
		Assert.Equal(1, s.ItemCount(At(s, 10, 10), Material.Dirt));
	}

	[Fact]
	public void A_target_that_loses_its_last_legal_place_to_put_its_unit_down_before_contact_fails_the_attempt()
	{
		var sim = Fight((14, 16), (17, 16));
		var s = sim.State;
		// A corridor of four tiles; the pursuer's own tile is the only one that is not a Pile, so the target's unit has somewhere to go.
		for (var tile = 0; tile < s.Width * s.Height; tile++)
			if (tile / s.Width != 16 || tile % s.Width < 14 || tile % s.Width > 17)
				s.TileKinds[tile] = (byte)TileKind.Solid;
		foreach (var x in new[] { 15, 16, 17 })
		{
			s.AddItems(At(s, x, 16), Material.Dirt, SimState.PileCapacity);
			s.Piles[At(s, x, 16)] = 1;
		}
		sim.RecheckReachability();
		RunTicks(sim, 1);
		Assert.Equal(GoblinTask.Pursuing, TaskOf(s, 0));

		// The last free tile fills.
		s.AddItems(At(s, 14, 16), Material.Dirt, SimState.PileCapacity);
		s.Piles[At(s, 14, 16)] = 1;
		sim.RecheckReachability();
		RunTicks(sim, 10);

		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		// The attempt failed on contact, the sixth tick of the walk, so five ticks of cooldown have gone by since.
		Assert.Equal(ChaosRules.CooldownTicks - 5, s.GoblinActCooldown[0]);
		Assert.Equal(GoblinTask.Carrying, TaskOf(s, 1));
		Assert.Equal((byte)Material.Dirt, s.GoblinCarry[1]);
		Assert.Equal(-1, s.GoblinFightPartner[1]);
		Assert.Empty(sim.DrainEvents().OfType<ScuffleStarted>());
	}

	// ---- Saves and replay

	static Simulation Restore(Simulation sim)
	{
		var json = SimJson.Serialize(sim);
		var restored = SimJson.Deserialize(json);
		Assert.Equal(json, SimJson.Serialize(restored));
		return restored;
	}

	[Theory]
	[InlineData(3, GoblinTask.Pursuing)]
	[InlineData(15, GoblinTask.Scuffling)]
	[InlineData(60, GoblinTask.Recovering)]
	public void A_save_taken_mid_pursuit_scuffle_or_recovery_continues_identically(int ticks, GoblinTask expected)
	{
		// Goblin 1 is 6 tiles away, so the pursuit lasts until tick 13: the three phases are all reached by tick 60 for the pair.
		var sim = Fight((10, 10), (11, 10));
		var s = sim.State;
		if (expected == GoblinTask.Pursuing)
		{
			s.GoblinX[1] = 16;
			sim.RecheckReachability();
		}
		RunTicks(sim, ticks);
		Assert.Equal(expected, TaskOf(s, 0));

		var restored = Restore(sim);
		RunTicks(sim, 400);
		RunTicks(restored, 400);

		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
	}

	[Fact]
	public void A_run_replays_exactly_from_a_save_taken_on_any_tick_of_the_fight_whichever_fighter_has_the_lower_index()
	{
		foreach (var (instigator, victim) in new[] { (0, 1), (1, 0) })
		{
			var sim = Arena((10, 10), (13, 10));
			var s = sim.State;
			Wait(s, 0);
			Wait(s, 1);
			s.GoblinTask[instigator] = (byte)GoblinTask.Idle;
			s.GoblinCarry[instigator] = (byte)Material.None;
			s.GoblinChaos[instigator] = 100;
			s.GoblinX[instigator] = 10;
			s.GoblinX[victim] = 13;
			s.Rng = SeedDrawing(1);
			sim.RecheckReachability();

			// One uninterrupted run, kept tick by tick; a save taken on any of those ticks must carry on to the same bytes.
			const int Ticks = 160;
			const int Ahead = 25;
			var saves = new string[Ticks + Ahead + 1];
			saves[0] = SimJson.Serialize(sim);
			for (var tick = 1; tick <= Ticks + Ahead; tick++)
			{
				sim.Advance(0.1);
				saves[tick] = SimJson.Serialize(sim);
			}
			for (var tick = 0; tick <= Ticks; tick++)
			{
				var branch = SimJson.Deserialize(saves[tick]);
				for (var i = 0; i < Ahead; i++)
					branch.Advance(0.1);
				Assert.Equal(saves[tick + Ahead], SimJson.Serialize(branch));
			}
			Assert.True(s.GoblinActCooldown[instigator] > 0);
		}
	}

	[Fact]
	public void Two_runs_of_one_seed_agree_to_the_last_byte_and_a_fight_happens_in_them()
	{
		static (Simulation Sim, int Scuffles) Run(int seed)
		{
			var sim = Simulation.NewWorld(seed);
			Array.Clear(sim.State.TileItems);
			sim.RecheckReachability();
			for (var g = 0; g < sim.State.GoblinCount; g++)
				sim.State.GoblinChaos[g] = (byte)(60 + g * 5);
			var scuffles = 0;
			for (var i = 0; i < 2500; i++)
			{
				sim.Advance(0.1);
				scuffles += sim.DrainEvents().OfType<ScuffleStarted>().Count();
			}
			return (sim, scuffles);
		}

		var (a, scuffles) = Run(9);
		var (b, _) = Run(9);

		Assert.True(scuffles > 0, "No scuffle started, so the run never reached the behaviour it replays.");
		Assert.Equal(SimJson.Serialize(a), SimJson.Serialize(b));
		Assert.NotEqual(SimJson.Serialize(a), SimJson.Serialize(Run(10).Sim));
	}

	// ---- Saves that break the rules of a fight

	static string SaveMid(GoblinTask phase, Action<SimState>? change = null)
	{
		var sim = Fight((10, 10), (16, 10));
		var s = sim.State;
		switch (phase)
		{
			case GoblinTask.Pursuing:
				RunTicks(sim, 2);
				break;
			case GoblinTask.Scuffling:
				sim = Scuffle();
				RunTicks(sim, 5);
				break;
			default:
				sim = Scuffle();
				RunTicks(sim, 40);
				break;
		}
		Assert.Equal(phase, TaskOf(sim.State, 0));
		change?.Invoke(sim.State);
		return SimJson.Serialize(sim);
	}

	static void Rejected(GoblinTask phase, Action<SimState> change) =>
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SaveMid(phase, change)));

	[Fact]
	public void The_saves_the_run_writes_in_each_phase_load()
	{
		SimJson.Deserialize(SaveMid(GoblinTask.Pursuing));
		SimJson.Deserialize(SaveMid(GoblinTask.Scuffling));
		SimJson.Deserialize(SaveMid(GoblinTask.Recovering));
	}

	[Fact]
	public void A_save_with_a_pursuer_that_breaks_the_rules_is_rejected()
	{
		Rejected(GoblinTask.Pursuing, s => s.GoblinFightPartner[0] = 0);
		Rejected(GoblinTask.Pursuing, s => s.GoblinFightPartner[0] = -1);
		Rejected(GoblinTask.Pursuing, s => s.GoblinFightPartner[0] = 9);
		Rejected(GoblinTask.Pursuing, s => s.GoblinFightTicks[0] = 0);
		Rejected(GoblinTask.Pursuing, s => s.GoblinFightTicks[0] = ChaosRules.PursuitTicks + 1);
		Rejected(GoblinTask.Pursuing, s => s.GoblinActCooldown[0] = 5);
		Rejected(GoblinTask.Pursuing, s => s.GoblinJobTile[0] = 5);
		Rejected(GoblinTask.Pursuing, s => s.GoblinCarry[0] = (byte)Material.Dirt);
	}

	[Fact]
	public void A_save_with_a_scuffle_that_is_not_a_whole_pair_is_rejected()
	{
		Rejected(GoblinTask.Scuffling, s => s.GoblinFightPartner[1] = -1);
		Rejected(GoblinTask.Scuffling, s => s.GoblinTask[1] = (byte)GoblinTask.Scuffling);
		Rejected(GoblinTask.Scuffling, s => s.GoblinTask[1] = (byte)GoblinTask.Idle);
		Rejected(GoblinTask.Scuffling, s => s.GoblinFightTicks[1] = (ushort)(s.GoblinFightTicks[1] - 1));
		Rejected(GoblinTask.Scuffling, s => s.GoblinFightTicks[0] = ChaosRules.ScuffleTicks + 1);
		Rejected(GoblinTask.Scuffling, s => s.GoblinX[1] = 22);
		Rejected(GoblinTask.Scuffling, s => s.GoblinZ[1] = 1);
		Rejected(GoblinTask.Scuffling, s => s.GoblinActCooldown[1] = 5);
		Rejected(GoblinTask.Scuffling, s => s.GoblinFightPartner[0] = 0);
	}

	[Fact]
	public void A_save_with_a_recovery_that_breaks_the_rules_is_rejected()
	{
		Rejected(GoblinTask.Recovering, s => s.GoblinFightPartner[0] = -1);
		Rejected(GoblinTask.Recovering, s => s.GoblinFightPartner[0] = 0);
		Rejected(GoblinTask.Recovering, s => s.GoblinFightTicks[0] = 0);
		Rejected(GoblinTask.Recovering, s => s.GoblinFightTicks[0] = ChaosRules.RecoveryTicks + 1);
		// A recovery that outlasts its cooldown.
		Rejected(GoblinTask.Recovering, s => s.GoblinActCooldown[0] = 1);
	}

	[Fact]
	public void A_save_with_fight_fields_on_a_goblin_in_no_fight_is_rejected()
	{
		Rejected(GoblinTask.Pursuing, s => s.GoblinFightTicks[1] = 5);
		Rejected(GoblinTask.Pursuing, s => s.GoblinFightPartner[1] = 0);
		Rejected(GoblinTask.Pursuing, s => { s.GoblinTask[0] = (byte)GoblinTask.Idle; });
	}

	[Fact]
	public void A_save_without_the_fight_arrays_or_with_the_wrong_number_of_them_is_rejected()
	{
		Rejected(GoblinTask.Pursuing, s => s.GoblinFightPartner = new int[3]);
		Rejected(GoblinTask.Pursuing, s => s.GoblinFightTicks = new ushort[3]);
		foreach (var name in new[] { "GoblinFightPartner", "GoblinFightTicks" })
		{
			var root = JsonNode.Parse(SaveMid(GoblinTask.Pursuing))!.AsObject();
			root.Remove(name);
			Assert.Throws<JsonException>(() => SimJson.Deserialize(root.ToJsonString()));
		}
	}
}
