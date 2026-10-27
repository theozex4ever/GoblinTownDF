namespace Goblins.Sim;

/// <summary>
/// The global job list and the goblins that work it. A designated tile is a Job; an idle goblin claims
/// the nearest one by path length (ties go to the lowest tile index), walks to a tile it can dig from,
/// and digs. A goblin with no reachable Job wanders instead. Loops over the state arrays; the only
/// memory it owns is reused scratch lists.
/// </summary>
public sealed class JobSystem
{
	/// <summary>A wander ends at most this many tiles away on each axis.</summary>
	public const int WanderRadius = 4;
	// Random targets tried per wander before the goblin gives up and pauses again.
	const int WanderAttempts = 8;

	readonly IPathfinder _pathfinder;
	readonly List<int> _path = [];
	readonly List<int> _goals = [];
	readonly List<int> _goalJobs = [];
	readonly List<int> _starts = [];
	byte[] _reached = [];

	public JobSystem(IPathfinder pathfinder)
	{
		_pathfinder = pathfinder;
	}

	/// <summary>Designates the rectangle, skipping tiles that cannot take it or are already designated.</summary>
	public void Designate(SimState s, Designate command)
	{
		if (command.Kind is not (DesignationKind.Dig or DesignationKind.DigStairs))
			throw new ArgumentOutOfRangeException(nameof(command), command.Kind, "Only Dig and Dig stairs can be designated.");
		if (!TryClip(s, command.Z, command.X0, command.Y0, command.X1, command.Y1, out var x0, out var y0, out var x1, out var y1))
			return;
		var designated = false;
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
		{
			var tile = s.TileIndex(x, y, command.Z);
			if (s.DesignationAt(tile) == DesignationKind.None
				&& TileRules.CanDesignate(command.Kind, s.KindAt(tile))
				&& MaterialRules.DigTicks(s.MaterialAt(tile)) > 0)
			{
				s.Designations[tile] = (byte)command.Kind;
				designated = true;
			}
		}

		if (designated)
			RecheckReachability(s);
	}

	/// <summary>
	/// Rebuilds <see cref="SimState.Reachable"/> from the tiles, designations and goblin positions. Runs once
	/// when a tile is dug, when a Designation is made, and when a sim is built, and never on a tick that did
	/// none of those. Anything else that opens a path or adds a Designation calls this too, through
	/// <see cref="Simulation.RecheckReachability"/>. Cancellation needs no flood: IsReachable checks that
	/// the Designation still exists.
	/// </summary>
	public void RecheckReachability(SimState s)
	{
		var tiles = s.TileKinds.Length;
		if (s.Reachable.Length != tiles)
			s.Reachable = new byte[tiles];
		if (_reached.Length != tiles)
			_reached = new byte[tiles];

		// Goblins never leave the region they stand in, so what they can reach only changes when a tile does.
		_starts.Clear();
		for (var g = 0; g < s.GoblinCount; g++)
			_starts.Add(s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]));
		_pathfinder.FloodReachable(s, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_starts), _reached);

		for (var tile = 0; tile < tiles; tile++)
		{
			s.Reachable[tile] = 0;
			if (s.DesignationAt(tile) == DesignationKind.None)
				continue;

			_goals.Clear();
			_goalJobs.Clear();
			AddStandingTiles(s, tile);
			foreach (var stand in _goals)
				if (_reached[stand] != 0)
				{
					s.Reachable[tile] = 1;
					break;
				}
		}
	}

	/// <summary>
	/// Cancels the Designations in the rectangle, skipping tiles that have none. A cancelled tile loses its
	/// dig progress, and a goblin working it is freed to claim its next Job.
	/// </summary>
	public void Cancel(SimState s, CancelDesignation command)
	{
		if (!TryClip(s, command.Z, command.X0, command.Y0, command.X1, command.Y1, out var x0, out var y0, out var x1, out var y1))
			return;
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
		{
			var tile = s.TileIndex(x, y, command.Z);
			if (s.DesignationAt(tile) == DesignationKind.None)
				continue;

			s.Designations[tile] = (byte)DesignationKind.None;
			s.DigProgress[tile] = 0;
			for (var g = 0; g < s.GoblinCount; g++)
				if (s.GoblinJobTile[g] == tile)
					GoblinTasks.Release(s, g);
		}
	}

	/// <summary>
	/// Clips the rectangle to the map, corners in any order. False when it lies wholly off the map, which
	/// covers nothing rather than the edge row. Throws if the level is off the map.
	/// </summary>
	static bool TryClip(SimState s, int z, int cx0, int cy0, int cx1, int cy1, out int x0, out int y0, out int x1, out int y1)
	{
		if (z < 0 || z >= s.Depth)
			throw new ArgumentOutOfRangeException(nameof(z), z, "Level is off the map.");

		x0 = Math.Max(Math.Min(cx0, cx1), 0);
		x1 = Math.Min(Math.Max(cx0, cx1), s.Width - 1);
		y0 = Math.Max(Math.Min(cy0, cy1), 0);
		y1 = Math.Min(Math.Max(cy0, cy1), s.Height - 1);
		return x0 <= x1 && y0 <= y1;
	}

	public void Step(SimState s, List<SimEvent> events)
	{
		for (var g = 0; g < s.GoblinCount; g++)
		{
			// An idle or wandering goblin takes any Job it can reach; with none, it wanders on.
			if ((GoblinTask)s.GoblinTask[g] is GoblinTask.Idle or GoblinTask.Wandering && !TryClaim(s, g))
			{
				Wander(s, g);
				continue;
			}

			var job = s.GoblinJobTile[g];
			if (s.DesignationAt(job) == DesignationKind.None)
			{
				GoblinTasks.Release(s, g);
				continue;
			}

			// One action per tick: arriving spends the tick, so digging starts on the next one.
			switch ((GoblinTask)s.GoblinTask[g])
			{
				case GoblinTask.Walking:
					Walk(s, g, job);
					break;
				case GoblinTask.Digging:
					Dig(s, g, job, events);
					break;
			}
		}
	}

	bool TryClaim(SimState s, int goblin)
	{
		_goals.Clear();
		_goalJobs.Clear();
		for (var tile = 0; tile < s.Designations.Length; tile++)
		{
			if (!s.IsReachable(tile) || IsClaimed(s, tile))
				continue;
			AddStandingTiles(s, tile);
		}

		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (!_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out var goalIndex))
			return false;

		GoblinTasks.BeginJob(s, goblin, _goalJobs[goalIndex], digging: _path.Count == 0);
		return true;
	}

	void Wander(SimState s, int goblin)
	{
		if ((GoblinTask)s.GoblinTask[goblin] == GoblinTask.Wandering)
		{
			WalkToWanderTile(s, goblin);
			return;
		}

		if (GoblinTasks.CountDownPause(s, goblin))
			StartWander(s, goblin);
	}

	void StartWander(SimState s, int goblin)
	{
		var x = s.GoblinX[goblin];
		var y = s.GoblinY[goblin];
		var z = s.GoblinZ[goblin];
		var start = s.TileIndex(x, y, z);
		for (var attempt = 0; attempt < WanderAttempts; attempt++)
		{
			var tx = x + s.Rng.NextInt(2 * WanderRadius + 1) - WanderRadius;
			var ty = y + s.Rng.NextInt(2 * WanderRadius + 1) - WanderRadius;
			if ((tx == x && ty == y) || tx < 0 || ty < 0 || tx >= s.Width || ty >= s.Height)
				continue;

			var target = s.TileIndex(tx, ty, z);
			if (!TileRules.IsWalkable(s.KindAt(target)) || !TryFindWanderPath(s, start, target))
				continue;

			GoblinTasks.BeginWander(s, goblin, target);
			return;
		}
	}

	void WalkToWanderTile(SimState s, int goblin)
	{
		if (++s.GoblinStepTicks[goblin] < GoblinTasks.WalkTicksPerTile)
			return;
		s.GoblinStepTicks[goblin] = 0;

		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (!TryFindWanderPath(s, start, s.GoblinWanderTile[goblin]) || _path.Count == 0)
		{
			GoblinTasks.Release(s, goblin);
			return;
		}

		var next = _path[0];
		s.GoblinX[goblin] = next % s.Width;
		s.GoblinY[goblin] = next / s.Width % s.Height;
		if (_path.Count == 1)
			GoblinTasks.Release(s, goblin);
	}

	// Finds the way to a wander target into _path. A wander stays on one level, so a route that
	// leaves the goblin's level (over Stairs) does not count.
	bool TryFindWanderPath(SimState s, int start, int target)
	{
		_goals.Clear();
		_goals.Add(target);
		if (!_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out _))
			return false;

		var layer = s.Width * s.Height;
		foreach (var tile in _path)
			if (tile / layer != start / layer)
				return false;
		return true;
	}

	void Walk(SimState s, int goblin, int job)
	{
		if (++s.GoblinStepTicks[goblin] < GoblinTasks.WalkTicksPerTile)
			return;
		s.GoblinStepTicks[goblin] = 0;

		_goals.Clear();
		_goalJobs.Clear();
		AddStandingTiles(s, job);
		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (!_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out _)
			|| _path.Count == 0)
		{
			GoblinTasks.Release(s, goblin);
			return;
		}

		var next = _path[0];
		var layer = s.Width * s.Height;
		s.GoblinX[goblin] = next % s.Width;
		s.GoblinY[goblin] = next / s.Width % s.Height;
		s.GoblinZ[goblin] = next / layer;
		if (_path.Count == 1)
			GoblinTasks.StartDigging(s, goblin);
	}

	void Dig(SimState s, int goblin, int job, List<SimEvent> events)
	{
		var material = s.MaterialAt(job);
		var progress = ++s.DigProgress[job];
		if (progress < MaterialRules.DigTicks(material))
			return;

		s.TileKinds[job] = (byte)TileRules.DugKind(s.DesignationAt(job));
		s.Designations[job] = (byte)DesignationKind.None;
		s.DigProgress[job] = 0;
		var layer = s.Width * s.Height;
		events.Add(new TileDug(job % s.Width, job / s.Width % s.Height, job / layer, material));
		GoblinTasks.Release(s, goblin);
		RecheckReachability(s);
	}

	static bool IsClaimed(SimState s, int tile)
	{
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.GoblinJobTile[g] == tile)
				return true;
		return false;
	}

	/// <summary>Appends the tiles a goblin can dig <paramref name="tile"/> from (see <see cref="TileRules.CanDigFrom"/>).</summary>
	void AddStandingTiles(SimState s, int tile)
	{
		var designation = s.DesignationAt(tile);
		var width = s.Width;
		var layer = s.Width * s.Height;
		var x = tile % width;
		var y = tile / width % s.Height;
		var z = tile / layer;
		if (x > 0) AddIfStanding(s, tile - 1, tile, designation);
		if (x < width - 1) AddIfStanding(s, tile + 1, tile, designation);
		if (y > 0) AddIfStanding(s, tile - width, tile, designation);
		if (y < s.Height - 1) AddIfStanding(s, tile + width, tile, designation);
		if (z > 0) AddIfStanding(s, tile - layer, tile, designation);
		if (z < s.Depth - 1) AddIfStanding(s, tile + layer, tile, designation);
	}

	void AddIfStanding(SimState s, int stand, int job, DesignationKind designation)
	{
		if (TileRules.CanDigFrom(s, stand, job, designation))
		{
			_goals.Add(stand);
			_goalJobs.Add(job);
		}
	}
}
