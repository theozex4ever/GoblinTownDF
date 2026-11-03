namespace Goblins.Sim;

/// <summary>
/// The global job list and the goblins that work it. A designated tile is a dig Job, a tile with a loose
/// unit is a Haul Job and an untended Grub Pit tile is a Tend Job. An idle goblin claims the nearest one of any kind by path length (ties go to the
/// lowest tile index). It walks to a tile it can dig from and digs, or walks to the unit, picks it up (5 ticks),
/// carries it to the nearest Pile tile with room for its material and drops it (5 ticks); with no such tile it waits holding
/// the unit; or walks to the Grub Pit tile and tends it (20 ticks), which starts the tile spawning Grub. A goblin with no reachable Job wanders instead.
/// A goblin with a Craving for Grub or Grog that can reach a unit of it drops everything and goes to eat or drink it, whatever
/// it was doing. With both, the lower meter wins (Grub on a tie), and a Craving with no unit in reach does not count.
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
	}

	readonly IPathfinder _pathfinder;
	readonly List<int> _path = [];
	readonly List<int> _goals = [];
	readonly List<int> _goalJobs = [];
	// Parallel to _goals while a claim is being weighed: which kind of Job the goal belongs to.
	readonly List<JobKind> _goalKinds = [];
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
			// A Craving comes before any Job, and before a Starving goblin sits a tick out. Eating and drinking are not slowed.
			TryCraving(s, g);
			if (!GoblinTasks.IsConsumingTask((GoblinTask)s.GoblinTask[g]) && !NeedRules.ActsThisTick(s.GoblinGrub[g], s.Tick))
				continue;

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
				case GoblinTask.Approaching:
				case GoblinTask.Tending:
					Tend(s, g);
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
			}
		}
	}

	// A goblin with a Craving drops its Job at once when it can reach a unit of what it craves, Pile or loose. With Cravings for
	// both, the lower meter is tried first (Grub on a tie) and a Craving with nothing in reach is passed over for the other.
	// Anything carried is put down first, where a legal drop is found; a carrier with nowhere to put its unit keeps working.
	// With nothing in reach nothing happens, and the goblin looks again next tick, which is how it notices supply or
	// reachability changing. A goblin already satisfying a Need finishes it before it considers another.
	void TryCraving(SimState s, int goblin)
	{
		var task = (GoblinTask)s.GoblinTask[goblin];
		if (GoblinTasks.IsSeekingTask(task) || GoblinTasks.IsConsumingTask(task))
			return;

		var first = NeedRules.WinningNeed(s.GoblinGrub[goblin], s.GoblinGrog[goblin]);
		if (!TryCraving(s, goblin, first))
			TryCraving(s, goblin, NeedRules.Other(first));
	}

	// True when the goblin set out for a unit of the supply.
	bool TryCraving(SimState s, int goblin, Material supply)
	{
		if (!NeedRules.IsCraving(s.MeterOf(supply)[goblin]))
			return false;

		ClearGoals();
		AddSupplyGoals(s, supply);
		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (_goals.Count == 0
			|| !_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out _))
			return false;

		var onSupply = _path.Count == 0;
		var carried = (Material)s.GoblinCarry[goblin];
		var dropTile = -1;
		if (carried != Material.None && !TryFindDropTile(s, goblin, carried, out dropTile))
			return false;

		GoblinTasks.BeginSeek(s, goblin, dropTile, eating: onSupply, supply);
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
		for (var tile = 0; tile < s.Designations.Length; tile++)
		{
			if (s.IsReachable(tile) && !IsClaimed(s, tile, JobKind.Dig))
				AddStandingTiles(s, tile);
			if (s.IsHaulReachable(tile) && !IsClaimed(s, tile, JobKind.Haul))
				AddGoal(tile, tile, JobKind.Haul);
			// A Grub Pit tile some goblin can path to; the pathfinder weighs the walk for this goblin.
			if (s.HaulRegion[tile] != 0 && FarmSystem.IsTendOffered(s, tile) && !IsClaimed(s, tile, JobKind.Tend))
				AddGoal(tile, tile, JobKind.Tend);
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
		// A goblin eating or drinking the last unit has nothing left to take.
		if (material is Material.Grub or Material.Grog)
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
			return GoblinTasks.FetchClaims(s, tile, s.GoblinCount) >= s.HaulableTotal(tile);
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
