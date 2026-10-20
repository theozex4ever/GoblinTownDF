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
	public void Z0_is_all_floor_and_z1_z2_are_all_solid_dirt()
	{
		var s = Simulation.NewWorld(seed: 7).State;

		for (var z = 0; z < s.Depth; z++)
		for (var y = 0; y < s.Height; y++)
		for (var x = 0; x < s.Width; x++)
		{
			var i = s.TileIndex(x, y, z);
			if (z == 0)
			{
				Assert.Equal(TileKind.Floor, s.KindAt(i));
				Assert.Equal(Material.None, s.MaterialAt(i));
			}
			else
			{
				Assert.Equal(TileKind.Solid, s.KindAt(i));
				Assert.Equal(Material.Dirt, s.MaterialAt(i));
			}
		}
	}

	[Fact]
	public void One_grunt_stands_at_16_16_on_z0()
	{
		var s = Simulation.NewWorld(seed: 7).State;

		Assert.Equal(1, s.GoblinCount);
		Assert.Equal(Role.Grunt, (Role)s.GoblinRole[0]);
		Assert.Equal(16, s.GoblinX[0]);
		Assert.Equal(16, s.GoblinY[0]);
		Assert.Equal(0, s.GoblinZ[0]);
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
