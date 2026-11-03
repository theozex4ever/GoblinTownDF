namespace Goblins.Sim;

/// <summary>
/// The one owner of every goblin Task and what it requires of the per-goblin arrays (ADR 0002). The
/// transitions are the only code that changes <see cref="SimState.GoblinTask"/>,
/// <see cref="SimState.GoblinJobTile"/>, <see cref="SimState.GoblinWanderTile"/>, <see cref="SimState.GoblinWanderTicks"/>,
/// <see cref="SimState.GoblinCarry"/> and <see cref="SimState.GoblinWorkTicks"/>;
/// <see cref="FindViolation"/> states the rules they keep.
/// A goblin works one of two Jobs. A dig: Walking to a designated tile, then Digging it. A Haul: Fetching a loose unit,
/// PickingUp, Carrying it to a Pile tile with room for its material (or waiting, still Carrying, while there is none),
/// and Dropping it there.
/// A Craving ends any of those Tasks at once, dropping what the goblin carries: it goes Seeking the nearest unit of Grub,
/// then Eating it where it lies, or SeekingGrog and Drinking for a Craving for Grog. A goblin finishes the one Need it
/// has begun before it considers the other.
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
	/// <summary>Ticks a goblin spends picking a unit up, and again dropping it off.</summary>
	public const int HaulActionTicks = 5;

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

	/// <summary>
	/// Idle or Wandering → Fetching a loose unit at <paramref name="unitTile"/>, or PickingUp when the goblin already
	/// stands on it. Drops any wander.
	/// </summary>
	public static void BeginFetch(SimState s, int g, int unitTile, bool pickingUp)
	{
		s.GoblinJobTile[g] = unitTile;
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinWanderTile[g] = -1;
		s.GoblinWanderTicks[g] = 0;
		s.GoblinTask[g] = (byte)(pickingUp ? GoblinTask.PickingUp : GoblinTask.Fetching);
	}

	/// <summary>Fetching → PickingUp, on arrival at the unit.</summary>
	public static void StartPickingUp(SimState s, int g)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.PickingUp;
	}

	/// <summary>
	/// Counts one tick of a pick-up or drop-off. True on the tick the <see cref="HaulActionTicks"/>th tick is
	/// counted, when the caller must finish the action with <see cref="FinishPickUp"/> or by releasing the goblin.
	/// </summary>
	public static bool WorkTick(SimState s, int g) => ++s.GoblinWorkTicks[g] >= HaulActionTicks;

	/// <summary>PickingUp → Carrying one unit of <paramref name="material"/>. The unit has just left its tile.</summary>
	public static void FinishPickUp(SimState s, int g, Material material)
	{
		s.GoblinCarry[g] = (byte)material;
		s.GoblinJobTile[g] = -1;
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.Carrying;
	}

	/// <summary>Carrying → Dropping, once the goblin stands on a Pile tile.</summary>
	public static void StartDropping(SimState s, int g)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.Dropping;
	}

	/// <summary>Dropping → Carrying again, when the Pile under the goblin is gone or has no room left for its unit.</summary>
	public static void ResumeCarrying(SimState s, int g)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.Carrying;
	}

	/// <summary>
	/// Dropping → Idle, storing the carried unit on the Pile tile the goblin stands on. The caller has checked the tile
	/// is a Pile with room for the unit, so no drop can take a material past <see cref="SimState.PileCapacity"/>.
	/// </summary>
	public static void FinishDrop(SimState s, int g)
	{
		var tile = s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]);
		if (!s.PileHasRoom(tile, (Material)s.GoblinCarry[g]))
			throw new InvalidOperationException($"Goblin {g} cannot drop {(Material)s.GoblinCarry[g]} on tile {tile}: no Pile with room.");
		s.AddItems(tile, (Material)s.GoblinCarry[g], 1);
		s.GoblinCarry[g] = (byte)Material.None;
		Release(s, g);
	}

	/// <summary>
	/// Any Task but a seeking or consuming one → Seeking the nearest unit of <paramref name="supply"/> (Grub or Grog), or
	/// Eating or Drinking at once when <paramref name="consuming"/> (the goblin already stands on one). Drops any wander and
	/// abandons any Job, which stays on the job list with its progress. A goblin carrying a unit puts it down on
	/// <paramref name="dropTile"/> first, which the caller has chosen: a Pile tile with room for the unit or a tile that is not
	/// a Pile, so no drop can take a material past <see cref="SimState.PileCapacity"/>. A goblin carrying nothing ignores
	/// <paramref name="dropTile"/>.
	/// </summary>
	public static void BeginSeek(SimState s, int g, int dropTile, bool eating, Material supply = Material.Grub)
	{
		if (supply is not (Material.Grub or Material.Grog))
			throw new ArgumentOutOfRangeException(nameof(supply), supply, "A Need is met with Grub or Grog.");
		if (IsSeekingTask((GoblinTask)s.GoblinTask[g]) || IsConsumingTask((GoblinTask)s.GoblinTask[g]))
			throw new InvalidOperationException($"Goblin {g} is already satisfying a Need and finishes it before considering another.");
		var carried = (Material)s.GoblinCarry[g];
		if (carried != Material.None)
		{
			if (!(dropTile >= 0 && dropTile < s.TileKinds.Length
				&& TileRules.IsWalkable(s.KindAt(dropTile))
				&& (!s.IsPile(dropTile) || s.PileHasRoom(dropTile, carried))))
				throw new InvalidOperationException($"Goblin {g} cannot put {carried} down on tile {dropTile}: not a walkable tile that can take it.");
			s.AddItems(dropTile, carried, 1);
			s.GoblinCarry[g] = (byte)Material.None;
		}
		s.GoblinJobTile[g] = -1;
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinWanderTile[g] = -1;
		s.GoblinWanderTicks[g] = 0;
		s.GoblinTask[g] = (byte)(supply == Material.Grub
			? eating ? GoblinTask.Eating : GoblinTask.Seeking
			: eating ? GoblinTask.Drinking : GoblinTask.SeekingGrog);
	}

	/// <summary>Seeking → Eating, on arrival at a tile holding Grub.</summary>
	public static void StartEating(SimState s, int g) => StartConsuming(s, g, GoblinTask.Eating);

	/// <summary>SeekingGrog → Drinking, on arrival at a tile holding Grog.</summary>
	public static void StartDrinking(SimState s, int g) => StartConsuming(s, g, GoblinTask.Drinking);

	static void StartConsuming(SimState s, int g, GoblinTask task)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)task;
	}

	/// <summary>
	/// Counts one tick of a meal or a drink. True on the tick the <see cref="NeedRules.EatTicks"/>th tick is counted, when the
	/// caller must finish it with <see cref="FinishEating"/> or <see cref="FinishDrinking"/>, or by releasing the goblin if the unit is gone.
	/// </summary>
	public static bool EatTick(SimState s, int g) => ++s.GoblinWorkTicks[g] >= NeedRules.EatTicks;

	/// <summary>
	/// Eating → Idle: the goblin eats one unit of Grub off the tile it stands on, Pile or floor, and its meter rises by
	/// <see cref="NeedRules.EatRestore"/>. The caller has checked the unit is still there.
	/// </summary>
	public static void FinishEating(SimState s, int g) => FinishConsuming(s, g, Material.Grub, s.GoblinGrub);

	/// <summary>Drinking → Idle: as <see cref="FinishEating"/>, with a unit of Grog and the Grog meter.</summary>
	public static void FinishDrinking(SimState s, int g) => FinishConsuming(s, g, Material.Grog, s.GoblinGrog);

	static void FinishConsuming(SimState s, int g, Material supply, byte[] meter)
	{
		var tile = s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]);
		if (s.ItemCount(tile, supply) == 0)
			throw new InvalidOperationException($"Goblin {g} cannot take {supply} on tile {tile}: none there.");
		s.RemoveItems(tile, supply, 1);
		meter[g] = NeedRules.Restore(meter[g]);
		Release(s, g);
	}

	/// <summary>True for SeekingGrog and Seeking: walking to a unit to consume.</summary>
	public static bool IsSeekingTask(GoblinTask task) => task is GoblinTask.Seeking or GoblinTask.SeekingGrog;

	/// <summary>True for Eating and Drinking: consuming a unit on the spot.</summary>
	public static bool IsConsumingTask(GoblinTask task) => task is GoblinTask.Eating or GoblinTask.Drinking;

	/// <summary>The Material a Need-satisfying Task consumes: Grub for Seeking and Eating, Grog for SeekingGrog and Drinking, None for any other Task.</summary>
	public static Material SupplyOf(GoblinTask task) => task switch
	{
		GoblinTask.Seeking or GoblinTask.Eating => Material.Grub,
		GoblinTask.SeekingGrog or GoblinTask.Drinking => Material.Grog,
		_ => Material.None,
	};

	/// <summary>
	/// Any Task → Idle, with no Job and no wander. The Job, if any, stays on the job list. A goblin holding a unit never
	/// goes through here: it keeps the unit until <see cref="FinishDrop"/> stores it, so a release cannot lose a unit
	/// or put one down past a Pile's capacity.
	/// </summary>
	public static void Release(SimState s, int g)
	{
		if ((Material)s.GoblinCarry[g] != Material.None)
			throw new InvalidOperationException($"Goblin {g} holds {(Material)s.GoblinCarry[g]} and can only let go of it by dropping it on a Pile.");
		s.GoblinTask[g] = (byte)GoblinTask.Idle;
		s.GoblinJobTile[g] = -1;
		s.GoblinWorkTicks[g] = 0;
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
	/// True for the Tasks that work a Designation (Walking, Digging), false for the Tasks that work a loose unit
	/// (Fetching, PickingUp). Two goblins never claim one tile for a dig; a dig and a Haul may share a tile, and a tile
	/// is claimed for Hauls by at most as many goblins as it has loose units.
	/// </summary>
	public static bool IsDigTask(GoblinTask task) => task is GoblinTask.Walking or GoblinTask.Digging;

	/// <summary>True for Fetching and PickingUp: the Tasks that hold a claim on a loose unit.</summary>
	public static bool IsFetchTask(GoblinTask task) => task is GoblinTask.Fetching or GoblinTask.PickingUp;

	/// <summary>
	/// How many goblins numbered below <paramref name="below"/> hold a claim on a loose unit of <paramref name="tile"/>.
	/// Pass the goblin count for every claim; pass a goblin's own number to ask whether its claim has a unit to itself,
	/// which is true while the count is under <see cref="SimState.LooseTotal"/>.
	/// </summary>
	public static int FetchClaims(SimState s, int tile, int below)
	{
		var claims = 0;
		for (var g = 0; g < below; g++)
			if (s.GoblinJobTile[g] == tile && IsFetchTask((GoblinTask)s.GoblinTask[g]))
				claims++;
		return claims;
	}

	/// <summary>
	/// The first broken Task rule, or null when every goblin's Task fits its fields and the world: the field
	/// shape of each Task, and the world rules (a claimed tile is designated or holds a loose unit, a Digging goblin
	/// stands where it can dig, a PickingUp goblin stands on its unit, a Dropping goblin stands on a Pile tile, a
	/// goblin carries a unit exactly while Carrying or Dropping, a seeking or consuming goblin has a Craving for what it seeks and an Eating or Drinking goblin stands
	/// on a unit of it, a wander target is walkable on the goblin's own
	/// level and not its own tile, no two goblins claim one tile to dig it and no more claim a tile's loose units than it holds, step ticks are below
	/// <see cref="WalkTicksPerTile"/>, work ticks are below <see cref="HaulActionTicks"/> (<see cref="NeedRules.EatTicks"/> while Eating or Drinking) and only count while
	/// PickingUp, Dropping, Eating or Drinking, an idle pause is at most <see cref="MaxWanderPauseTicks"/>).
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

			var carry = (Material)s.GoblinCarry[g];
			if (!Enum.IsDefined(carry))
				return $"Goblin {g} carries unknown material {s.GoblinCarry[g]}.";
			var carrying = task is GoblinTask.Carrying or GoblinTask.Dropping;
			if (carrying && carry == Material.None)
				return $"Goblin {g} is {task} with nothing to carry.";
			if (!carrying && carry != Material.None)
				return $"Goblin {g} is {task} but carries {carry}.";
			if (s.GoblinWorkTicks[g] >= (IsConsumingTask(task) ? NeedRules.EatTicks : HaulActionTicks))
				return $"Goblin {g} has work ticks {s.GoblinWorkTicks[g]}.";
			if (s.GoblinWorkTicks[g] != 0 && task is not (GoblinTask.PickingUp or GoblinTask.Dropping or GoblinTask.Eating or GoblinTask.Drinking))
				return $"Goblin {g} is {task} but has work ticks {s.GoblinWorkTicks[g]}.";

			var job = s.GoblinJobTile[g];
			var wanderTile = s.GoblinWanderTile[g];
			var wanderTicks = s.GoblinWanderTicks[g];
			var here = s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]);
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
						&& wanderTile != here))
						return $"Goblin {g} wanders to tile {wanderTile}, which is not a walkable tile on its own level.";
					break;

				case GoblinTask.Walking:
				case GoblinTask.Digging:
					if (wanderTile != -1 || wanderTicks != 0)
						return $"Goblin {g} is working but wanders.";
					if (!(job >= 0 && job < tiles && s.DesignationAt(job) != DesignationKind.None))
						return $"Goblin {g} works tile {job}, which is not designated.";
					if (task == GoblinTask.Digging
						&& !TileRules.CanDigFrom(s, here, job, s.DesignationAt(job)))
						return $"Goblin {g} is digging tile {job} from where it cannot dig it.";
					if (FindOtherDigger(s, g, job) is { } digRival)
						return $"Goblins {digRival} and {g} both claim tile {job}.";
					break;

				case GoblinTask.Fetching:
				case GoblinTask.PickingUp:
					if (wanderTile != -1 || wanderTicks != 0)
						return $"Goblin {g} is working but wanders.";
					if (!(job >= 0 && job < tiles && s.LooseMaterial(job) != Material.None))
						return $"Goblin {g} fetches tile {job}, which holds no loose unit.";
					if (task == GoblinTask.PickingUp && job != here)
						return $"Goblin {g} is picking up tile {job} from tile {here}.";
					if (FetchClaims(s, job, g) >= s.LooseTotal(job))
						return $"Goblin {g} fetches a unit on tile {job}, which holds {s.LooseTotal(job)} and is claimed by others.";
					break;

				case GoblinTask.Carrying:
				case GoblinTask.Dropping:
					if (job != -1)
						return $"Goblin {g} is carrying but holds job tile {job}.";
					if (wanderTile != -1 || wanderTicks != 0)
						return $"Goblin {g} is carrying but wanders.";
					if (task == GoblinTask.Dropping && !s.IsPile(here))
						return $"Goblin {g} is dropping a unit on tile {here}, which is not a Pile.";
					break;

				case GoblinTask.Seeking:
				case GoblinTask.Eating:
				case GoblinTask.SeekingGrog:
				case GoblinTask.Drinking:
					var supply = SupplyOf(task);
					var meter = s.MeterOf(supply)[g];
					if (job != -1)
						return $"Goblin {g} is not working but holds job tile {job}.";
					if (wanderTile != -1 || wanderTicks != 0)
						return $"Goblin {g} is {task} but wanders.";
					if (!NeedRules.IsCraving(meter))
						return $"Goblin {g} is {task} with a {supply} meter of {meter}, which is no Craving.";
					if (IsConsumingTask(task) && s.ItemCount(here, supply) == 0)
						return $"Goblin {g} is {task} on tile {here}, which holds no {supply}.";
					break;
			}
		}
		return null;
	}

	// The lower-numbered goblin that is digging the same tile as goblin g, or null.
	static int? FindOtherDigger(SimState s, int g, int job)
	{
		for (var other = 0; other < g; other++)
			if (s.GoblinJobTile[other] == job && IsDigTask((GoblinTask)s.GoblinTask[other]))
				return other;
		return null;
	}
}
