using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

public class PathfindingTests
{
	static readonly GridPathfinder Finder = new();

	[Fact]
	public void The_path_is_the_shortest_orthogonal_route_and_leaves_out_the_start()
	{
		var s = Simulation.NewWorld(seed: 1).State;
		var path = new List<int>();

		var found = Finder.TryFindPath(s, s.TileIndex(2, 2, 0), [s.TileIndex(5, 3, 0)], path, out var goal);

		Assert.True(found);
		Assert.Equal(0, goal);
		Assert.Equal(4, path.Count);
		Assert.Equal(s.TileIndex(5, 3, 0), path[^1]);
		var (px, py) = (2, 2);
		foreach (var tile in path)
		{
			var (x, y) = (tile % s.Width, tile / s.Width);
			Assert.Equal(1, Math.Abs(x - px) + Math.Abs(y - py));
			(px, py) = (x, y);
		}
	}

	[Fact]
	public void A_start_that_is_a_goal_has_an_empty_path()
	{
		var s = Simulation.NewWorld(seed: 1).State;
		var path = new List<int> { 99 };

		var found = Finder.TryFindPath(s, s.TileIndex(2, 2, 0), [s.TileIndex(2, 2, 0)], path, out var goal);

		Assert.True(found);
		Assert.Equal(0, goal);
		Assert.Empty(path);
	}

	[Fact]
	public void Equally_near_goals_go_to_the_earliest_in_the_list()
	{
		var s = Simulation.NewWorld(seed: 1).State;
		var west = s.TileIndex(1, 5, 0);
		var east = s.TileIndex(5, 5, 0);

		Finder.TryFindPath(s, s.TileIndex(3, 5, 0), [east, west], new List<int>(), out var first);
		Finder.TryFindPath(s, s.TileIndex(3, 5, 0), [west, east], new List<int>(), out var second);

		Assert.Equal(0, first);
		Assert.Equal(0, second);
	}

	[Fact]
	public void The_nearer_goal_wins_even_when_it_is_later_in_the_list()
	{
		var s = Simulation.NewWorld(seed: 1).State;

		Finder.TryFindPath(s, s.TileIndex(3, 5, 0), [s.TileIndex(20, 5, 0), s.TileIndex(4, 5, 0)], new List<int>(), out var goal);

		Assert.Equal(1, goal);
	}

	[Fact]
	public void Solid_tiles_block_the_way_and_levels_join_only_through_Stairs_above_each_other()
	{
		var s = Simulation.NewWorld(seed: 1).State;
		var path = new List<int>();
		var below = s.TileIndex(4, 4, 1);

		// z1 is all Solid: nothing walks into it.
		Assert.False(Finder.TryFindPath(s, s.TileIndex(4, 4, 0), [below], path, out _));

		// One Stairs tile on its own does not link the levels...
		s.TileKinds[s.TileIndex(4, 4, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[below] = (byte)TileKind.Floor;
		Assert.False(Finder.TryFindPath(s, s.TileIndex(4, 4, 0), [below], path, out _));

		// ...two stacked Stairs tiles do, and the way down is one step.
		s.TileKinds[below] = (byte)TileKind.Stairs;
		Assert.True(Finder.TryFindPath(s, s.TileIndex(4, 4, 0), [below], path, out _));
		Assert.Equal([below], path);
	}

	[Fact]
	public void Paths_do_not_wrap_from_one_row_to_the_next()
	{
		var s = Simulation.NewWorld(seed: 1).State;
		var path = new List<int>();

		// (0, 5) and (31, 4) sit next to each other in the tile array but 32 steps apart on the map.
		Assert.True(Finder.TryFindPath(s, s.TileIndex(0, 5, 0), [s.TileIndex(31, 4, 0)], path, out _));
		Assert.Equal(32, path.Count);
		Assert.True(Finder.TryFindPath(s, s.TileIndex(31, 4, 0), [s.TileIndex(0, 5, 0)], path, out _));
		Assert.Equal(32, path.Count);
	}

	[Fact]
	public void Paths_do_not_wrap_from_one_level_to_the_next()
	{
		var s = Simulation.NewWorld(seed: 1).State;
		// The top row of z1 sits right after the bottom row of z0 in the tile array, but no Stairs link them.
		s.TileKinds[s.TileIndex(5, 0, 1)] = (byte)TileKind.Floor;

		Assert.False(Finder.TryFindPath(s, s.TileIndex(5, 0, 1), [s.TileIndex(5, 31, 0)], new List<int>(), out _));
		Assert.False(Finder.TryFindPath(s, s.TileIndex(5, 31, 0), [s.TileIndex(5, 0, 1)], new List<int>(), out _));
	}

	[Fact]
	public void No_goals_means_no_path()
	{
		var s = Simulation.NewWorld(seed: 1).State;

		Assert.False(Finder.TryFindPath(s, 0, [], new List<int>(), out var goal));
		Assert.Equal(-1, goal);
	}
}
