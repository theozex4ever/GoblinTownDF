using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

public class ReachabilityTests
{
	// One tick of wall time at 1×.
	const double OneTick = 0.1;

	static Designate DigStairsAt(int x, int y, int z) => new(DesignationKind.DigStairs, z, x, y, x, y);

	/// <summary>Counts the reachability floods, so a test can tell when the sim rechecked.</summary>
	sealed class CountingPathfinder : IPathfinder
	{
		readonly GridPathfinder _inner = new();

		public int Floods { get; private set; }

		// Every tile a claim or walk ever asked for a path to.
		public HashSet<int> GoalsSearched { get; } = [];

		public bool TryFindPath(SimState state, int start, ReadOnlySpan<int> goals, List<int> path, out int goalIndex)
		{
			foreach (var goal in goals)
				GoalsSearched.Add(goal);
			return _inner.TryFindPath(state, start, goals, path, out goalIndex);
		}

		public void FloodReachable(SimState state, ReadOnlySpan<int> starts, byte[] reached)
		{
			Floods++;
			_inner.FloodReachable(state, starts, reached);
		}
	}

	[Fact]
	public void An_unreachable_designation_is_never_claimed_and_the_goblin_takes_a_reachable_one_instead()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var waiting = s.TileIndex(16, 15, 1);
		var open = s.TileIndex(16, 2, 0);
		// The waiting tile is right under the goblin but has no Stairs above it; the open one is 13 steps off.
		sim.Apply(DigStairsAt(16, 15, 1));
		sim.Apply(DigStairsAt(16, 2, 0));

		Assert.False(s.IsReachable(waiting));
		Assert.True(s.IsReachable(open));

		RunTicks(sim, 1);
		Assert.Equal(open, s.GoblinJobTile[0]);
		for (var i = 0; i < 100; i++)
		{
			RunTicks(sim, 1);
			Assert.NotEqual(waiting, s.GoblinJobTile[0]);
		}
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(waiting));
		Assert.False(s.IsReachable(waiting));
	}

	[Fact]
	public void A_designation_with_a_cut_off_stand_tile_never_reaches_the_path_search()
	{
		var counting = new CountingPathfinder();
		var sim = new Simulation(Simulation.NewWorld(seed: 1).State, counting);
		var s = sim.State;
		// A Floor tile on z1 walled in by Solid: a place to dig the waiting tile from, but no goblin can get
		// there. Without the reachability filter it would be offered to the path search as a goal.
		var island = s.TileIndex(16, 14, 1);
		var waiting = s.TileIndex(16, 13, 1);
		var open = s.TileIndex(16, 2, 0);
		s.TileKinds[island] = (byte)TileKind.Floor;
		sim.Apply(DigStairsAt(16, 13, 1));
		sim.Apply(DigStairsAt(16, 2, 0));
		Assert.False(s.IsReachable(waiting));

		RunTicks(sim, 100);

		Assert.Equal(TileKind.Stairs, s.KindAt(open));
		Assert.DoesNotContain(island, counting.GoalsSearched);
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(waiting));
	}

	[Fact]
	public void A_cleared_designation_is_never_reachable_even_before_a_recheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var near = s.TileIndex(16, 15, 0);
		var far = s.TileIndex(20, 20, 0);
		sim.Apply(DigStairsAt(16, 15, 0));
		sim.Apply(DigStairsAt(20, 20, 0));

		// What a cancel that forgets the hook does: clear the Designation and leave the flag stale.
		s.Designations[near] = (byte)DesignationKind.None;
		Assert.False(s.IsReachable(near));

		RunTicks(sim, 7 * 3 + 20);

		Assert.Equal(TileKind.Stairs, s.KindAt(far));
		Assert.Equal(TileKind.Floor, s.KindAt(near));
	}

	[Fact]
	public void Reachable_is_only_ever_set_on_designated_tiles()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;

		Assert.False(s.IsReachable(s.TileIndex(16, 15, 0)));
		sim.Apply(DigStairsAt(16, 15, 0));
		Assert.True(s.IsReachable(s.TileIndex(16, 15, 0)));

		RunTicks(sim, 20);
		Assert.Equal(DesignationKind.None, s.DesignationAt(s.TileIndex(16, 15, 0)));
		Assert.False(s.IsReachable(s.TileIndex(16, 15, 0)));
	}

	[Fact]
	public void A_waiting_designation_becomes_reachable_once_a_dug_tile_opens_a_path_and_then_gets_dug()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		var top = s.TileIndex(16, 15, 0);
		var middle = s.TileIndex(16, 15, 1);
		var bottom = s.TileIndex(16, 15, 2);
		// Designate down the column before any of it is dug: only the z0 tile can be reached.
		sim.Apply(DigStairsAt(16, 15, 2));
		sim.Apply(DigStairsAt(16, 15, 1));
		sim.Apply(DigStairsAt(16, 15, 0));
		Assert.True(s.IsReachable(top));
		Assert.False(s.IsReachable(middle));
		Assert.False(s.IsReachable(bottom));

		// The z0 Stairs finish on tick 20 and open the way to z1.
		RunTicks(sim, 19);
		Assert.False(s.IsReachable(middle));
		RunTicks(sim, 1);
		Assert.Equal(TileKind.Stairs, s.KindAt(top));
		Assert.True(s.IsReachable(middle));
		Assert.False(s.IsReachable(bottom));

		// The z1 Stairs take 20 more dig ticks after one step, and open the way to z2.
		RunTicks(sim, 3 + 20);
		Assert.Equal(TileKind.Stairs, s.KindAt(middle));
		Assert.True(s.IsReachable(bottom));

		// One step down onto the z1 Stairs, then 40 dig ticks from above: z2 is stone.
		RunTicks(sim, 3 + 40);
		Assert.Equal(TileKind.Stairs, s.KindAt(bottom));
		Assert.Equal(DesignationKind.None, s.DesignationAt(bottom));
		Assert.Equal(
			new SimEvent[]
			{
				new TileDug(16, 15, 0, Material.Dirt),
				new TileDug(16, 15, 1, Material.Dirt),
				new TileDug(16, 15, 2, Material.Stone),
			},
			sim.DrainEvents());
	}

	[Fact]
	public void Reachability_is_not_rechecked_on_ticks_where_no_tile_was_dug()
	{
		var counting = new CountingPathfinder();
		var sim = new Simulation(Simulation.NewWorld(seed: 1).State, counting);
		var s = sim.State;
		var floodsAtStart = counting.Floods;

		// Idle ticks with nothing designated.
		RunTicks(sim, 10);
		Assert.Equal(floodsAtStart, counting.Floods);

		// A designation that waits, and one the goblin walks to and digs in place.
		sim.Apply(DigStairsAt(5, 5, 2));
		sim.Apply(DigStairsAt(16, 15, 0));
		var floods = counting.Floods;

		RunTicks(sim, 19);
		Assert.Equal(floods, counting.Floods);
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(s.TileIndex(16, 15, 0)));

		// The tick that digs a tile rechecks, once.
		RunTicks(sim, 1);
		Assert.Equal(TileKind.Stairs, s.KindAt(s.TileIndex(16, 15, 0)));
		Assert.Equal(floods + 1, counting.Floods);

		// With only the waiting designation left, no tick rechecks.
		RunTicks(sim, 50);
		Assert.Equal(floods + 1, counting.Floods);
	}

	[Fact]
	public void The_recheck_hook_catches_up_after_tiles_change_outside_a_dig()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var waiting = s.TileIndex(16, 15, 1);
		sim.Apply(DigStairsAt(16, 15, 1));
		Assert.False(s.IsReachable(waiting));

		// What cancelling or a new designation will do: change tiles, then call the one hook.
		s.TileKinds[s.TileIndex(16, 15, 0)] = (byte)TileKind.Stairs;
		sim.RecheckReachability();

		Assert.True(s.IsReachable(waiting));
	}

	[Fact]
	public void Reachability_is_not_written_to_the_save_and_comes_back_the_same_on_load()
	{
		var original = Simulation.NewWorld(seed: 7);
		original.Apply(DigStairsAt(16, 15, 0));
		original.Apply(DigStairsAt(16, 15, 1));
		original.Apply(DigStairsAt(5, 5, 2));
		RunTicks(original, 12);

		var json = SimJson.Serialize(original);
		var restored = SimJson.Deserialize(json);

		Assert.DoesNotContain("Reachable", json);
		Assert.Equal(original.State.Reachable, restored.State.Reachable);
		Assert.Contains(original.State.Reachable, flag => flag != 0);
	}

	[Fact]
	public void A_world_saved_with_a_waiting_designation_lights_it_up_when_the_path_opens_exactly_as_the_original_does()
	{
		var original = Simulation.NewWorld(seed: 5);
		original.Apply(DigStairsAt(16, 15, 0));
		original.Apply(DigStairsAt(16, 15, 1));
		RunTicks(original, 10);
		var restored = SimJson.Deserialize(SimJson.Serialize(original));

		for (var i = 0; i < 80; i++)
		{
			RunTicks(original, 1);
			RunTicks(restored, 1);
			Assert.Equal(original.State.Reachable, restored.State.Reachable);
		}

		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));
		Assert.Equal(TileKind.Stairs, restored.State.KindAt(restored.State.TileIndex(16, 15, 1)));
	}

	[Fact]
	public void A_world_saved_between_a_designation_and_the_next_tick_shows_the_same_reachability_as_the_original()
	{
		var original = Simulation.NewWorld(seed: 5);
		original.Apply(DigStairsAt(16, 15, 0));
		original.Apply(DigStairsAt(16, 15, 1));

		var restored = SimJson.Deserialize(SimJson.Serialize(original));

		Assert.Equal(original.State.Reachable, restored.State.Reachable);
	}

	[Fact]
	public void Replaying_a_seed_and_command_log_gives_identical_reachability()
	{
		var log = new (int Tick, Command Command)[]
		{
			(0, DigStairsAt(16, 14, 2)),
			(0, DigStairsAt(16, 14, 1)),
			(3, DigStairsAt(16, 14, 0)),
			(60, new Designate(DesignationKind.DigStairs, 1, 10, 10, 12, 11)),
		};

		var first = Replay(seed: 3, log, ticks: 300);
		var second = Replay(seed: 3, log, ticks: 300);

		Assert.Equal(first.State.Reachable, second.State.Reachable);
		Assert.Equal(SimJson.Serialize(first), SimJson.Serialize(second));
	}

	static Simulation Replay(int seed, (int Tick, Command Command)[] log, int ticks)
	{
		var sim = Simulation.NewWorld(seed);
		var next = 0;
		for (var tick = 0; tick < ticks; tick++)
		{
			while (next < log.Length && log[next].Tick == tick)
				sim.Apply(log[next++].Command);
			sim.Advance(OneTick);
		}
		return sim;
	}
}
