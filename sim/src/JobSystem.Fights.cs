namespace Goblins.Sim;

// Fights: the acting-out behaviour that needs a second goblin. The Task transitions are GoblinTasks'; this is the choosing, the walking and the contact.
public sealed partial class JobSystem
{
	// Per goblin, scratch for target choice: whether it is eligible to be set upon.
	readonly bool[] _eligible = new bool[SimState.MaxGoblins];

	// A fight stage ends on the tick its clock reaches 0: a pursuit that found no one, a scuffle that ran its course, a rest that is over. A scuffle is ended
	// from its instigator, which ends it for the victim too, so the victim's own turn here is a no-op. Needs come first: a Craving that can be met, or a Collapse,
	// that has arisen by a scuffle's last tick ends it for both with no relief and no recovery, as it would a tick earlier, so both fighters get their chance at
	// it, in index order, before the scuffle is allowed to have run its course.
	void EndDueFights(SimState s)
	{
		for (var g = 0; g < s.GoblinCount; g++)
		{
			if (s.GoblinFightTicks[g] != 0)
				continue;
			switch ((GoblinTask)s.GoblinTask[g])
			{
				case GoblinTask.Pursuing:
					GoblinTasks.FailPursuit(s, g);
					break;
				case GoblinTask.Scuffling:
					var partner = s.GoblinFightPartner[g];
					TryCraving(s, Math.Min(g, partner));
					TryCraving(s, Math.Max(g, partner));
					// A Need met by either one has already ended the scuffle for both.
					if ((GoblinTask)s.GoblinTask[g] == GoblinTask.Scuffling)
						GoblinTasks.FinishScuffle(s, g);
					break;
				case GoblinTask.Recovering:
					GoblinTasks.FinishRecovery(s, g);
					break;
			}
		}
	}

	// A goblin that can be set upon: awake, free of Needs and of any act, out of its cooldown, and either empty-handed or able to put its unit down legally.
	// <paramref name="dropTile"/> is where its unit would go, or -1 when it holds none.
	bool CanBeSetUpon(SimState s, int goblin, out int dropTile)
	{
		dropTile = -1;
		var task = (GoblinTask)s.GoblinTask[goblin];
		if (GoblinTasks.IsSatisfyingTask(task) || GoblinTasks.IsActingOutTask(task) || s.GoblinActCooldown[goblin] != 0)
			return false;
		var carried = (Material)s.GoblinCarry[goblin];
		return carried == Material.None || TryFindDropTile(s, goblin, carried, out dropTile);
	}

	// The nearest goblin on the instigator's own level that can be set upon and that a path leads to, by walking distance, ties to the lowest index.
	bool TryFindTarget(SimState s, int instigator, out int target)
	{
		target = -1;
		var z = s.GoblinZ[instigator];
		// Judged first, because the legal-drop search borrows the goal list that the path search below needs.
		var any = false;
		for (var g = 0; g < s.GoblinCount; g++)
		{
			_eligible[g] = g != instigator && s.GoblinZ[g] == z && CanBeSetUpon(s, g, out _);
			any |= _eligible[g];
		}
		if (!any)
			return false;

		ClearGoals();
		for (var g = 0; g < s.GoblinCount; g++)
			if (_eligible[g])
				AddGoal(s.TileIndex(s.GoblinX[g], s.GoblinY[g], z), g);
		var start = s.TileIndex(s.GoblinX[instigator], s.GoblinY[instigator], z);
		if (!_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out var goalIndex))
			return false;
		target = _goalJobs[goalIndex];
		return true;
	}

	// One turn of a pursuit: make contact if the target is on or beside the instigator's tile, otherwise take the next step toward it, which a Starving goblin
	// does at half pace, and make contact if that step ended beside it. No path to the target ends the pursuit. The pursuit's clock is not here: see EndDueFights.
	void Pursue(SimState s, int goblin, List<SimEvent> events)
	{
		if (TryContact(s, goblin, events))
			return;
		if (!NeedRules.ActsThisTick(s.GoblinGrub[goblin], s.Tick) || !StepDue(s, goblin))
			return;

		var target = s.GoblinFightPartner[goblin];
		ClearGoals();
		AddGoal(s.TileIndex(s.GoblinX[target], s.GoblinY[target], s.GoblinZ[target]), target);
		if (MoveToward(s, goblin) == Move.NoPath)
		{
			GoblinTasks.FailPursuit(s, goblin);
			return;
		}
		TryContact(s, goblin, events);
	}

	// Contact is the same tile or an orthogonal neighbour on one level. It sets the target upon if it can still be, and otherwise ends the attempt
	// without choosing another. True when contact was made, whichever way it went.
	bool TryContact(SimState s, int goblin, List<SimEvent> events)
	{
		var target = s.GoblinFightPartner[goblin];
		if (s.GoblinZ[goblin] != s.GoblinZ[target]
			|| Math.Abs(s.GoblinX[goblin] - s.GoblinX[target]) + Math.Abs(s.GoblinY[goblin] - s.GoblinY[target]) > 1)
			return false;

		if (!CanBeSetUpon(s, target, out var dropTile))
		{
			GoblinTasks.FailPursuit(s, goblin);
			return true;
		}

		GoblinTasks.BeginScuffle(s, goblin, target, dropTile);
		events.Add(new ScuffleStarted(goblin, target));
		// The unit put down is a new loose Haul, or has taken a Pile tile's last slot of its material.
		if (dropTile >= 0)
			RecheckReachability(s);
		return true;
	}
}
