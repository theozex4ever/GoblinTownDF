namespace Goblins.Sim;

// Thefts: the acting-out behaviour that takes one communal unit. The Task transitions are GoblinTasks'; this is the choosing and the walking.
public sealed partial class JobSystem
{
	// The nearest Pile tile holding Grub or Grog that a path leads to, by walking distance, ties to the lowest tile index; Grog if it holds both. Theft is only
	// available when a destination for it exists too, so a goblin is never sent for a unit it has nowhere to eat. Nothing is reserved.
	bool TryFindTheft(SimState s, int goblin, out int source, out Material supply, out int destination)
	{
		source = -1;
		supply = Material.None;
		destination = -1;
		ClearGoals();
		for (var tile = 0; tile < s.Piles.Length; tile++)
			if (s.IsPile(tile) && (s.ItemCount(tile, Material.Grog) > 0 || s.ItemCount(tile, Material.Grub) > 0))
				AddGoal(tile, tile);
		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (_goals.Count == 0
			|| !_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out var goalIndex))
			return false;

		source = _goals[goalIndex];
		supply = s.ItemCount(source, Material.Grog) > 0 ? Material.Grog : Material.Grub;
		return TryFindDestination(s, source, out destination);
	}

	// The farthest tile that is not a Pile within the theft range of the source, on its level, ties to the lowest tile index. The tiles are tried in
	// index order and only a strictly longer walk replaces the best, which is what makes the tie go to the lowest.
	bool TryFindDestination(SimState s, int source, out int destination)
	{
		const int range = ChaosRules.TheftRange;
		destination = -1;
		var layer = s.Width * s.Height;
		var z = source / layer;
		var sx = source % s.Width;
		var sy = source / s.Width % s.Height;
		var farthest = 0;
		for (var y = Math.Max(0, sy - range); y <= Math.Min(s.Height - 1, sy + range); y++)
		for (var x = Math.Max(0, sx - range); x <= Math.Min(s.Width - 1, sx + range); x++)
		{
			var steps = Math.Abs(x - sx) + Math.Abs(y - sy);
			if (steps == 0 || steps > range)
				continue;
			var tile = s.TileIndex(x, y, z);
			if (!TileRules.IsWalkable(s.KindAt(tile)) || s.IsPile(tile))
				continue;

			_goals.Clear();
			_goals.Add(tile);
			if (!_pathfinder.TryFindPath(s, source, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out _)
				|| _path.Count > range || _path.Count <= farthest)
				continue;
			farthest = _path.Count;
			destination = tile;
		}
		return destination >= 0;
	}

	// Before the pickup a plan holds while its source is still a Pile holding the unit it chose and its destination is still a tile that is not a Pile. Nothing is
	// reserved, so anyone may have taken the unit, and painting may have changed either tile; a plan that no longer holds fails, and no other is chosen.
	static bool PlanHolds(SimState s, int goblin)
	{
		var source = s.GoblinJobTile[goblin];
		return s.IsPile(source) && s.ItemCount(source, (Material)s.GoblinTheftSupply[goblin]) > 0
			&& DestinationHolds(s, goblin);
	}

	static bool DestinationHolds(SimState s, int goblin)
	{
		var destination = s.GoblinTheftDestination[goblin];
		return TileRules.IsWalkable(s.KindAt(destination)) && !s.IsPile(destination);
	}

	// True when a path leads from <paramref name="from"/> to the destination.
	bool CanReachDestination(SimState s, int goblin, int from)
	{
		_goals.Clear();
		_goals.Add(s.GoblinTheftDestination[goblin]);
		return _pathfinder.TryFindPath(s, from, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out _);
	}

	// A theft that has failed ends with no relief and its cooldown. Before the pickup nothing has moved. After it the unit goes down by the rules for a Craving: on the Pile
	// underfoot if that has room, on the goblin's own tile if that is no Pile, otherwise on the nearest tile that can legally take it. With no such tile the goblin keeps the
	// unit and stalls, and the cooldown starts when the unit is finally put down.
	void FailTheft(SimState s, int goblin)
	{
		var carried = (Material)s.GoblinCarry[goblin];
		if (carried == Material.None)
		{
			GoblinTasks.EndTheft(s, goblin, dropTile: -1);
			return;
		}
		if (!TryFindDropTile(s, goblin, carried, out var dropTile))
		{
			GoblinTasks.StallTheft(s, goblin);
			return;
		}
		GoblinTasks.EndTheft(s, goblin, dropTile);
		// The unit put down is a new loose Haul, or has taken a Pile tile's last slot of its material.
		RecheckReachability(s);
	}

	// Walks to the Pile tile the thief set out for, found afresh at every step. It starts picking up on arrival.
	void Sneak(SimState s, int goblin)
	{
		if (!PlanHolds(s, goblin))
		{
			FailTheft(s, goblin);
			return;
		}
		if (!StepDue(s, goblin))
			return;

		var here = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (!CanReachDestination(s, goblin, here))
		{
			FailTheft(s, goblin);
			return;
		}

		var source = s.GoblinJobTile[goblin];
		ClearGoals();
		AddGoal(source, source);
		switch (MoveToward(s, goblin))
		{
			case Move.NoPath:
				FailTheft(s, goblin);
				break;
			case Move.Here:
			case Move.Arrived:
				GoblinTasks.StartPilfering(s, goblin);
				break;
		}
	}

	// Five working ticks, then the unit leaves the Pile: this is the one moment a theft takes anything from the shared stock. A Pile that was full of the supply
	// has room again, and a goblin eating or drinking the last unit has nothing left to take.
	void Pilfer(SimState s, int goblin, List<SimEvent> events)
	{
		if (!PlanHolds(s, goblin))
		{
			FailTheft(s, goblin);
			return;
		}
		if (!GoblinTasks.WorkTick(s, goblin))
			return;

		var source = s.GoblinJobTile[goblin];
		if (!CanReachDestination(s, goblin, source))
		{
			FailTheft(s, goblin);
			return;
		}

		var supply = (Material)s.GoblinTheftSupply[goblin];
		var wasFull = !s.PileHasRoom(source, supply);
		s.RemoveItems(source, supply, 1);
		GoblinTasks.FinishPilfer(s, goblin);
		events.Add(new TheftPickedUp(goblin, supply));
		RoomRules.Settle(s);
		if (wasFull)
			RecheckReachability(s);
	}

	// Walks the unit to the destination chosen at the start, one step every 3 ticks (6 when Starving), and begins consuming it on arrival.
	void MakeOff(SimState s, int goblin)
	{
		if (!DestinationHolds(s, goblin))
		{
			FailTheft(s, goblin);
			return;
		}
		if (!StepDue(s, goblin))
			return;

		var destination = s.GoblinTheftDestination[goblin];
		ClearGoals();
		AddGoal(destination, destination);
		switch (MoveToward(s, goblin))
		{
			case Move.NoPath:
				FailTheft(s, goblin);
				break;
			case Move.Here:
			case Move.Arrived:
				GoblinTasks.StartScoffing(s, goblin);
				break;
		}
	}

	// Ten working ticks, then the unit in the goblin's hands is consumed. Sim time: Starving does not slow it.
	void Scoff(SimState s, int goblin)
	{
		if (!DestinationHolds(s, goblin))
		{
			FailTheft(s, goblin);
			return;
		}
		if (GoblinTasks.ScoffTick(s, goblin))
			GoblinTasks.FinishScoff(s, goblin);
	}

	// A stalled thief looks for a legal place to put its unit down on every tick, which is how it notices a Pile gaining room or being removed, and consumes nothing meanwhile.
	void Stall(SimState s, int goblin)
	{
		var carried = (Material)s.GoblinCarry[goblin];
		if (!TryFindDropTile(s, goblin, carried, out var dropTile))
			return;
		GoblinTasks.EndTheft(s, goblin, dropTile);
		RecheckReachability(s);
	}
}
