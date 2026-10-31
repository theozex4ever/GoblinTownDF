namespace Goblins.Sim;

/// <summary>
/// The global job list and the goblins that work it. A designated tile is a dig Job and a tile with a loose
/// unit is a Haul Job. An idle goblin claims the nearest one of either kind by path length (ties go to the
/// lowest tile index). It walks to a tile it can dig from and digs, or walks to the unit, picks it up (5 ticks),
/// carries it to the nearest Pile tile and drops it (5 ticks). A goblin with no reachable Job wanders instead.
/// Loops over the state arrays; the only memory it owns is reused scratch lists.
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
	// Parallel to _goals while a claim is being weighed: whether the goal is a loose unit rather than a dig.
	readonly List<bool> _goalHauls = [];
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
	/// Rebuilds <see cref="SimState.Reachable"/> and <see cref="SimState.HaulReachable"/> from the tiles,
	/// designations, loose units, Piles and goblin positions. Runs once when a tile is dug, when a Designation
	/// is made, when a Pile is painted or removed, when a goblin puts a unit down loose, and when a sim is
	/// built, and never on a tick that did none of those. Anything else that opens a path or adds a Designation calls this too, through
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

		// A Haul is reachable when its unit and some Pile tile are.
		var pileReached = false;
		for (var tile = 0; tile < tiles && !pileReached; tile++)
			pileReached = s.Piles[tile] != 0 && _reached[tile] != 0;
		if (s.HaulReachable.Length != tiles)
			s.HaulReachable = new byte[tiles];

		for (var tile = 0; tile < tiles; tile++)
		{
			s.HaulReachable[tile] = (byte)(pileReached && _reached[tile] != 0 && s.LooseMaterial(tile) != Material.None ? 1 : 0);
			s.Reachable[tile] = 0;
			if (s.DesignationAt(tile) == DesignationKind.None)
				continue;

			ClearGoals();
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
				if (s.GoblinJobTile[g] == tile && GoblinTasks.IsDigTask((GoblinTask)s.GoblinTask[g]))
					GoblinTasks.Release(s, g);
		}
	}

	/// <summary>
	/// Clips the rectangle to the map, corners in any order. False when it lies wholly off the map, which
	/// covers nothing rather than the edge row. Throws if the level is off the map.
	/// </summary>
	internal static bool TryClip(SimState s, int z, int cx0, int cy0, int cx1, int cy1, out int x0, out int y0, out int x1, out int y1)
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

			// One action per tick: arriving spends the tick, so the work starts on the next one. A goblin that
			// carries a unit is never offered a new Job, whatever becomes reachable.
			switch ((GoblinTask)s.GoblinTask[g])
			{
				case GoblinTask.Walking:
				case GoblinTask.Digging:
					var job = s.GoblinJobTile[g];
					if (s.DesignationAt(job) == DesignationKind.None)
						GoblinTasks.Release(s, g);
					else if ((GoblinTask)s.GoblinTask[g] == GoblinTask.Walking)
						Walk(s, g, job);
					else
						Dig(s, g, job, events);
					break;
				case GoblinTask.Fetching:
					Fetch(s, g);
					break;
				case GoblinTask.PickingUp:
					PickUp(s, g);
					break;
				case GoblinTask.Carrying:
					Carry(s, g);
					break;
				case GoblinTask.Dropping:
					Drop(s, g);
					break;
			}
		}
	}

	// Claims the nearest reachable Job of either kind: dig goals are the tiles a goblin can dig from, haul goals are
	// the loose units themselves. Goals go in tile order, so a tie goes to the lowest tile index.
	bool TryClaim(SimState s, int goblin)
	{
		ClearGoals();
		for (var tile = 0; tile < s.Designations.Length; tile++)
		{
			if (s.IsReachable(tile) && !IsClaimed(s, tile, haul: false))
				AddStandingTiles(s, tile);
			if (s.IsHaulReachable(tile) && !IsClaimed(s, tile, haul: true))
				AddGoal(tile, tile, haul: true);
		}

		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (!_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out var goalIndex))
			return false;

		if (_goalHauls[goalIndex])
			GoblinTasks.BeginFetch(s, goblin, _goalJobs[goalIndex], pickingUp: _path.Count == 0);
		else
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
		if (!StepDue(s, goblin))
			return;

		ClearGoals();
		AddStandingTiles(s, job);
		switch (MoveToward(s, goblin))
		{
			case Move.NoPath:
			case Move.Here:
				GoblinTasks.Release(s, goblin);
				break;
			case Move.Arrived:
				GoblinTasks.StartDigging(s, goblin);
				break;
		}
	}

	/// <summary>True on the tick a walking goblin is due to take its next step; otherwise it counts the tick.</summary>
	static bool StepDue(SimState s, int goblin)
	{
		if (++s.GoblinStepTicks[goblin] < GoblinTasks.WalkTicksPerTile)
			return false;
		s.GoblinStepTicks[goblin] = 0;
		return true;
	}

	enum Move
	{
		/// <summary>No goal in <c>_goals</c> can be reached.</summary>
		NoPath,
		/// <summary>The goblin already stands on a goal, so it did not move.</summary>
		Here,
		/// <summary>One step along the way; more remain.</summary>
		Moved,
		/// <summary>One step onto the nearest goal.</summary>
		Arrived,
	}

	/// <summary>Takes one step toward the nearest of the goal tiles in <c>_goals</c>, on any level.</summary>
	Move MoveToward(SimState s, int goblin)
	{
		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (!_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out _))
			return Move.NoPath;
		if (_path.Count == 0)
			return Move.Here;

		var next = _path[0];
		var layer = s.Width * s.Height;
		s.GoblinX[goblin] = next % s.Width;
		s.GoblinY[goblin] = next / s.Width % s.Height;
		s.GoblinZ[goblin] = next / layer;
		return _path.Count == 1 ? Move.Arrived : Move.Moved;
	}

	void Fetch(SimState s, int goblin)
	{
		if (!StepDue(s, goblin))
			return;

		ClearGoals();
		AddGoal(s.GoblinJobTile[goblin], s.GoblinJobTile[goblin], haul: true);
		switch (MoveToward(s, goblin))
		{
			case Move.NoPath:
				GoblinTasks.Release(s, goblin);
				break;
			case Move.Here:
			case Move.Arrived:
				GoblinTasks.StartPickingUp(s, goblin);
				break;
		}
	}

	void PickUp(SimState s, int goblin)
	{
		if (!GoblinTasks.WorkTick(s, goblin))
			return;

		var tile = s.GoblinJobTile[goblin];
		var material = s.LooseMaterial(tile);
		if (material == Material.None)
		{
			GoblinTasks.Release(s, goblin);
			return;
		}

		s.RemoveItems(tile, material, 1);
		GoblinTasks.FinishPickUp(s, goblin, material);
	}

	// Walks to the nearest Pile tile by path length, ties to the lowest tile index. The route is found afresh at every
	// step, so a Pile painted or removed on the way changes the goal. With no Pile in reach the goblin puts the unit down
	// where it stands.
	void Carry(SimState s, int goblin)
	{
		if (!StepDue(s, goblin))
			return;

		ClearGoals();
		for (var tile = 0; tile < s.Piles.Length; tile++)
			if (s.Piles[tile] != 0)
				AddGoal(tile, tile, haul: false);
		switch (MoveToward(s, goblin))
		{
			case Move.NoPath:
				DropWhereStanding(s, goblin);
				break;
			case Move.Here:
			case Move.Arrived:
				GoblinTasks.StartDropping(s, goblin);
				break;
		}
	}

	// Dropping on a Pile tile stores the unit.
	void Drop(SimState s, int goblin)
	{
		if (GoblinTasks.WorkTick(s, goblin))
			PutDown(s, goblin);
	}

	// With no Pile in reach, the unit is loose again and its Haul waits for a Pile.
	void DropWhereStanding(SimState s, int goblin)
	{
		PutDown(s, goblin);
		RecheckReachability(s);
	}

	// Puts the carried unit on the goblin's own tile and frees the goblin.
	static void PutDown(SimState s, int goblin)
	{
		var tile = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		s.AddItems(tile, (Material)s.GoblinCarry[goblin], 1);
		GoblinTasks.Release(s, goblin);
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
		// Only Floor can hold a Pile; its stock stays on the tile, loose.
		var wasPile = s.Piles[job] != 0;
		s.Piles[job] = 0;
		s.AddItems(job, material, 1);
		var layer = s.Width * s.Height;
		events.Add(new TileDug(job % s.Width, job / s.Width % s.Height, job / layer, material));
		GoblinTasks.Release(s, goblin);
		if (wasPile)
			PileRules.Settle(s);
		RecheckReachability(s);
	}

	static bool IsClaimed(SimState s, int tile, bool haul)
	{
		for (var g = 0; g < s.GoblinCount; g++)
		{
			var task = (GoblinTask)s.GoblinTask[g];
			if (s.GoblinJobTile[g] == tile && (haul ? GoblinTasks.IsFetchTask(task) : GoblinTasks.IsDigTask(task)))
				return true;
		}
		return false;
	}

	void ClearGoals()
	{
		_goals.Clear();
		_goalJobs.Clear();
		_goalHauls.Clear();
	}

	void AddGoal(int stand, int job, bool haul)
	{
		_goals.Add(stand);
		_goalJobs.Add(job);
		_goalHauls.Add(haul);
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
			AddGoal(stand, job, haul: false);
	}
}
