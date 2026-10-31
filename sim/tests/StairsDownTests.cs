using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

public class StairsDownTests
{
	const double OneTick = 0.1;

	// Runs until the goblin has nothing left to do, so a test does not hard-code walk times.
	static void RunUntilIdle(Simulation sim, int limit = 500)
	{
		for (var i = 0; i < limit; i++)
		{
			sim.Advance(OneTick);
			if ((GoblinTask)sim.State.GoblinTask[0] == GoblinTask.Idle)
				return;
		}
		Assert.Fail("The goblin never finished.");
	}

	static void DigStairsAndWait(Simulation sim, int x, int y, int z)
	{
		sim.Apply(new Designate(DesignationKind.DigStairs, z, x, y, x, y));
		RunUntilIdle(sim);
	}

	static List<int> PathBetween(SimState s, int from, int to)
	{
		var path = new List<int>();
		Assert.True(new GridPathfinder().TryFindPath(s, from, [to], path, out _));
		return path;
	}

	[Fact]
	public void Dig_stairs_on_z1_below_z0_Stairs_is_dug_from_the_Stairs_above_and_then_links_the_levels()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		DigStairsAndWait(sim, 16, 15, 0);
		sim.DrainEvents();

		DigStairsAndWait(sim, 16, 15, 1);

		var upper = s.TileIndex(16, 15, 0);
		var lower = s.TileIndex(16, 15, 1);
		Assert.Equal(TileKind.Stairs, s.KindAt(upper));
		Assert.Equal(TileKind.Stairs, s.KindAt(lower));
		Assert.Equal(new SimEvent[] { new TileDug(16, 15, 1, Material.Dirt) }, sim.DrainEvents());
		// The goblin dug it from the Stairs above and never left them.
		Assert.Equal((16, 15, 0), (s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]));
		// Now a tile on z1 is on a path from the surface, and back.
		Assert.Equal([upper, lower], PathBetween(s, s.TileIndex(16, 16, 0), lower));
		Assert.Equal([upper, s.TileIndex(16, 16, 0)], PathBetween(s, lower, s.TileIndex(16, 16, 0)));
	}

	[Fact]
	public void Dig_stairs_on_z2_below_z1_Stairs_is_dug_from_the_Stairs_above_and_then_links_the_levels()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		DigStairsAndWait(sim, 16, 15, 0);
		DigStairsAndWait(sim, 16, 15, 1);
		sim.DrainEvents();

		DigStairsAndWait(sim, 16, 15, 2);

		var z0 = s.TileIndex(16, 15, 0);
		var z1 = s.TileIndex(16, 15, 1);
		var z2 = s.TileIndex(16, 15, 2);
		Assert.Equal(TileKind.Stairs, s.KindAt(z2));
		Assert.Equal(new SimEvent[] { new TileDug(16, 15, 2, Material.Stone) }, sim.DrainEvents());
		// The goblin walked down the z0 Stairs onto the z1 Stairs and dug z2 from there.
		Assert.Equal((16, 15, 1), (s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]));
		Assert.Equal([z1, z2], PathBetween(s, z0, z2));
		Assert.Equal([z1, z0], PathBetween(s, z2, z0));
	}

	[Fact]
	public void Stairs_on_z2_lead_nowhere_further_down()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		DigStairsAndWait(sim, 16, 15, 0);
		DigStairsAndWait(sim, 16, 15, 1);
		DigStairsAndWait(sim, 16, 15, 2);
		var z2 = s.TileIndex(16, 15, 2);
		// One layer past z2 in the tile array is off the map, not a level below, and the search must not read it.
		var offMap = z2 + s.Width * s.Height;
		Assert.True(offMap >= s.TileKinds.Length);

		Assert.False(new GridPathfinder().TryFindPath(s, z2, [s.TileIndex(1, 1, 1)], new List<int>(), out _));
		Assert.Equal([s.TileIndex(16, 15, 1)], PathBetween(s, z2, s.TileIndex(16, 15, 1)));
	}

	[Theory]
	[InlineData(true, true)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(false, false)]
	public void A_goblin_cannot_move_between_levels_where_only_one_of_the_two_tiles_is_Stairs(bool upperIsStairs, bool goblinOnStairs)
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		s.TileKinds[s.TileIndex(16, 15, 0)] = (byte)(upperIsStairs ? TileKind.Stairs : TileKind.Floor);
		s.TileKinds[s.TileIndex(16, 15, 1)] = (byte)(upperIsStairs ? TileKind.Floor : TileKind.Stairs);

		// The goblin stands on the Stairs or on the Floor of the pair. Its work is on the other level, beside
		// the other tile of the pair: in reach from there, and only from there.
		var goblinZ = goblinOnStairs == upperIsStairs ? 0 : 1;
		var targetZ = 1 - goblinZ;
		(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]) = (16, 15, goblinZ);
		var target = s.TileIndex(16, 14, targetZ);
		var kindBefore = s.KindAt(target);
		var materialBefore = s.MaterialAt(target);
		sim.Apply(new Designate(DesignationKind.DigStairs, targetZ, 16, 14, 16, 14));

		// Wandering may change x/y, but neither wandering nor claiming work can cross this pair.
		for (var tick = 0; tick < 200; tick++)
		{
			RunTicks(sim, 1);
			Assert.Equal(goblinZ, s.GoblinZ[0]);
			Assert.Equal(-1, s.GoblinJobTile[0]);
			Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(target));
			Assert.Equal(0, s.DigProgress[target]);
			Assert.Equal(kindBefore, s.KindAt(target));
			Assert.Equal(materialBefore, s.MaterialAt(target));
			Assert.Empty(sim.DrainEvents());
		}
	}

	[Fact]
	public void A_column_of_Stairs_from_z0_to_z2_replays_identically_and_survives_a_save()
	{
		var log = new (int Tick, Command Command)[]
		{
			(0, new Designate(DesignationKind.DigStairs, 0, 16, 15, 16, 15)),
			(1, new Designate(DesignationKind.DigStairs, 1, 16, 15, 16, 15)),
			(2, new Designate(DesignationKind.DigStairs, 2, 16, 15, 16, 15)),
		};

		var first = Replay(seed: 7, log, ticks: 200);
		var second = Replay(seed: 7, log, ticks: 200);

		Assert.Equal(SimJson.Serialize(first), SimJson.Serialize(second));
		var s = first.State;
		for (var z = 0; z < 3; z++)
			Assert.Equal(TileKind.Stairs, s.KindAt(s.TileIndex(16, 15, z)));
		var restored = SimJson.Deserialize(SimJson.Serialize(first));
		Assert.Equal(SimJson.Serialize(first), SimJson.Serialize(restored));
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
