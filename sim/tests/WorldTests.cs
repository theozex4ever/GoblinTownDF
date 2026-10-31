using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

public class WorldTests
{
	[Fact]
	public void New_world_is_32_by_32_by_3()
	{
		var s = Simulation.NewWorld(seed: 7).State;

		Assert.Equal(32, s.Width);
		Assert.Equal(32, s.Height);
		Assert.Equal(3, s.Depth);
		Assert.Equal(32 * 32 * 3, s.TileKinds.Length);
		Assert.Equal(s.TileKinds.Length, s.TileMaterials.Length);
	}

	[Fact]
	public void Z0_is_dirt_floor_z1_is_solid_dirt_and_z2_is_solid_stone()
	{
		var s = Simulation.NewWorld(seed: 7).State;

		for (var z = 0; z < s.Depth; z++)
		for (var y = 0; y < s.Height; y++)
		for (var x = 0; x < s.Width; x++)
		{
			var i = s.TileIndex(x, y, z);
			Assert.Equal(z == 0 ? TileKind.Floor : TileKind.Solid, s.KindAt(i));
			Assert.Equal(z == 2 ? Material.Stone : Material.Dirt, s.MaterialAt(i));
		}
	}

	[Fact]
	public void Five_grunts_stand_in_a_cluster_around_the_centre_of_z0()
	{
		var s = Simulation.NewWorld(seed: 7).State;

		Assert.Equal(5, s.GoblinCount);
		var spots = new HashSet<(int, int)>();
		for (var g = 0; g < s.GoblinCount; g++)
		{
			Assert.Equal(Role.Grunt, (Role)s.GoblinRole[g]);
			Assert.Equal(0, s.GoblinZ[g]);
			Assert.True(Math.Abs(s.GoblinX[g] - 16) + Math.Abs(s.GoblinY[g] - 16) <= 1);
			spots.Add((s.GoblinX[g], s.GoblinY[g]));
		}
		Assert.Equal(5, spots.Count);
	}

	[Fact]
	public void The_goblin_cluster_is_the_same_for_every_seed()
	{
		var a = Simulation.NewWorld(seed: 1).State;
		var b = Simulation.NewWorld(seed: 2).State;

		Assert.Equal(a.GoblinX, b.GoblinX);
		Assert.Equal(a.GoblinY, b.GoblinY);
		Assert.Equal(a.GoblinZ, b.GoblinZ);
	}

	[Fact]
	public void The_map_is_identical_for_every_seed()
	{
		var a = Simulation.NewWorld(seed: 1).State;
		var b = Simulation.NewWorld(seed: 2).State;

		Assert.Equal(a.TileKinds, b.TileKinds);
		Assert.Equal(a.TileMaterials, b.TileMaterials);
	}
}
