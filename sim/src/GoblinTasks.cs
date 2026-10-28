namespace Goblins.Sim;

/// <summary>
/// The one owner of every goblin Task and what it requires of the per-goblin arrays (ADR 0002). The
/// transitions are the only code that changes <see cref="SimState.GoblinTask"/>,
/// <see cref="SimState.GoblinJobTile"/>, <see cref="SimState.GoblinWanderTile"/> and
/// <see cref="SimState.GoblinWanderTicks"/>; <see cref="FindViolation"/> states the rules they keep.
/// A new Task is added here and nowhere else. Movement along a path stays in <see cref="JobSystem"/>,
/// which only counts <see cref="SimState.GoblinStepTicks"/>.
/// </summary>
public static class GoblinTasks
{
	/// <summary>Ticks a goblin spends on each step of a walk.</summary>
	public const int WalkTicksPerTile = 3;
	/// <summary>An idle goblin pauses this many ticks, drawn from the Rng, before each wander: 3–6 seconds.</summary>
	public const int MinWanderPauseTicks = 30;
	public const int MaxWanderPauseTicks = 60;

	/// <summary>
	/// Idle or Wandering → Walking to the claimed Job tile, or Digging when the goblin already stands where
	/// it can dig it. Drops any wander.
	/// </summary>
	public static void BeginJob(SimState s, int g, int jobTile, bool digging)
	{
		s.GoblinJobTile[g] = jobTile;
		s.GoblinStepTicks[g] = 0;
		s.GoblinWanderTile[g] = -1;
		s.GoblinWanderTicks[g] = 0;
		s.GoblinTask[g] = (byte)(digging ? GoblinTask.Digging : GoblinTask.Walking);
	}

	/// <summary>Walking → Digging, on arrival at a tile the goblin can dig its Job from.</summary>
	public static void StartDigging(SimState s, int g)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.Digging;
	}

	/// <summary>Idle → Wandering to <paramref name="target"/>, a walkable tile on the goblin's own level.</summary>
	public static void BeginWander(SimState s, int g, int target)
	{
		s.GoblinWanderTile[g] = target;
		s.GoblinWanderTicks[g] = 0;
		s.GoblinStepTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.Wandering;
	}

	/// <summary>Any Task → Idle, with no Job and no wander. The Job, if any, stays on the job list.</summary>
	public static void Release(SimState s, int g)
	{
		s.GoblinTask[g] = (byte)GoblinTask.Idle;
		s.GoblinJobTile[g] = -1;
		s.GoblinStepTicks[g] = 0;
		s.GoblinWanderTile[g] = -1;
		s.GoblinWanderTicks[g] = 0;
	}

	/// <summary>
	/// One tick of an Idle goblin's pause before it wanders. The pause (<see cref="MinWanderPauseTicks"/> to
	/// <see cref="MaxWanderPauseTicks"/>) is drawn from <see cref="SimState.Rng"/> on first use. True on the tick
	/// it runs out, which is when the goblin should pick a wander target.
	/// </summary>
	public static bool CountDownPause(SimState s, int g)
	{
		if (s.GoblinWanderTicks[g] == 0)
			s.GoblinWanderTicks[g] = (byte)(MinWanderPauseTicks + s.Rng.NextInt(MaxWanderPauseTicks - MinWanderPauseTicks + 1));
		return --s.GoblinWanderTicks[g] == 0;
	}

	/// <summary>
	/// The first broken Task rule, or null when every goblin's Task fits its fields and the world: the field
	/// shape of each Task, and the world rules (a claimed tile is designated, a Digging goblin stands where it
	/// can dig, a wander target is walkable on the goblin's own level and not its own tile, no two goblins
	/// claim one tile, step ticks are below <see cref="WalkTicksPerTile"/>, an idle pause is at most
	/// <see cref="MaxWanderPauseTicks"/>).
	/// Precondition: the state is structurally sound: array lengths match the map and goblin cap, tile, material
	/// and designation values are defined, and every goblin stands on the map.
	/// </summary>
	public static string? FindViolation(SimState s)
	{
		var tiles = s.TileKinds.Length;
		var layer = s.Width * s.Height;
		for (var g = 0; g < s.GoblinCount; g++)
		{
			var task = (GoblinTask)s.GoblinTask[g];
			if (!Enum.IsDefined(task))
				return $"Goblin {g} has unknown task {s.GoblinTask[g]}.";
			if (s.GoblinStepTicks[g] >= WalkTicksPerTile)
				return $"Goblin {g} has step ticks {s.GoblinStepTicks[g]}.";

			var job = s.GoblinJobTile[g];
			var wanderTile = s.GoblinWanderTile[g];
			var wanderTicks = s.GoblinWanderTicks[g];
			switch (task)
			{
				case GoblinTask.Idle:
					if (job != -1)
						return $"Goblin {g} is not working but holds job tile {job}.";
					if (wanderTile != -1)
						return $"Goblin {g} is idle but has wander tile {wanderTile}.";
					if (wanderTicks > MaxWanderPauseTicks)
						return $"Goblin {g} pauses {wanderTicks} ticks before wandering.";
					break;

				case GoblinTask.Wandering:
					if (job != -1)
						return $"Goblin {g} is not working but holds job tile {job}.";
					if (wanderTicks != 0)
						return $"Goblin {g} is wandering but still pauses {wanderTicks} ticks.";
					if (!(wanderTile >= 0 && wanderTile < tiles
						&& TileRules.IsWalkable(s.KindAt(wanderTile))
						&& wanderTile / layer == s.GoblinZ[g]
						&& wanderTile != s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g])))
						return $"Goblin {g} wanders to tile {wanderTile}, which is not a walkable tile on its own level.";
					break;

				case GoblinTask.Walking:
				case GoblinTask.Digging:
					if (wanderTile != -1 || wanderTicks != 0)
						return $"Goblin {g} is working but wanders.";
					if (!(job >= 0 && job < tiles && s.DesignationAt(job) != DesignationKind.None))
						return $"Goblin {g} works tile {job}, which is not designated.";
					if (task == GoblinTask.Digging
						&& !TileRules.CanDigFrom(s, s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]), job, s.DesignationAt(job)))
						return $"Goblin {g} is digging tile {job} from where it cannot dig it.";
					for (var other = 0; other < g; other++)
						if (s.GoblinJobTile[other] == job)
							return $"Goblins {other} and {g} both claim tile {job}.";
					break;
			}
		}
		return null;
	}
}
