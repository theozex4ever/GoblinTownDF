namespace Goblins.Sim;

/// <summary>
/// Finds routes over the 3D tile grid. Systems depend on this interface, so plain search can later
/// give way to hierarchical paths between chunks without touching them.
/// </summary>
public interface IPathfinder
{
	/// <summary>
	/// Finds the shortest route from <paramref name="start"/> to the nearest of <paramref name="goals"/>.
	/// Among goals equally near, the earliest in the span wins. <paramref name="path"/> is replaced with the
	/// tiles to step onto, in order, excluding the start (empty when the start is itself a goal).
	/// </summary>
	bool TryFindPath(SimState state, int start, ReadOnlySpan<int> goals, List<int> path, out int goalIndex);

	/// <summary>
	/// Sets <paramref name="reached"/>[tile] to 1 for every tile a goblin standing on any of
	/// <paramref name="starts"/> can walk to (the starts included), and to 0 for every other tile.
	/// <paramref name="reached"/> must hold one entry per tile.
	/// </summary>
	void FloodReachable(SimState state, ReadOnlySpan<int> starts, byte[] reached);
}

/// <summary>
/// Breadth-first search. Every step costs the same (3 ticks), so fewest steps is shortest. Steps are
/// orthogonal on one level, or between two Stairs tiles directly above each other.
/// </summary>
public sealed class GridPathfinder : IPathfinder
{
	// Scratch space reused across queries so a search allocates nothing once warm.
	int[] _parent = [];
	int[] _distance = [];
	int[] _goalRank = [];
	int[] _queue = [];

	public bool TryFindPath(SimState state, int start, ReadOnlySpan<int> goals, List<int> path, out int goalIndex)
	{
		path.Clear();
		goalIndex = -1;
		if (goals.IsEmpty)
			return false;

		EnsureScratch(state.TileKinds.Length);
		Array.Fill(_distance, -1);
		Array.Fill(_goalRank, -1);
		for (var i = goals.Length - 1; i >= 0; i--)
			_goalRank[goals[i]] = i;

		var head = 0;
		var tail = 0;
		_queue[tail++] = start;
		_distance[start] = 0;
		_parent[start] = -1;

		var bestRank = -1;
		var bestTile = -1;
		var bestDistance = -1;
		while (head < tail)
		{
			var tile = _queue[head++];
			var distance = _distance[tile];
			if (bestDistance >= 0 && distance > bestDistance)
				break;

			var rank = _goalRank[tile];
			if (rank >= 0 && (bestRank < 0 || rank < bestRank))
			{
				bestRank = rank;
				bestTile = tile;
				bestDistance = distance;
			}

			Expand(state, tile, distance, ref tail);
		}

		if (bestTile < 0)
			return false;

		for (var tile = bestTile; tile != start; tile = _parent[tile])
			path.Add(tile);
		path.Reverse();
		goalIndex = bestRank;
		return true;
	}

	public void FloodReachable(SimState state, ReadOnlySpan<int> starts, byte[] reached)
	{
		var tiles = state.TileKinds.Length;
		EnsureScratch(tiles);
		Array.Fill(_distance, -1);

		var head = 0;
		var tail = 0;
		foreach (var start in starts)
		{
			if (_distance[start] >= 0)
				continue;
			_queue[tail++] = start;
			_distance[start] = 0;
			_parent[start] = -1;
		}
		while (head < tail)
		{
			var tile = _queue[head++];
			Expand(state, tile, _distance[tile], ref tail);
		}

		for (var tile = 0; tile < tiles; tile++)
			reached[tile] = (byte)(_distance[tile] >= 0 ? 1 : 0);
	}

	void EnsureScratch(int tiles)
	{
		if (_parent.Length == tiles)
			return;
		_parent = new int[tiles];
		_distance = new int[tiles];
		_goalRank = new int[tiles];
		_queue = new int[tiles];
	}

	/// <summary>Queues every tile one step from <paramref name="tile"/> that has not been reached yet.</summary>
	void Expand(SimState state, int tile, int distance, ref int tail)
	{
		var width = state.Width;
		var layer = state.Width * state.Height;
		var x = tile % width;
		var y = tile / width % state.Height;
		var z = tile / layer;
		if (x > 0) Visit(state, tile - 1, tile, distance, ref tail);
		if (x < width - 1) Visit(state, tile + 1, tile, distance, ref tail);
		if (y > 0) Visit(state, tile - width, tile, distance, ref tail);
		if (y < state.Height - 1) Visit(state, tile + width, tile, distance, ref tail);
		if (state.KindAt(tile) == TileKind.Stairs)
		{
			if (z > 0 && state.KindAt(tile - layer) == TileKind.Stairs)
				Visit(state, tile - layer, tile, distance, ref tail);
			if (z < state.Depth - 1 && state.KindAt(tile + layer) == TileKind.Stairs)
				Visit(state, tile + layer, tile, distance, ref tail);
		}
	}

	void Visit(SimState state, int next, int from, int distance, ref int tail)
	{
		if (_distance[next] >= 0 || !TileRules.IsWalkable(state.KindAt(next)))
			return;
		_distance[next] = distance + 1;
		_parent[next] = from;
		_queue[tail++] = next;
	}
}
