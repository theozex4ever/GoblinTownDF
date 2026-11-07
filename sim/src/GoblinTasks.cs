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
/// A Tend: Approaching a farm tile (a Grub Pit or a Fungus Farm), then Tending it for <see cref="ProductionRules.TendTicks"/> working ticks, after which the tile
/// spawns its crop and the goblin is Idle again.
/// A Brew: Gathering to a tile holding Fungus, Taking one unit (5 ticks), Bringing it to a free Brewery tile and Brewing it there for
/// <see cref="ProductionRules.BrewTicks"/> working ticks, counted only while a Pile has room for the Grog, after which the Fungus is one Grog, loose on the tile, and the goblin is Idle again.
/// A Craving ends any of those Tasks at once, dropping what the goblin carries: it goes Seeking the nearest unit of Grub,
/// then Eating it where it lies, or SeekingGrog and Drinking for a Craving for Grog, or SeekingHeap (walking to the
/// nearest Sleeping Heap tile) and Sleeping there, or Sleeping where it stands when no Heap is in reach. A goblin finishes the one Need it
/// has begun before it considers another. Sleeping lasts until the Sleep meter is full, whatever else is craved; the one
/// thing that ends a Task already satisfying a Need is Collapse, at Sleep 0, which puts the goblin to sleep on the spot.
/// Acting out ends a Job too: a goblin with enough Chaos and no cooldown begins Shirking, loafing where it stands for <see cref="ChaosRules.ShirkTicks"/>
/// ticks and taking no Job, putting down what it carries as a Craving does. A Craving or Collapse ends a shirk at once, for no relief; either way a cooldown follows.
/// A fight is the other way to act out: the instigator goes Pursuing a target for up to <see cref="ChaosRules.PursuitTicks"/> ticks while the target keeps working, and on contact
/// both abandon their work, as a shirk does, and Scuffle for <see cref="ChaosRules.ScuffleTicks"/> ticks (the instigator Scuffling, the victim Scuffled). A scuffle that
/// runs its course relieves the instigator and leaves both Recovering, taking no work, for <see cref="ChaosRules.RecoveryTicks"/> ticks with a cooldown that began with the scuffle's end;
/// a Craving or Collapse in either ends it for both at once, for no relief and no recovery. A failed or interrupted pursuit touches only the instigator. The fight clock is
/// <see cref="SimState.GoblinFightTicks"/>, counted down on every sim tick, and fighters are paired through <see cref="SimState.GoblinFightPartner"/>.
/// A theft is the third way to act out: the thief goes Sneaking to the nearest Pile holding Grub or Grog, Pilfers one unit for <see cref="HaulActionTicks"/> ticks (the only moment the unit leaves the Pile),
/// goes MakingOff with it to a destination chosen when it set out, and Scoffs it for <see cref="NeedRules.EatTicks"/> ticks, which restores the Need, relieves <see cref="ChaosRules.ActRelief"/> Chaos and starts the cooldown.
/// Nothing is reserved. A plan that changes, or a Craving or Collapse, ends the theft for no relief with the cooldown, a unit in hand put down by the same legal-drop rule; with no legal place the thief is Stalled,
/// holding the unit and consuming nothing, until one opens. The chosen supply and destination are <see cref="SimState.GoblinTheftSupply"/> and <see cref="SimState.GoblinTheftDestination"/>, and the source Pile tile is its Job tile.
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

	/// <summary>
	/// Idle or Wandering → Approaching the farm tile <paramref name="tile"/> it has claimed to Tend, or Tending at once when the
	/// goblin already stands on it. Drops any wander.
	/// </summary>
	public static void BeginTend(SimState s, int g, int tile, bool tending)
	{
		s.GoblinJobTile[g] = tile;
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinWanderTile[g] = -1;
		s.GoblinWanderTicks[g] = 0;
		s.GoblinTask[g] = (byte)(tending ? GoblinTask.Tending : GoblinTask.Approaching);
	}

	/// <summary>Approaching → Tending, on arrival at the tile.</summary>
	public static void StartTending(SimState s, int g)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.Tending;
	}

	/// <summary>
	/// Counts one working tick of a Tend. True on the tick the <see cref="ProductionRules.TendTicks"/>th tick is counted, when the
	/// caller must start the tile spawning and release the goblin.
	/// </summary>
	public static bool TendTick(SimState s, int g) => ++s.GoblinWorkTicks[g] >= ProductionRules.TendTicks;

	/// <summary>
	/// Idle or Wandering → Gathering, walking to the tile <paramref name="sourceTile"/> holding the Fungus it has claimed, or Taking
	/// at once when <paramref name="taking"/> (the goblin already stands on it). Drops any wander.
	/// </summary>
	public static void BeginBrew(SimState s, int g, int sourceTile, bool taking)
	{
		s.GoblinJobTile[g] = sourceTile;
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinWanderTile[g] = -1;
		s.GoblinWanderTicks[g] = 0;
		s.GoblinTask[g] = (byte)(taking ? GoblinTask.Taking : GoblinTask.Gathering);
	}

	/// <summary>Gathering → Taking, on arrival at the Fungus.</summary>
	public static void StartTaking(SimState s, int g)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.Taking;
	}

	/// <summary>
	/// Taking → Bringing one unit of Fungus to the Brewery tile <paramref name="breweryTile"/>, which the goblin claims, or
	/// Brewing at once when <paramref name="brewing"/> (it already stands on it). The unit has just left its tile.
	/// </summary>
	public static void FinishTake(SimState s, int g, int breweryTile, bool brewing)
	{
		s.GoblinCarry[g] = (byte)Material.Fungus;
		s.GoblinJobTile[g] = breweryTile;
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)(brewing ? GoblinTask.Brewing : GoblinTask.Bringing);
	}

	/// <summary>Bringing → Brewing, on arrival at the Brewery tile.</summary>
	public static void StartBrewing(SimState s, int g)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.Brewing;
	}

	/// <summary>
	/// Counts one working tick of a Brew. True on the tick the <see cref="ProductionRules.BrewTicks"/>th tick is counted, when the
	/// caller must finish it with <see cref="FinishBrew"/>.
	/// </summary>
	public static bool BrewTick(SimState s, int g) => ++s.GoblinWorkTicks[g] >= ProductionRules.BrewTicks;

	/// <summary>
	/// Brewing → Idle: the Fungus in the goblin's hands becomes one Grog, loose on the Brewery tile it stands on. The Fungus is
	/// gone, so a Collapse or Craving before this tick loses the brew's progress and drops the Fungus instead.
	/// </summary>
	public static void FinishBrew(SimState s, int g)
	{
		var tile = s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]);
		if (s.RoomAt(tile) != RoomKind.Brewery || (Material)s.GoblinCarry[g] != Material.Fungus)
			throw new InvalidOperationException($"Goblin {g} cannot brew on tile {tile}: no Brewery, or no Fungus in hand.");
		s.AddItems(tile, Material.Grog, 1);
		s.GoblinCarry[g] = (byte)Material.None;
		Release(s, g);
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

	/// <summary>
	/// Dropping → Carrying again, when the Pile under the goblin is gone or has no room left for its unit. Also Bringing or Brewing →
	/// Carrying, when the Brewery tile the goblin claimed is gone: the Fungus in its hands goes to a Pile like any carried unit, and the
	/// brew's progress is lost.
	/// </summary>
	public static void ResumeCarrying(SimState s, int g)
	{
		s.GoblinJobTile[g] = -1;
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
		if (IsSatisfyingTask((GoblinTask)s.GoblinTask[g]))
			throw new InvalidOperationException($"Goblin {g} is already satisfying a Need and finishes it before considering another.");
		BeginNeedTask(s, g, dropTile, supply == Material.Grub
			? eating ? GoblinTask.Eating : GoblinTask.Seeking
			: eating ? GoblinTask.Drinking : GoblinTask.SeekingGrog);
	}

	/// <summary>
	/// Any Task but a seeking, consuming or sleeping one → SeekingHeap, walking to the nearest Sleeping Heap tile, or Sleeping at once
	/// when <paramref name="asleep"/> (the goblin already stands on a Heap, or no Heap is in reach and it sleeps where it stands).
	/// Drops any wander and abandons any Job, as <see cref="BeginSeek"/> does, and puts a carried unit down on <paramref name="dropTile"/> first.
	/// </summary>
	public static void BeginSleep(SimState s, int g, int dropTile, bool asleep)
	{
		if (IsSatisfyingTask((GoblinTask)s.GoblinTask[g]))
			throw new InvalidOperationException($"Goblin {g} is already satisfying a Need and finishes it before considering another.");
		BeginNeedTask(s, g, dropTile, asleep ? GoblinTask.Sleeping : GoblinTask.SeekingHeap);
	}

	/// <summary>
	/// Any Task but a seeking, consuming, sleeping or shirking one → Shirking where the goblin stands. Drops any wander and abandons any Job, which
	/// stays on the job list with its progress: a dig keeps its progress on the tile, while a Tend or a Brew loses the goblin's. A carried unit is put down on
	/// <paramref name="dropTile"/> first, as in <see cref="BeginSeek"/>; the caller has chosen a tile that can legally take it, or defers the shirk.
	/// </summary>
	public static void BeginShirk(SimState s, int g, int dropTile)
	{
		var task = (GoblinTask)s.GoblinTask[g];
		if (IsSatisfyingTask(task) || IsActingOutTask(task))
			throw new InvalidOperationException($"Goblin {g} is {task} and cannot begin to shirk.");
		BeginNeedTask(s, g, dropTile, GoblinTask.Shirking);
	}

	/// <summary>
	/// Any Task but a seeking, consuming, sleeping or acting-out one → Sneaking to the Pile tile <paramref name="source"/> for a unit of <paramref name="supply"/> (Grub or Grog),
	/// or Pilfering it at once when <paramref name="atSource"/> (the goblin already stands there). The goblin has chosen <paramref name="destination"/>, the non-Pile tile it will
	/// consume the unit on, and never replaces it. Abandons any Job and drops any wander and puts a carried unit down on <paramref name="dropTile"/> first, exactly as
	/// <see cref="BeginShirk"/> does. Nothing is reserved: the unit stays communal stock until it is picked up.
	/// </summary>
	public static void BeginTheft(SimState s, int g, int source, Material supply, int destination, int dropTile, bool atSource)
	{
		var task = (GoblinTask)s.GoblinTask[g];
		if (IsSatisfyingTask(task) || IsActingOutTask(task))
			throw new InvalidOperationException($"Goblin {g} is {task} and cannot begin to steal.");
		if (supply is not (Material.Grub or Material.Grog))
			throw new ArgumentOutOfRangeException(nameof(supply), supply, "A thief takes Grub or Grog.");
		BeginNeedTask(s, g, dropTile, atSource ? GoblinTask.Pilfering : GoblinTask.Sneaking);
		s.GoblinJobTile[g] = source;
		s.GoblinTheftSupply[g] = (byte)supply;
		s.GoblinTheftDestination[g] = destination;
	}

	/// <summary>Sneaking → Pilfering, on arrival at the Pile tile.</summary>
	public static void StartPilfering(SimState s, int g)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.Pilfering;
	}

	/// <summary>
	/// Pilfering → MakingOff with the unit of the goblin's chosen supply, which has just left its Pile. The goblin walks it to the destination it chose when it set out.
	/// </summary>
	public static void FinishPilfer(SimState s, int g)
	{
		s.GoblinCarry[g] = s.GoblinTheftSupply[g];
		s.GoblinJobTile[g] = -1;
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.MakingOff;
	}

	/// <summary>MakingOff → Scoffing, on arrival at the destination.</summary>
	public static void StartScoffing(SimState s, int g)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.Scoffing;
	}

	/// <summary>Counts one tick of private consumption. True on the tick the <see cref="NeedRules.EatTicks"/>th tick is counted, when the caller must end it with <see cref="FinishScoff"/>.</summary>
	public static bool ScoffTick(SimState s, int g) => ++s.GoblinWorkTicks[g] >= NeedRules.EatTicks;

	/// <summary>
	/// Scoffing → Idle: the goblin consumes the unit in its hands exactly once. Its meter for that supply rises by <see cref="NeedRules.EatRestore"/>, held at <see cref="NeedRules.MeterMax"/>,
	/// it loses <see cref="ChaosRules.ActRelief"/> Chaos and its cooldown starts. The unit was taken from the Pile when it was picked up, so nothing else is removed.
	/// </summary>
	public static void FinishScoff(SimState s, int g)
	{
		var supply = (Material)s.GoblinCarry[g];
		if ((GoblinTask)s.GoblinTask[g] != GoblinTask.Scoffing || supply is not (Material.Grub or Material.Grog))
			throw new InvalidOperationException($"Goblin {g} has no stolen Grub or Grog in hand to consume.");
		var meter = supply == Material.Grub ? s.GoblinGrub : s.GoblinGrog;
		meter[g] = NeedRules.Restore(meter[g]);
		s.GoblinCarry[g] = (byte)Material.None;
		Release(s, g);
		s.GoblinActCooldown[g] = ChaosRules.CooldownTicks;
		ChaosSystem.Add(s, g, -ChaosRules.ActRelief);
	}

	/// <summary>
	/// MakingOff or Scoffing → Stalled: the theft has failed or been interrupted but the goblin has no legal place to put its unit down (every tile it can reach is a Pile full of
	/// that supply), so it keeps the unit, consumes nothing and waits, its cooldown not yet begun. <see cref="EndTheft"/> ends it once a place opens.
	/// </summary>
	public static void StallTheft(SimState s, int g)
	{
		var task = (GoblinTask)s.GoblinTask[g];
		if (task is not (GoblinTask.MakingOff or GoblinTask.Scoffing) || (Material)s.GoblinCarry[g] == Material.None)
			throw new InvalidOperationException($"Goblin {g} is {task} and has no stolen unit to hold on to.");
		s.GoblinJobTile[g] = -1;
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTheftDestination[g] = -1;
		s.GoblinTask[g] = (byte)GoblinTask.Stalled;
	}

	/// <summary>
	/// Any theft Task → Idle, the theft having failed or been given up, for no relief: a unit in hand goes down on <paramref name="dropTile"/>, which the caller has chosen as a
	/// walkable tile that can legally take it, and the cooldown starts. Nothing before the pickup has moved, so nothing is put back.
	/// </summary>
	public static void EndTheft(SimState s, int g, int dropTile)
	{
		if (!IsTheftTask((GoblinTask)s.GoblinTask[g]))
			throw new InvalidOperationException($"Goblin {g} is not stealing.");
		BeginNeedTask(s, g, dropTile, GoblinTask.Idle);
	}

	/// <summary>
	/// Any Task but a seeking, consuming, sleeping or acting-out one → Pursuing goblin <paramref name="target"/>, for at most <see cref="ChaosRules.PursuitTicks"/> ticks.
	/// Drops any wander and abandons any Job, and puts a carried unit down on <paramref name="dropTile"/> first, exactly as <see cref="BeginShirk"/> does. The target is told nothing.
	/// </summary>
	public static void BeginPursuit(SimState s, int g, int target, int dropTile)
	{
		var task = (GoblinTask)s.GoblinTask[g];
		if (IsSatisfyingTask(task) || IsActingOutTask(task))
			throw new InvalidOperationException($"Goblin {g} is {task} and cannot begin a pursuit.");
		if (target == g || target < 0 || target >= s.GoblinCount)
			throw new ArgumentOutOfRangeException(nameof(target), target, "A pursuit needs another goblin as its target.");
		BeginNeedTask(s, g, dropTile, GoblinTask.Pursuing);
		s.GoblinFightPartner[g] = target;
		s.GoblinFightTicks[g] = ChaosRules.PursuitTicks;
	}

	/// <summary>
	/// Pursuing → Idle: the pursuit ran out its ticks, lost its path, or found the target no longer available. The instigator alone starts its cooldown, for no relief.
	/// </summary>
	public static void FailPursuit(SimState s, int g)
	{
		Release(s, g);
		s.GoblinActCooldown[g] = ChaosRules.CooldownTicks;
	}

	/// <summary>
	/// Pursuing → Scuffling for the instigator, and any non-acting-out Task → Scuffled for <paramref name="victim"/>, which abandons its work as a shirk does and puts a
	/// carried unit down on <paramref name="victimDropTile"/> first. Both hold the same scuffle clock, and the victim gains <see cref="ChaosRules.VictimGain"/> Chaos, once, now.
	/// </summary>
	public static void BeginScuffle(SimState s, int instigator, int victim, int victimDropTile)
	{
		if ((GoblinTask)s.GoblinTask[instigator] != GoblinTask.Pursuing || s.GoblinFightPartner[instigator] != victim)
			throw new InvalidOperationException($"Goblin {instigator} is not pursuing goblin {victim}.");
		var task = (GoblinTask)s.GoblinTask[victim];
		if (IsSatisfyingTask(task) || IsActingOutTask(task))
			throw new InvalidOperationException($"Goblin {victim} is {task} and cannot be set upon.");
		BeginNeedTask(s, victim, victimDropTile, GoblinTask.Scuffled);
		s.GoblinFightPartner[victim] = instigator;
		s.GoblinFightTicks[victim] = ChaosRules.ScuffleTicks;
		ChaosSystem.Add(s, victim, ChaosRules.VictimGain);

		s.GoblinStepTicks[instigator] = 0;
		s.GoblinWorkTicks[instigator] = 0;
		s.GoblinFightTicks[instigator] = ChaosRules.ScuffleTicks;
		s.GoblinTask[instigator] = (byte)GoblinTask.Scuffling;
	}

	/// <summary>
	/// Scuffling and Scuffled → Recovering, both at once: the scuffle ran its course. The instigator loses <see cref="ChaosRules.ActRelief"/> Chaos and the victim none, and
	/// both start their cooldown and their <see cref="ChaosRules.RecoveryTicks"/> ticks of no work together. Each keeps the other as its partner, to be named while it recovers.
	/// </summary>
	public static void FinishScuffle(SimState s, int instigator)
	{
		var victim = s.GoblinFightPartner[instigator];
		StartRecovery(s, instigator, victim);
		StartRecovery(s, victim, instigator);
		ChaosSystem.Add(s, instigator, -ChaosRules.ActRelief);
	}

	static void StartRecovery(SimState s, int g, int partner)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinFightPartner[g] = partner;
		s.GoblinFightTicks[g] = ChaosRules.RecoveryTicks;
		s.GoblinActCooldown[g] = ChaosRules.CooldownTicks;
		s.GoblinTask[g] = (byte)GoblinTask.Recovering;
	}

	/// <summary>Gives a new world its fight arrays: no goblin in a fight, no clock running.</summary>
	public static void StartFights(SimState s)
	{
		s.GoblinFightPartner = SimState.NoJobs();
		s.GoblinFightTicks = new ushort[SimState.MaxGoblins];
	}

	/// <summary>Gives a new world its theft arrays: no goblin stealing, nothing chosen.</summary>
	public static void StartThefts(SimState s)
	{
		s.GoblinTheftDestination = SimState.NoJobs();
		s.GoblinTheftSupply = new byte[SimState.MaxGoblins];
	}

	/// <summary>Recovering → Idle: the goblin has rested its full time. Its cooldown, started with the scuffle, runs on.</summary>
	public static void FinishRecovery(SimState s, int g) => Release(s, g);

	/// <summary>
	/// Counts every fight clock down by one sim tick, whatever its goblin is doing, so a Starving goblin's pursuit times out, its scuffle ends and its recovery
	/// finishes on the same tick as anyone's. <see cref="JobSystem"/> ends a stage on the tick its clock reaches 0.
	/// </summary>
	public static void CountDownFights(SimState s)
	{
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.GoblinFightTicks[g] > 0)
				s.GoblinFightTicks[g]--;
	}

	/// <summary>
	/// Counts one tick of shirking. True on the tick the <see cref="ChaosRules.ShirkTicks"/>th tick is counted, when the caller must end it with
	/// <see cref="FinishShirk"/>.
	/// </summary>
	public static bool ShirkTick(SimState s, int g) => ++s.GoblinWorkTicks[g] >= ChaosRules.ShirkTicks;

	/// <summary>Shirking → Idle: the goblin has loafed its full time, so it loses <see cref="ChaosRules.ActRelief"/> Chaos and starts its cooldown.</summary>
	public static void FinishShirk(SimState s, int g)
	{
		Release(s, g);
		s.GoblinActCooldown[g] = ChaosRules.CooldownTicks;
		ChaosSystem.Add(s, g, -ChaosRules.ActRelief);
	}

	/// <summary>
	/// Any Task but Sleeping → Sleeping where the goblin stands, because its Sleep meter has reached 0. Unlike a Craving this takes the goblin
	/// off a Task that satisfies a Need as well, a meal or a walk to a Heap included; the unit it was about to eat stays where it is.
	/// A carried unit is put down on <paramref name="dropTile"/> first, as in <see cref="BeginSleep"/>.
	/// </summary>
	public static void BeginCollapse(SimState s, int g, int dropTile)
	{
		if (s.GoblinSleep[g] != 0)
			throw new InvalidOperationException($"Goblin {g} has Sleep {s.GoblinSleep[g]}, not 0, so it does not Collapse.");
		if ((GoblinTask)s.GoblinTask[g] == GoblinTask.Sleeping)
			throw new InvalidOperationException($"Goblin {g} is already asleep.");
		BeginNeedTask(s, g, dropTile, GoblinTask.Sleeping);
	}

	// The common start of a Task that takes a goblin off its work, to satisfy a Need, to shirk or to fight: any carried unit goes down on dropTile,
	// which the caller has chosen as a walkable tile that can take it, the Job and wander are dropped, and the counters start from nothing.
	// An act this ends was interrupted, so it starts the goblin's cooldown and gives it no relief: a shirk or a pursuit, or a scuffle, which ends for the
	// other fighter too, with a cooldown for both. A recovery this ends starts nothing: its cooldown began with the scuffle.
	static void BeginNeedTask(SimState s, int g, int dropTile, GoblinTask task)
	{
		var before = (GoblinTask)s.GoblinTask[g];
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
		if (before is GoblinTask.Scuffling or GoblinTask.Scuffled)
		{
			var other = s.GoblinFightPartner[g];
			Release(s, other);
			s.GoblinActCooldown[other] = ChaosRules.CooldownTicks;
		}
		s.GoblinJobTile[g] = -1;
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinWanderTile[g] = -1;
		s.GoblinWanderTicks[g] = 0;
		s.GoblinFightPartner[g] = -1;
		s.GoblinFightTicks[g] = 0;
		s.GoblinTheftDestination[g] = -1;
		s.GoblinTheftSupply[g] = (byte)Material.None;
		s.GoblinTask[g] = (byte)task;
		if (before is GoblinTask.Shirking or GoblinTask.Pursuing or GoblinTask.Scuffling or GoblinTask.Scuffled || IsTheftTask(before))
			s.GoblinActCooldown[g] = ChaosRules.CooldownTicks;
	}

	/// <summary>Seeking → Eating, on arrival at a tile holding Grub.</summary>
	public static void StartEating(SimState s, int g) => StartConsuming(s, g, GoblinTask.Eating);

	/// <summary>SeekingGrog → Drinking, on arrival at a tile holding Grog.</summary>
	public static void StartDrinking(SimState s, int g) => StartConsuming(s, g, GoblinTask.Drinking);

	/// <summary>SeekingHeap → Sleeping, on arrival at a Heap tile, or when no Heap is left in reach and the goblin sleeps where it stands.</summary>
	public static void StartSleeping(SimState s, int g)
	{
		s.GoblinStepTicks[g] = 0;
		s.GoblinWorkTicks[g] = 0;
		s.GoblinTask[g] = (byte)GoblinTask.Sleeping;
	}

	/// <summary>
	/// Counts one tick of sleep. The goblin gains a point of Sleep on the <see cref="NeedRules.HeapRecoveryTicks"/>th counted tick if it lies on a
	/// Sleeping Heap tile now, and on the <see cref="NeedRules.FloorRecoveryTicks"/>th anywhere else, so the rate follows the tile under it,
	/// whatever it was when the sleep began. A goblin whose meter reaches <see cref="NeedRules.MeterMax"/> wakes, Idle.
	/// </summary>
	public static void SleepTick(SimState s, int g)
	{
		var here = s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]);
		if (++s.GoblinWorkTicks[g] < NeedRules.RecoveryTicks(s.RoomAt(here) == RoomKind.SleepingHeap))
			return;

		s.GoblinWorkTicks[g] = 0;
		if (++s.GoblinSleep[g] >= NeedRules.MeterMax)
			Release(s, g);
	}

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

	/// <summary>True for SeekingHeap and Sleeping: the Tasks that satisfy the Sleep Need.</summary>
	public static bool IsSleepTask(GoblinTask task) => task is GoblinTask.SeekingHeap or GoblinTask.Sleeping;

	/// <summary>True for every Task that satisfies a Need, which a goblin finishes before it considers another.</summary>
	public static bool IsSatisfyingTask(GoblinTask task) => IsSeekingTask(task) || IsConsumingTask(task) || IsSleepTask(task);

	/// <summary>True for Pursuing, Scuffling, Scuffled and Recovering: the stages of a fight.</summary>
	public static bool IsFightTask(GoblinTask task) => task is GoblinTask.Pursuing or GoblinTask.Scuffling or GoblinTask.Scuffled or GoblinTask.Recovering;

	/// <summary>True for Sneaking, Pilfering, MakingOff, Scoffing and Stalled: the stages of a theft.</summary>
	public static bool IsTheftTask(GoblinTask task) => task is GoblinTask.Sneaking or GoblinTask.Pilfering or GoblinTask.MakingOff or GoblinTask.Scoffing or GoblinTask.Stalled;

	/// <summary>True for Shirking, every stage of a fight and every stage of a theft: a goblin in one is acting out and starts no other act, nor is it set upon.</summary>
	public static bool IsActingOutTask(GoblinTask task) => task == GoblinTask.Shirking || IsFightTask(task) || IsTheftTask(task);

	/// <summary>The Need a Need-satisfying Task serves, or None for any other Task.</summary>
	public static Need NeedOf(GoblinTask task) => task switch
	{
		GoblinTask.Seeking or GoblinTask.Eating => Need.Grub,
		GoblinTask.SeekingGrog or GoblinTask.Drinking => Need.Grog,
		GoblinTask.SeekingHeap or GoblinTask.Sleeping => Need.Sleep,
		_ => Need.None,
	};

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
		s.GoblinFightPartner[g] = -1;
		s.GoblinFightTicks[g] = 0;
		s.GoblinTheftDestination[g] = -1;
		s.GoblinTheftSupply[g] = (byte)Material.None;
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

	/// <summary>True for Approaching and Tending: the Tasks that hold a claim on a farm tile.</summary>
	public static bool IsTendTask(GoblinTask task) => task is GoblinTask.Approaching or GoblinTask.Tending;

	/// <summary>True for Fetching and PickingUp: the Tasks that hold a claim on a loose unit.</summary>
	public static bool IsFetchTask(GoblinTask task) => task is GoblinTask.Fetching or GoblinTask.PickingUp;

	/// <summary>True for Gathering and Taking: the Tasks that hold a claim on a unit of Fungus.</summary>
	public static bool IsBrewSourceTask(GoblinTask task) => task is GoblinTask.Gathering or GoblinTask.Taking;

	/// <summary>True for Bringing and Brewing: the Tasks that hold a claim on a Brewery tile, with the Fungus in hand.</summary>
	public static bool IsBreweryTask(GoblinTask task) => task is GoblinTask.Bringing or GoblinTask.Brewing;

	/// <summary>How many goblins numbered below <paramref name="below"/> are Gathering or Taking a unit of Fungus on <paramref name="tile"/>.</summary>
	public static int BrewClaims(SimState s, int tile, int below)
	{
		var claims = 0;
		for (var g = 0; g < below; g++)
			if (s.GoblinJobTile[g] == tile && IsBrewSourceTask((GoblinTask)s.GoblinTask[g]))
				claims++;
		return claims;
	}

	/// <summary>
	/// Claims on a tile's units by goblins numbered below <paramref name="below"/>, haulers and brewers together. A hauler's claim is on
	/// any loose unit and a brewer's on a unit of Fungus, so a hauler has a unit of its own while this stays under <see cref="SimState.LooseTotal"/>.
	/// For a brewer see <see cref="BrewClaimHasUnit"/>.
	/// </summary>
	public static int SupplyClaims(SimState s, int tile, int below) => FetchClaims(s, tile, below) + BrewClaims(s, tile, below);

	/// <summary>
	/// True when a Brew claim on <paramref name="tile"/>'s Fungus has a unit of its own beside the claims of the goblins numbered below
	/// <paramref name="below"/>: the brewers before it leave Fungus, and the brewers and haulers before it together leave a unit of
	/// the tile's stock (a hauler may take Fungus, so only the whole stock bounds them). Pass the goblin count to ask whether one more brewer
	/// may claim. A hauler's claim on Dirt does not count against a brewer's Fungus, nor a brewer's against a hauler's Dirt.
	/// </summary>
	public static bool BrewClaimHasUnit(SimState s, int tile, int below)
	{
		var brewers = BrewClaims(s, tile, below);
		return brewers < s.ItemCount(tile, Material.Fungus)
			&& brewers + FetchClaims(s, tile, below) < s.LooseTotal(tile) + s.StoredCount(tile, Material.Fungus);
	}

	/// <summary>Loose Fungus on a tile that brewers have claimed. A hauler never takes these, whether or not Fungus has a Pile to go to.</summary>
	public static int ReservedFungus(SimState s, int tile) =>
		Math.Min(BrewClaims(s, tile, s.GoblinCount), s.LooseCount(tile, Material.Fungus));

	/// <summary>
	/// Loose units on a tile that haulers may claim: the units whose material has storage to go to, less the Fungus brewers have
	/// claimed. Fungus with no Pile to go to is not haulable, so a brewer's claim on it costs no other material a hauler.
	/// </summary>
	public static int HaulClaimable(SimState s, int tile) =>
		s.HaulableTotal(tile) - (s.HasStorageRoom(tile, Material.Fungus) ? ReservedFungus(s, tile) : 0);

	/// <summary>The first material (in Material order) a hauler may pick up on the tile: loose, with storage to go to, and not Fungus a brewer has claimed. None when there is none.</summary>
	public static Material HaulPickup(SimState s, int tile)
	{
		var reserved = ReservedFungus(s, tile);
		for (var material = Material.Dirt; (int)material <= SimState.CountedMaterials; material++)
			if (s.LooseCount(tile, material) - (material == Material.Fungus ? reserved : 0) > 0 && s.HasStorageRoom(tile, material))
				return material;
		return Material.None;
	}

	/// <summary>True when a goblin is Bringing or Brewing at the Brewery tile <paramref name="tile"/>: one brewer per tile.</summary>
	public static bool IsBreweryClaimed(SimState s, int tile)
	{
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.GoblinJobTile[g] == tile && IsBreweryTask((GoblinTask)s.GoblinTask[g]))
				return true;
		return false;
	}

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
	/// on a unit of it, a Sleeping goblin's meter is below <see cref="NeedRules.MeterMax"/> (it has climbed past any Craving, or Collapsed), a wander target is walkable on the goblin's own
	/// level and not its own tile, a tender works a farm tile that is not producing, and a Tending one stands on it, no two goblins claim one tile to dig it or to tend it, no more claim a tile's loose units than it holds, no more claim its Fungus to brew than it holds, and haulers and brewers together never claim more units than it has, a Bringing or Brewing goblin holds Fungus and a Brewery tile that no other goblin has, and a Brewing one stands on it, step ticks are below
	/// <see cref="WalkTicksPerTile"/>, work ticks are below <see cref="HaulActionTicks"/> (<see cref="NeedRules.EatTicks"/> while Eating or Drinking, <see cref="ProductionRules.TendTicks"/> while Tending,
	/// <see cref="NeedRules.FloorRecoveryTicks"/> while Sleeping, <see cref="ChaosRules.ShirkTicks"/> while Shirking) and only count while
	/// PickingUp, Dropping, Eating, Drinking, Tending, Sleeping or Shirking, a Shirking goblin holds no Job, wanders nowhere and has no cooldown, an idle pause is at most <see cref="MaxWanderPauseTicks"/>,
/// and a fight keeps its shape: a goblin Pursuing, Scuffling, Scuffled or Recovering holds no Job, wanders nowhere, carries nothing, names another goblin as its partner and has a fight clock
/// from 1 to its stage's length, and has none otherwise; a Pursuing, Scuffling or Scuffled goblin has no cooldown; Scuffling and Scuffled goblins name each other, stand on one tile or beside each other on one level
/// and hold one clock; a Recovering goblin has a cooldown at least as long as its recovery has left).
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
			var carrying = task is GoblinTask.Carrying or GoblinTask.Dropping or GoblinTask.MakingOff or GoblinTask.Scoffing or GoblinTask.Stalled || IsBreweryTask(task);
			if (carrying && carry == Material.None)
				return $"Goblin {g} is {task} with nothing to carry.";
			if (!carrying && carry != Material.None)
				return $"Goblin {g} is {task} but carries {carry}.";
			if (s.GoblinWorkTicks[g] >= (IsConsumingTask(task) || task == GoblinTask.Scoffing ? NeedRules.EatTicks : task == GoblinTask.Tending ? ProductionRules.TendTicks : task == GoblinTask.Sleeping ? NeedRules.FloorRecoveryTicks : task == GoblinTask.Brewing ? ProductionRules.BrewTicks : task == GoblinTask.Shirking ? ChaosRules.ShirkTicks : HaulActionTicks))
				return $"Goblin {g} has work ticks {s.GoblinWorkTicks[g]}.";
			if (s.GoblinWorkTicks[g] != 0 && task is not (GoblinTask.PickingUp or GoblinTask.Dropping or GoblinTask.Eating or GoblinTask.Drinking or GoblinTask.Tending or GoblinTask.Sleeping or GoblinTask.Taking or GoblinTask.Brewing or GoblinTask.Shirking or GoblinTask.Pilfering or GoblinTask.Scoffing))
				return $"Goblin {g} is {task} but has work ticks {s.GoblinWorkTicks[g]}.";

			if (FindFightViolation(s, g, task) is { } fightViolation)
				return fightViolation;
			if (FindTheftViolation(s, g, task) is { } theftViolation)
				return theftViolation;

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
					if (SupplyClaims(s, job, g) >= s.LooseTotal(job))
						return $"Goblin {g} fetches a unit on tile {job}, which holds {s.LooseTotal(job)} and is claimed by others.";
					break;

				case GoblinTask.Gathering:
				case GoblinTask.Taking:
					if (wanderTile != -1 || wanderTicks != 0)
						return $"Goblin {g} is working but wanders.";
					if (!(job >= 0 && job < tiles && s.ItemCount(job, Material.Fungus) > 0))
						return $"Goblin {g} gathers tile {job}, which holds no Fungus.";
					if (task == GoblinTask.Taking && job != here)
						return $"Goblin {g} is taking Fungus on tile {job} from tile {here}.";
					if (!BrewClaimHasUnit(s, job, g))
						return $"Goblin {g} gathers a unit of Fungus on tile {job}, which holds {s.ItemCount(job, Material.Fungus)} and is claimed by others.";
					break;

				case GoblinTask.Bringing:
				case GoblinTask.Brewing:
					if (wanderTile != -1 || wanderTicks != 0)
						return $"Goblin {g} is working but wanders.";
					if (carry != Material.Fungus)
						return $"Goblin {g} is {task} with {carry} in hand, not Fungus.";
					if (!(job >= 0 && job < tiles && s.RoomAt(job) == RoomKind.Brewery))
						return $"Goblin {g} brews at tile {job}, which is no Brewery.";
					if (task == GoblinTask.Brewing && job != here)
						return $"Goblin {g} is brewing at tile {job} from tile {here}.";
					if (FindOtherBrewer(s, g, job) is { } brewRival)
						return $"Goblins {brewRival} and {g} both claim Brewery tile {job}.";
					break;

				case GoblinTask.Approaching:
				case GoblinTask.Tending:
					if (wanderTile != -1 || wanderTicks != 0)
						return $"Goblin {g} is working but wanders.";
					if (!(job >= 0 && job < tiles && FarmSystem.IsTendTarget(s, job)))
						return $"Goblin {g} tends tile {job}, which is not a farm that is free to tend.";
					if (task == GoblinTask.Tending && job != here)
						return $"Goblin {g} is tending tile {job} from tile {here}.";
					if (FindOtherTender(s, g, job) is { } tendRival)
						return $"Goblins {tendRival} and {g} both claim tile {job} to tend.";
					break;

				case GoblinTask.Shirking:
				case GoblinTask.Pursuing:
				case GoblinTask.Scuffling:
				case GoblinTask.Scuffled:
				case GoblinTask.Recovering:
					if (job != -1)
						return $"Goblin {g} is not working but holds job tile {job}.";
					if (wanderTile != -1 || wanderTicks != 0)
						return $"Goblin {g} is {task} but wanders.";
					if (task != GoblinTask.Recovering && s.GoblinActCooldown[g] != 0)
						return $"Goblin {g} is {task} with a cooldown of {s.GoblinActCooldown[g]}, which only starts when the act ends.";
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
				case GoblinTask.SeekingHeap:
				case GoblinTask.Sleeping:
					var need = NeedOf(task);
					var meter = s.MeterOf(need)[g];
					if (job != -1)
						return $"Goblin {g} is not working but holds job tile {job}.";
					if (wanderTile != -1 || wanderTicks != 0)
						return $"Goblin {g} is {task} but wanders.";
					// A sleeper's meter climbs past the Craving that sent it to bed, so only the walk to bed needs one.
					if (task == GoblinTask.Sleeping ? meter >= NeedRules.MeterMax : !NeedRules.IsCraving(meter))
						return task == GoblinTask.Sleeping
							? $"Goblin {g} is Sleeping with a Sleep meter of {meter}, which is full."
							: $"Goblin {g} is {task} with a {need} meter of {meter}, which is no Craving.";
					if (IsConsumingTask(task) && s.ItemCount(here, SupplyOf(task)) == 0)
						return $"Goblin {g} is {task} on tile {here}, which holds no {SupplyOf(task)}.";
					break;
			}
		}
		return null;
	}

	// The first broken rule of a goblin's theft fields, or null. A goblin that is not stealing has no destination and no supply. One that is has a supply of Grub or Grog and no
	// cooldown (it starts when the theft ends); before the pickup it claims its source Pile tile, no unit in hand, and a Pilfering one stands on that tile; after it, it holds no
	// claim and carries exactly its supply, a MakingOff or Scoffing one has a destination (a Scoffing one stands on it), and a Stalled one, which has given its plan up, has none.
	static string? FindTheftViolation(SimState s, int g, GoblinTask task)
	{
		var supply = (Material)s.GoblinTheftSupply[g];
		var destination = s.GoblinTheftDestination[g];
		if (!IsTheftTask(task))
		{
			if (supply != Material.None)
				return $"Goblin {g} is {task} but has a theft supply of {supply}.";
			if (destination != -1)
				return $"Goblin {g} is {task} but has theft destination {destination}.";
			return null;
		}

		var tiles = s.TileKinds.Length;
		var job = s.GoblinJobTile[g];
		var here = s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]);
		if (supply is not (Material.Grub or Material.Grog))
			return $"Goblin {g} is {task} with {supply} to steal, not Grub or Grog.";
		if (s.GoblinActCooldown[g] != 0)
			return $"Goblin {g} is {task} with a cooldown of {s.GoblinActCooldown[g]}, which only starts when the act ends.";
		if (s.GoblinWanderTile[g] != -1 || s.GoblinWanderTicks[g] != 0)
			return $"Goblin {g} is {task} but wanders.";
		var before = task is GoblinTask.Sneaking or GoblinTask.Pilfering;
		if (before)
		{
			if (!(job >= 0 && job < tiles))
				return $"Goblin {g} is {task} for tile {job}, which is off the map.";
			if (task == GoblinTask.Pilfering && job != here)
				return $"Goblin {g} is pilfering tile {job} from tile {here}.";
		}
		else
		{
			if (job != -1)
				return $"Goblin {g} is {task} but holds job tile {job}.";
			if ((Material)s.GoblinCarry[g] != supply)
				return $"Goblin {g} is {task} with {(Material)s.GoblinCarry[g]} in hand, not the {supply} it stole.";
		}

		if (task == GoblinTask.Stalled)
			return destination == -1 ? null : $"Goblin {g} is Stalled but still has theft destination {destination}.";
		if (!(destination >= 0 && destination < tiles))
			return $"Goblin {g} is {task} for destination {destination}, which is off the map.";
		if (before)
		{
			// The source survives until pickup: reject fixed geometry no legal plan could have selected, even if a path exists now.
			var layer = s.Width * s.Height;
			var distance = Math.Abs(job % s.Width - destination % s.Width)
				+ Math.Abs(job / s.Width % s.Height - destination / s.Width % s.Height);
			if (job / layer != destination / layer || distance == 0 || distance > ChaosRules.TheftRange)
				return $"Goblin {g} is {task} with destination {destination}, which is not a distinct tile within {ChaosRules.TheftRange} steps of source {job} on its level.";
		}
		if (task == GoblinTask.Scoffing && destination != here)
			return $"Goblin {g} is scoffing at destination {destination} from tile {here}.";
		return null;
	}

	// The first broken rule of a goblin's fight fields, or null. A goblin in no fight has no partner and no clock.
	static string? FindFightViolation(SimState s, int g, GoblinTask task)
	{
		var partner = s.GoblinFightPartner[g];
		var ticks = s.GoblinFightTicks[g];
		if (!IsFightTask(task))
		{
			if (partner != -1)
				return $"Goblin {g} is {task} but has fight partner {partner}.";
			if (ticks != 0)
				return $"Goblin {g} is {task} but has a fight clock of {ticks}.";
			return null;
		}

		if (!(partner >= 0 && partner < s.GoblinCount && partner != g))
			return $"Goblin {g} is {task} with fight partner {partner}, which is not another goblin.";
		var length = task switch
		{
			GoblinTask.Pursuing => ChaosRules.PursuitTicks,
			GoblinTask.Recovering => ChaosRules.RecoveryTicks,
			_ => ChaosRules.ScuffleTicks,
		};
		if (ticks < 1 || ticks > length)
			return $"Goblin {g} is {task} with a fight clock of {ticks}, outside 1 to {length}.";
		if (task == GoblinTask.Recovering && s.GoblinActCooldown[g] < ticks)
			return $"Goblin {g} is Recovering for {ticks} ticks with a cooldown of only {s.GoblinActCooldown[g]}.";
		if (task is GoblinTask.Scuffling or GoblinTask.Scuffled)
		{
			var other = (GoblinTask)s.GoblinTask[partner];
			if (other != (task == GoblinTask.Scuffling ? GoblinTask.Scuffled : GoblinTask.Scuffling) || s.GoblinFightPartner[partner] != g)
				return $"Goblin {g} is {task} with goblin {partner}, who is {other} and does not name it back.";
			if (s.GoblinFightTicks[partner] != ticks)
				return $"Goblins {g} and {partner} scuffle on different clocks, {ticks} and {s.GoblinFightTicks[partner]}.";
			if (s.GoblinZ[g] != s.GoblinZ[partner] || Math.Abs(s.GoblinX[g] - s.GoblinX[partner]) + Math.Abs(s.GoblinY[g] - s.GoblinY[partner]) > 1)
				return $"Goblins {g} and {partner} scuffle without standing on one tile or beside each other.";
		}
		return null;
	}

	// The lower-numbered goblin that is tending the same tile as goblin g, or null.
	static int? FindOtherTender(SimState s, int g, int job)
	{
		for (var other = 0; other < g; other++)
			if (s.GoblinJobTile[other] == job && IsTendTask((GoblinTask)s.GoblinTask[other]))
				return other;
		return null;
	}

	// The lower-numbered goblin that is bringing Fungus to or brewing at the same Brewery tile as goblin g, or null.
	static int? FindOtherBrewer(SimState s, int g, int job)
	{
		for (var other = 0; other < g; other++)
			if (s.GoblinJobTile[other] == job && IsBreweryTask((GoblinTask)s.GoblinTask[other]))
				return other;
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
