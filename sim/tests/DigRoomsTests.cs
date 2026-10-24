using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

public class DigRoomsTests
{
	// One tick of wall time at 1×.
	const double OneTick = 0.1;

	static void RunTicks(Simulation sim, int ticks)
	{
		for (var i = 0; i < ticks; i++)
			Assert.Equal(1, sim.Advance(OneTick));
	}

	static Designate DigRect(int z, int x0, int y0, int x1, int y1) => new(DesignationKind.Dig, z, x0, y0, x1, y1);

	// A stair shaft at (x, y) linking z0 and z1, with the Grunt standing on its z1 end.
	static Simulation WorldWithShaftAt(int x, int y)
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.TileKinds[s.TileIndex(x, y, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[s.TileIndex(x, y, 1)] = (byte)TileKind.Stairs;
		(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]) = (x, y, 1);
		return sim;
	}

	[Fact]
	public void Dig_on_a_rectangle_of_Solid_tiles_beside_Stairs_turns_all_of_them_to_Floor_with_one_tile_dug_each()
	{
		var sim = WorldWithShaftAt(16, 15);
		var s = sim.State;

		sim.Apply(DigRect(z: 1, x0: 17, y0: 12, x1: 20, y1: 15));
		RunTicks(sim, 16 * (1 + 20) + 20);

		var dug = new List<(int, int, int)>();
		foreach (var e in sim.DrainEvents())
		{
			var tileDug = Assert.IsType<TileDug>(e);
			Assert.Equal(Material.Dirt, tileDug.Material);
			dug.Add((tileDug.X, tileDug.Y, tileDug.Z));
		}
		var expected = new List<(int, int, int)>();
		for (var y = 12; y <= 15; y++)
		for (var x = 17; x <= 20; x++)
		{
			Assert.Equal(TileKind.Floor, s.KindAt(s.TileIndex(x, y, 1)));
			Assert.Equal(DesignationKind.None, s.DesignationAt(s.TileIndex(x, y, 1)));
			expected.Add((x, y, 1));
		}
		Assert.Equal(expected.OrderBy(t => t).ToList(), dug.OrderBy(t => t).ToList());
		Assert.Equal((byte)GoblinTask.Idle, s.GoblinTask[0]);
	}

	// Dug-tile order for a 3×2 room east of a shaft at (16, 15), as (x, y). Worked out by hand, not by the sim:
	//   y14:  .  A  B  C        Stairs at (16, 15); the Grunt starts there.
	//   y15:  S  D  E  F
	// Only D touches the Stairs, so D is first. Then A and E tie one step away; A has the lower tile index.
	// From D, E is no steps away, so E. Then B and F tie one step away (B via A or E, F via E); B has the
	// lower tile index, and A is the standing tile listed first for it. From A, C is one step away (via B)
	// and F is two, so C. Finally F is a step from either neighbour.
	static readonly (int X, int Y)[] RoomOrder = [(17, 15), (17, 14), (18, 15), (18, 14), (19, 14), (19, 15)];

	static (List<(int X, int Y)> Order, Simulation Sim) DigRoom()
	{
		var sim = WorldWithShaftAt(16, 15);
		sim.Apply(DigRect(z: 1, x0: 17, y0: 14, x1: 19, y1: 15));

		var order = new List<(int, int)>();
		for (var tick = 0; tick < 6 * (1 + 20) + 20; tick++)
		{
			RunTicks(sim, 1);
			foreach (var e in sim.DrainEvents())
				order.Add((((TileDug)e).X, ((TileDug)e).Y));
		}
		return (order, sim);
	}

	[Fact]
	public void The_goblin_digs_a_room_nearest_first_by_path_length_with_ties_to_the_lowest_tile_index()
	{
		Assert.Equal(RoomOrder, DigRoom().Order);
	}

	[Fact]
	public void The_dig_order_is_identical_across_two_runs()
	{
		var first = DigRoom();
		var second = DigRoom();

		Assert.Equal(first.Order, second.Order);
		Assert.Equal(SimJson.Serialize(first.Sim), SimJson.Serialize(second.Sim));
	}

	[Theory]
	[InlineData(TileKind.Floor)]
	[InlineData(TileKind.Stairs)]
	[InlineData(TileKind.OpenAir)]
	public void Dig_on_a_tile_that_is_not_Solid_creates_no_designation(TileKind kind)
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(16, 15, 0);
		s.TileKinds[tile] = (byte)kind;

		sim.Apply(DigRect(z: 0, x0: 16, y0: 15, x1: 16, y1: 15));

		Assert.Equal(DesignationKind.None, s.DesignationAt(tile));
		RunTicks(sim, 30);
		Assert.Empty(sim.DrainEvents());
		Assert.Equal((byte)GoblinTask.Idle, s.GoblinTask[0]);
	}

	[Fact]
	public void Dig_on_a_rectangle_of_Floor_designates_nothing()
	{
		var sim = Simulation.NewWorld(seed: 1);

		sim.Apply(DigRect(z: 0, x0: 10, y0: 10, x1: 14, y1: 14));

		Assert.All(sim.State.Designations, d => Assert.Equal((byte)DesignationKind.None, d));
	}

	[Fact]
	public void Dig_on_a_rectangle_that_is_part_Floor_designates_only_the_Solid_tiles()
	{
		var sim = WorldWithShaftAt(16, 15);
		var s = sim.State;
		s.TileKinds[s.TileIndex(18, 14, 1)] = (byte)TileKind.Floor;

		sim.Apply(DigRect(z: 1, x0: 17, y0: 14, x1: 19, y1: 14));

		Assert.Equal(DesignationKind.Dig, s.DesignationAt(s.TileIndex(17, 14, 1)));
		Assert.Equal(DesignationKind.None, s.DesignationAt(s.TileIndex(18, 14, 1)));
		Assert.Equal(DesignationKind.Dig, s.DesignationAt(s.TileIndex(19, 14, 1)));
	}

	[Fact]
	public void Dig_does_not_replace_a_Dig_stairs_designation_and_leaves_the_tile_as_Stairs()
	{
		var sim = WorldWithShaftAt(16, 15);
		var s = sim.State;
		var tile = s.TileIndex(17, 15, 1);
		sim.Apply(new Designate(DesignationKind.DigStairs, 1, 17, 15, 17, 15));

		sim.Apply(DigRect(z: 1, x0: 17, y0: 15, x1: 17, y1: 15));
		RunTicks(sim, 1 + 20);

		Assert.Equal(TileKind.Stairs, s.KindAt(tile));
	}

	[Fact]
	public void Dig_stairs_on_Stairs_is_ignored()
	{
		var sim = WorldWithShaftAt(16, 15);
		var s = sim.State;

		sim.Apply(new Designate(DesignationKind.DigStairs, 1, 16, 15, 16, 15));

		Assert.Equal(DesignationKind.None, s.DesignationAt(s.TileIndex(16, 15, 1)));
	}

	[Fact]
	public void Dig_is_dug_from_beside_and_never_from_Stairs_directly_above()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		// Stairs at (16, 15, 0) with Solid under it. Dig stairs could be dug from there; Dig cannot.
		s.TileKinds[s.TileIndex(16, 15, 0)] = (byte)TileKind.Stairs;
		var tile = s.TileIndex(16, 15, 1);
		sim.Apply(DigRect(z: 1, x0: 16, y0: 15, x1: 16, y1: 15));

		RunTicks(sim, 100);

		Assert.Equal(TileKind.Solid, s.KindAt(tile));
		Assert.Equal(DesignationKind.Dig, s.DesignationAt(tile));
		Assert.Equal((byte)GoblinTask.Idle, s.GoblinTask[0]);
		Assert.Empty(sim.DrainEvents());
	}
}
