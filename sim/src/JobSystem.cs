namespace Goblins.Sim;

/// <summary>
/// The global job list and the goblins that work it. A designated tile is a dig Job, a tile with a loose
/// unit is a Haul Job, an untended farm tile is a Tend Job and a tile holding Fungus is a Brew Job while a Brewery tile and Grog storage are in reach. An idle goblin claims the nearest one of any kind by path length (ties go to the
/// lowest tile index). It walks to a tile it can dig from and digs, or walks to the unit, picks it up (5 ticks),
/// carries it to the nearest Pile tile with room for its material and drops it (5 ticks); with no such tile it waits holding
/// the unit; or walks to the Grub Pit tile and tends it (20 ticks), which starts the tile spawning Grub; or walks to the Fungus, takes a unit (5 ticks),
/// brings it to a free Brewery tile and brews it (20 ticks) into one Grog, loose on that tile. A goblin with no reachable Job wanders instead.
/// A goblin with a Craving for Grub or Grog that can reach a unit of it drops everything and goes to eat or drink it, whatever
/// it was doing. A Craving for Sleep always can be met: the goblin walks to the nearest Sleeping Heap tile and sleeps there, or sleeps
/// where it stands when none is in reach. With several Cravings the lowest meter wins (Grub, then Grog, then Sleep on a tie),
/// and a Craving with no unit in reach does not count. A goblin at Sleep 0 collapses and sleeps on the spot, whatever else it was doing.
/// Loops over the state arrays; the only memory it owns is reused scratch lists.
/// </summary>
public sealed class JobSystem
{
	/// <summary>A wander ends at most this many tiles away on each axis.</summary>
	public const int WanderRadius = 4;
	// Random targets tried per wander before the goblin gives up and pauses again.
	const int WanderAttempts = 8;

	// The kinds of Job on the one list. Goals that belong to no Job (a meal, a drop-off) are filed as Dig and never read back.
	enum JobKind
	{
		Dig,
		Haul,
		Tend,
		Brew,
	}

	readonly IPathfinder _pathfinder;
	readonly List<int> _path = [];
	readonly List<int> _goals = [];
	readonly List<int> _goalJobs = [];
	// Parallel to _goals while a claim is being weighed: which kind of Job the goal belongs to.
	readonly List<JobKind> _goalKinds = [];
	readonly List<int> _starts = [];
	byte[] _reached = [];
	// Per region (index 0 is no region), the Brewery tiles no goblin is Bringing Fungus to or Brewing at, and the goblins who have
	// claimed Fungus to brew and will take one. Filled by CountBreweries.
	readonly int[] _freeBreweries = new int[SimState.MaxGoblins + 1];
	readonly int[] _gatherers = new int[SimState.MaxGoblins + 1];

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
	/// Rebuilds <see cref="SimState.Reachable"/>, <see cref="SimState.HaulReachable"/> and <see cref="SimState.HaulRegion"/> from the tiles, designations and
	/// goblin positions, and <see cref="SimState.StorageRoom"/> from each region's Piles and their counts.
	/// Runs once when a tile is dug, when a Designation is made, when a Pile is painted or removed, when a drop takes a
	/// Pile tile's last slot of a material, and when a sim is built, and never on a tick that did none of those. Anything else that opens a path or adds a Designation calls this too, through
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

		// Goblins never leave the region they stand in, so what they can reach only changes when a tile does. Each region
		// is flooded from its first goblin and numbered, so a unit is only offered storage its own region's goblins can reach.
		if (s.HaulRegion.Length != tiles)
			s.HaulRegion = new byte[tiles];
		Array.Clear(s.HaulRegion);
		Array.Clear(s.StorageRoom);
		var regions = 0;
		for (var g = 0; g < s.GoblinCount; g++)
		{
			var start = s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]);
			if (s.HaulRegion[start] != 0)
				continue;
			regions++;
			_starts.Clear();
			_starts.Add(start);
			_pathfinder.FloodReachable(s, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_starts), _reached);
			for (var tile = 0; tile < tiles; tile++)
				if (_reached[tile] != 0)
					s.HaulRegion[tile] = (byte)regions;
		}

		for (var tile = 0; tile < tiles; tile++)
		{
			if (s.HaulRegion[tile] == 0 || !s.IsPile(tile))
				continue;
			for (var material = Material.Dirt; (int)material <= SimState.CountedMaterials; material++)
				if (s.PileHasRoom(tile, material))
					s.StorageRoom[(s.HaulRegion[tile] - 1) * SimState.CountedMaterials + (int)material - 1] = 1;
		}

		if (s.HaulReachable.Length != tiles)
			s.HaulReachable = new byte[tiles];
		for (var tile = 0; tile < tiles; tile++)
		{
			s.HaulReachable[tile] = (byte)(s.HaulRegion[tile] != 0 && s.LooseMaterial(tile) != Material.None ? 1 : 0);
			s.Reachable[tile] = 0;
			if (s.DesignationAt(tile) == DesignationKind.None)
				continue;

			ClearGoals();
			AddStandingTiles(s, tile);
			foreach (var stand in _goals)
				if (s.HaulRegion[stand] != 0)
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
			// A Craving comes before any Job, and before a Starving goblin sits a tick out. Eating, drinking and sleeping are not slowed.
			TryCraving(s, g);
			var task = (GoblinTask)s.GoblinTask[g];
			if (!GoblinTasks.IsConsumingTask(task) && task != GoblinTask.Sleeping && !NeedRules.ActsThisTick(s.GoblinGrub[g], s.Tick))
				continue;

			// An idle or wandering goblin takes any Job it can reach; with none, it wanders on.
			if (task is GoblinTask.Idle or GoblinTask.Wandering && !TryClaim(s, g))
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
				case GoblinTask.Approaching:
				case GoblinTask.Tending:
					Tend(s, g);
					break;
				case GoblinTask.Gathering:
					Gather(s, g);
					break;
				case GoblinTask.Taking:
					Take(s, g);
					break;
				case GoblinTask.Bringing:
					Bring(s, g);
					break;
				case GoblinTask.Brewing:
					Brew(s, g);
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
				case GoblinTask.Seeking:
				case GoblinTask.SeekingGrog:
					Seek(s, g);
					break;
				case GoblinTask.Eating:
				case GoblinTask.Drinking:
					Consume(s, g);
					break;
				case GoblinTask.SeekingHeap:
					SeekHeap(s, g);
					break;
				case GoblinTask.Sleeping:
					GoblinTasks.SleepTick(s, g);
					break;
			}
		}
	}

	// A goblin at Sleep 0 collapses at once, whatever it is doing, so long as it has somewhere to put down what it carries.
	// Otherwise a goblin with a Craving drops its Job at once when it can meet it: it can reach a unit of what it craves, Pile or loose,
	// or the Craving is for Sleep, which always can be met. The lowest meter is tried first (Grub, then Grog, then Sleep on a tie)
	// and a Craving that cannot be met is passed over for the next. Anything carried is put down first, where a legal drop is found;
	// a carrier with nowhere to put its unit keeps working, and a collapse waits for such a place. With nothing to do nothing
	// happens, and the goblin looks again next tick, which is how it notices supply or reachability changing. A goblin already
	// satisfying a Need finishes it before it considers another.
	void TryCraving(SimState s, int goblin)
	{
		var task = (GoblinTask)s.GoblinTask[goblin];
		if (s.GoblinSleep[goblin] == 0 && task != GoblinTask.Sleeping)
		{
			TryMeet(s, goblin, Need.Sleep, collapse: true);
			return;
		}
		if (GoblinTasks.IsSatisfyingTask(task))
			return;

		var passedOver = 0;
		for (var need = NeedRules.LeadingCraving(s.GoblinGrub[goblin], s.GoblinGrog[goblin], s.GoblinSleep[goblin]);
			need != Need.None;
			need = NeedRules.LeadingCraving(s.GoblinGrub[goblin], s.GoblinGrog[goblin], s.GoblinSleep[goblin], passedOver))
		{
			if (TryMeet(s, goblin, need, collapse: false))
				return;
			passedOver |= NeedRules.Bit(need);
		}
	}

	// True when the goblin set out to meet the Need: for a unit of Grub or Grog, or for a bed (the nearest Heap, or the floor under it).
	bool TryMeet(SimState s, int goblin, Need need, bool collapse)
	{
		var supply = NeedRules.SupplyOf(need);
		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		// Whether the goblin already has what it needs where it stands: a unit underfoot, or a bed (a Heap underfoot, or no Heap at all).
		bool here;
		if (supply != Material.None)
		{
			ClearGoals();
			AddSupplyGoals(s, supply);
			if (_goals.Count == 0
				|| !_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out _))
				return false;
			here = _path.Count == 0;
		}
		else
		{
			// With no Heap in reach, or after a collapse, the bed is the floor underfoot.
			here = true;
			if (!collapse)
			{
				ClearGoals();
				AddHeapGoals(s);
				if (_goals.Count > 0
					&& _pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out _))
					here = _path.Count == 0;
			}
		}

		var carried = (Material)s.GoblinCarry[goblin];
		var dropTile = -1;
		if (carried != Material.None && !TryFindDropTile(s, goblin, carried, out dropTile))
			return false;

		if (collapse)
			GoblinTasks.BeginCollapse(s, goblin, dropTile);
		else if (supply == Material.None)
			GoblinTasks.BeginSleep(s, goblin, dropTile, asleep: here);
		else
			GoblinTasks.BeginSeek(s, goblin, dropTile, eating: here, supply);
		// The unit put down is a new loose Haul, or has taken a Pile tile's last slot of its material.
		if (carried != Material.None)
			RecheckReachability(s);
		return true;
	}

	// Where a carried unit lands: the Pile tile underfoot if it has room, the goblin's own tile if that is not a Pile,
	// otherwise the nearest walkable tile that can legally take it (a Pile tile with room, or any tile that is not a
	// Pile), by path length, ties to the lowest tile index. False when no such tile is in reach.
	bool TryFindDropTile(SimState s, int goblin, Material material, out int tile)
	{
		var here = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (!s.IsPile(here) || s.PileHasRoom(here, material))
		{
			tile = here;
			return true;
		}

		ClearGoals();
		for (var candidate = 0; candidate < s.Piles.Length; candidate++)
			if (candidate != here && TileRules.IsWalkable(s.KindAt(candidate))
				&& (!s.IsPile(candidate) || s.PileHasRoom(candidate, material)))
				AddGoal(candidate, candidate);
		if (!_pathfinder.TryFindPath(s, here, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out var goalIndex))
		{
			tile = -1;
			return false;
		}
		tile = _goals[goalIndex];
		return true;
	}

	// Every tile holding a unit of the supply (Grub or Grog), stored or loose, as a goal. Goals go in tile order, so a tie goes to the lowest tile index.
	void AddSupplyGoals(SimState s, Material supply)
	{
		for (var tile = 0; tile < s.Piles.Length; tile++)
			if (s.ItemCount(tile, supply) > 0)
				AddGoal(tile, tile);
	}

	// Every Sleeping Heap tile as a goal, in tile order, so a tie goes to the lowest tile index.
	void AddHeapGoals(SimState s)
	{
		for (var tile = 0; tile < s.Rooms.Length; tile++)
			if (s.Rooms[tile] == (byte)RoomKind.SleepingHeap)
				AddGoal(tile, tile);
	}

	// Walks toward the nearest Sleeping Heap tile, found afresh at every step so a Heap painted or removed on the way changes the goal.
	// The goblin lies down once it stands on one, and where it stands if no Heap is left in reach: the floor is always a bed.
	void SeekHeap(SimState s, int goblin)
	{
		if (!StepDue(s, goblin))
			return;

		ClearGoals();
		AddHeapGoals(s);
		if (MoveToward(s, goblin) != Move.Moved)
			GoblinTasks.StartSleeping(s, goblin);
	}

	// Walks toward the nearest unit of what the goblin seeks, found afresh at every step so a unit eaten, drunk or hauled off
	// changes the goal. The goblin eats or drinks once it stands on one. With none left in reach it gives up and goes back to its work.
	void Seek(SimState s, int goblin)
	{
		if (!StepDue(s, goblin))
			return;

		var task = (GoblinTask)s.GoblinTask[goblin];
		ClearGoals();
		AddSupplyGoals(s, GoblinTasks.SupplyOf(task));
		switch (MoveToward(s, goblin))
		{
			case Move.NoPath:
				GoblinTasks.Release(s, goblin);
				break;
			case Move.Here:
			case Move.Arrived:
				if (task == GoblinTask.Seeking)
					GoblinTasks.StartEating(s, goblin);
				else
					GoblinTasks.StartDrinking(s, goblin);
				break;
		}
	}

	// The unit is consumed as the meal or drink ends, so when two goblins take the last one the first to finish has it. Settle
	// frees the other, which finds it gone and looks again. A Pile tile that was full of the material has room again.
	void Consume(SimState s, int goblin)
	{
		if (!GoblinTasks.EatTick(s, goblin))
			return;

		var tile = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		var task = (GoblinTask)s.GoblinTask[goblin];
		var supply = GoblinTasks.SupplyOf(task);
		var wasFull = s.IsPile(tile) && !s.PileHasRoom(tile, supply);
		if (task == GoblinTask.Eating)
			GoblinTasks.FinishEating(s, goblin);
		else
			GoblinTasks.FinishDrinking(s, goblin);
		RoomRules.Settle(s);
		if (wasFull)
			RecheckReachability(s);
	}

	// Claims the nearest reachable Job of either kind: dig goals are the tiles a goblin can dig from, haul goals are
	// the loose units themselves. Goals go in tile order, so a tie goes to the lowest tile index.
	bool TryClaim(SimState s, int goblin)
	{
		ClearGoals();
		CountBreweries(s);
		for (var tile = 0; tile < s.Designations.Length; tile++)
		{
			if (s.IsReachable(tile) && !IsClaimed(s, tile, JobKind.Dig))
				AddStandingTiles(s, tile);
			if (s.IsHaulReachable(tile) && !IsClaimed(s, tile, JobKind.Haul))
				AddGoal(tile, tile, JobKind.Haul);
			// A Grub Pit tile some goblin can path to; the pathfinder weighs the walk for this goblin.
			if (s.HaulRegion[tile] != 0 && FarmSystem.IsTendOffered(s, tile) && !IsClaimed(s, tile, JobKind.Tend))
				AddGoal(tile, tile, JobKind.Tend);
			// A tile holding Fungus no one has claimed, with a free Brewery tile and room for the Grog in its region.
			if (IsBrewOffered(s, tile))
				AddGoal(tile, tile, JobKind.Brew);
		}

		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (!_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out var goalIndex))
			return false;

		switch (_goalKinds[goalIndex])
		{
			case JobKind.Haul:
				GoblinTasks.BeginFetch(s, goblin, _goalJobs[goalIndex], pickingUp: _path.Count == 0);
				break;
			case JobKind.Tend:
				GoblinTasks.BeginTend(s, goblin, _goalJobs[goalIndex], tending: _path.Count == 0);
				break;
			case JobKind.Brew:
				GoblinTasks.BeginBrew(s, goblin, _goalJobs[goalIndex], taking: _path.Count == 0);
				break;
			default:
				GoblinTasks.BeginJob(s, goblin, _goalJobs[goalIndex], digging: _path.Count == 0);
				break;
		}
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

	// A Tend ends the moment its Job goes: the tile gained loose Grub, or stopped being a Grub Pit that is free to tend. The goblin is
	// freed with no progress, and the Job is offered again whenever it exists again.
	void Tend(SimState s, int goblin)
	{
		var tile = s.GoblinJobTile[goblin];
		if (!FarmSystem.IsTendOffered(s, tile))
		{
			GoblinTasks.Release(s, goblin);
			return;
		}

		if ((GoblinTask)s.GoblinTask[goblin] == GoblinTask.Tending)
		{
			if (!GoblinTasks.TendTick(s, goblin))
				return;
			FarmSystem.StartSpawning(s, tile);
			GoblinTasks.Release(s, goblin);
			return;
		}

		if (!StepDue(s, goblin))
			return;
		ClearGoals();
		AddGoal(tile, tile, JobKind.Tend);
		switch (MoveToward(s, goblin))
		{
			case Move.NoPath:
				GoblinTasks.Release(s, goblin);
				break;
			case Move.Here:
			case Move.Arrived:
				GoblinTasks.StartTending(s, goblin);
				break;
		}
	}

	// Counts, per region, the Brewery tiles no goblin has claimed (not Bringing Fungus to, nor Brewing at) and the goblins who have
	// claimed Fungus to brew and will each need one. A Brew is offered while the first exceeds the second.
	void CountBreweries(SimState s)
	{
		Array.Clear(_freeBreweries);
		Array.Clear(_gatherers);
		for (var tile = 0; tile < s.Rooms.Length; tile++)
			if (s.Rooms[tile] == (byte)RoomKind.Brewery)
				_freeBreweries[s.HaulRegion[tile]]++;
		for (var g = 0; g < s.GoblinCount; g++)
		{
			var task = (GoblinTask)s.GoblinTask[g];
			if (GoblinTasks.IsBreweryTask(task))
				_freeBreweries[s.HaulRegion[s.GoblinJobTile[g]]]--;
			else if (GoblinTasks.IsBrewSourceTask(task))
				_gatherers[s.HaulRegion[s.GoblinJobTile[g]]]++;
		}
	}

	// A Brew Job on a tile: Fungus no one has claimed, a Brewery tile in its region that no brewer has claimed and no gatherer is counting
	// on, and a Pile in its region with room for the Grog. The region is what a goblin who can reach the Fungus can reach.
	bool IsBrewOffered(SimState s, int tile)
	{
		var fungus = s.ItemCount(tile, Material.Fungus);
		if (fungus == 0)
			return false;
		var region = s.HaulRegion[tile];
		return region != 0
			&& _freeBreweries[region] - _gatherers[region] > 0
			&& GoblinTasks.SupplyClaims(s, tile, s.GoblinCount) < fungus
			&& s.HasStorageRoom(tile, Material.Grog);
	}

	// Walks to the claimed Fungus. The walk ends where the Job does: the Fungus gone, no Pile with room for the Grog, or no free Brewery.
	void Gather(SimState s, int goblin)
	{
		if (!StepDue(s, goblin))
			return;

		var tile = s.GoblinJobTile[goblin];
		CountBreweries(s);
		if (s.ItemCount(tile, Material.Fungus) == 0 || !s.HasStorageRoom(tile, Material.Grog) || _freeBreweries[s.HaulRegion[tile]] <= 0)
		{
			GoblinTasks.Release(s, goblin);
			return;
		}

		ClearGoals();
		AddGoal(tile, tile, JobKind.Brew);
		switch (MoveToward(s, goblin))
		{
			case Move.NoPath:
				GoblinTasks.Release(s, goblin);
				break;
			case Move.Here:
			case Move.Arrived:
				GoblinTasks.StartTaking(s, goblin);
				break;
		}
	}

	// The unit leaves its tile as the taking ends, when the goblin also claims the nearest free Brewery tile. If the Fungus has gone, or
	// there is nowhere to brew it or put the Grog, the goblin takes nothing and is freed. A Pile that was full of Fungus has room again.
	void Take(SimState s, int goblin)
	{
		if (!GoblinTasks.WorkTick(s, goblin))
			return;

		var tile = s.GoblinJobTile[goblin];
		if (s.ItemCount(tile, Material.Fungus) == 0
			|| !s.HasStorageRoom(tile, Material.Grog)
			|| !TryFindBrewery(s, goblin, out var brewery, out var here))
		{
			GoblinTasks.Release(s, goblin);
			return;
		}

		var wasFull = s.IsPile(tile) && !s.PileHasRoom(tile, Material.Fungus);
		s.RemoveItems(tile, Material.Fungus, 1);
		GoblinTasks.FinishTake(s, goblin, brewery, brewing: here);
		RoomRules.Settle(s);
		if (wasFull)
			RecheckReachability(s);
	}

	// The nearest Brewery tile no brewer has claimed, by path length, ties to the lowest tile index.
	bool TryFindBrewery(SimState s, int goblin, out int tile, out bool here)
	{
		ClearGoals();
		for (var candidate = 0; candidate < s.Rooms.Length; candidate++)
			if (s.Rooms[candidate] == (byte)RoomKind.Brewery && !GoblinTasks.IsBreweryClaimed(s, candidate))
				AddGoal(candidate, candidate, JobKind.Brew);
		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (!_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out var goalIndex))
		{
			tile = -1;
			here = false;
			return false;
		}
		tile = _goals[goalIndex];
		here = _path.Count == 0;
		return true;
	}

	// Walks the Fungus to the claimed Brewery tile. A way that is gone, or a Brewery that is, leaves it carrying the Fungus to a Pile.
	void Bring(SimState s, int goblin)
	{
		if (!StepDue(s, goblin))
			return;

		var tile = s.GoblinJobTile[goblin];
		ClearGoals();
		AddGoal(tile, tile, JobKind.Brew);
		switch (MoveToward(s, goblin))
		{
			case Move.NoPath:
				GoblinTasks.ResumeCarrying(s, goblin);
				break;
			case Move.Here:
			case Move.Arrived:
				GoblinTasks.StartBrewing(s, goblin);
				break;
		}
	}

	// Twenty working ticks, then the Fungus is one Grog, loose on the Brewery tile. Reachability is derived: a tile some goblin can path
	// to is a Haul as soon as it holds a loose unit.
	void Brew(SimState s, int goblin)
	{
		if (!GoblinTasks.BrewTick(s, goblin))
			return;

		var tile = s.GoblinJobTile[goblin];
		GoblinTasks.FinishBrew(s, goblin);
		if (s.HaulRegion[tile] != 0)
			s.HaulReachable[tile] = 1;
	}

	void Fetch(SimState s, int goblin)
	{
		if (!StepDue(s, goblin))
			return;

		// Storage that filled or went since the claim leaves nothing worth the walk.
		if (s.HaulableTotal(s.GoblinJobTile[goblin]) == 0)
		{
			GoblinTasks.Release(s, goblin);
			return;
		}

		ClearGoals();
		AddGoal(s.GoblinJobTile[goblin], s.GoblinJobTile[goblin], JobKind.Haul);
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

		// The unit taken is the first material that has storage to go to, so a blocked material waits while another is hauled.
		var tile = s.GoblinJobTile[goblin];
		var material = s.HaulableMaterial(tile);
		if (material == Material.None)
		{
			GoblinTasks.Release(s, goblin);
			return;
		}

		s.RemoveItems(tile, material, 1);
		GoblinTasks.FinishPickUp(s, goblin, material);
		// A goblin eating or drinking the last unit, or gathering the Fungus, has nothing left to take.
		if (material is Material.Grub or Material.Grog or Material.Fungus)
			RoomRules.Settle(s);
	}

	// Walks to the nearest Pile tile with room for the carried material, by path length, ties to the lowest tile index. The
	// route is found afresh at every step, so a Pile painted, removed or filled on the way changes the goal. With no such
	// Pile in reach the goblin keeps its unit and waits where it stands, taking no other Job, and tries again each step.
	void Carry(SimState s, int goblin)
	{
		if (!StepDue(s, goblin))
			return;

		var material = (Material)s.GoblinCarry[goblin];
		ClearGoals();
		for (var tile = 0; tile < s.Piles.Length; tile++)
			if (s.PileHasRoom(tile, material))
				AddGoal(tile, tile);
		switch (MoveToward(s, goblin))
		{
			case Move.Here:
			case Move.Arrived:
				GoblinTasks.StartDropping(s, goblin);
				break;
		}
	}

	// There are no reservations, so room is checked again as the drop completes: the unit is stored if the tile is still a
	// Pile with room for it, and otherwise the goblin keeps it and looks for storage again. The goblin that fills a
	// material's last slot makes every loose unit and carrier of that material wait, so reachability is rebuilt.
	void Drop(SimState s, int goblin)
	{
		if (!GoblinTasks.WorkTick(s, goblin))
			return;

		var tile = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		var material = (Material)s.GoblinCarry[goblin];
		if (!s.PileHasRoom(tile, material))
		{
			GoblinTasks.ResumeCarrying(s, goblin);
			return;
		}

		GoblinTasks.FinishDrop(s, goblin);
		if (!s.PileHasRoom(tile, material))
			RecheckReachability(s);
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
		// Only Floor can hold a Room; a Pile's stock stays on the tile, loose.
		var hadRoom = s.RoomAt(job) != RoomKind.None;
		RoomRules.Clear(s, job);
		s.AddItems(job, material, 1);
		var layer = s.Width * s.Height;
		events.Add(new TileDug(job % s.Width, job / s.Width % s.Height, job / layer, material));
		GoblinTasks.Release(s, goblin);
		if (hadRoom)
			RoomRules.Settle(s);
		RecheckReachability(s);
	}

	// A dig or a Tend is claimed by one goblin; a tile's loose units are claimed one goblin each, so it is claimed once all are.
	static bool IsClaimed(SimState s, int tile, JobKind kind)
	{
		if (kind == JobKind.Haul)
			return GoblinTasks.SupplyClaims(s, tile, s.GoblinCount) >= s.HaulableTotal(tile);
		for (var g = 0; g < s.GoblinCount; g++)
		{
			var task = (GoblinTask)s.GoblinTask[g];
			if (s.GoblinJobTile[g] == tile && (kind == JobKind.Tend ? GoblinTasks.IsTendTask(task) : GoblinTasks.IsDigTask(task)))
				return true;
		}
		return false;
	}

	void ClearGoals()
	{
		_goals.Clear();
		_goalJobs.Clear();
		_goalKinds.Clear();
	}

	void AddGoal(int stand, int job, JobKind kind = JobKind.Dig)
	{
		_goals.Add(stand);
		_goalJobs.Add(job);
		_goalKinds.Add(kind);
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
			AddGoal(stand, job);
	}
}
