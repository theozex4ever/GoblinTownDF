using System.Text.Json.Serialization;

namespace Goblins.Sim;

/// <summary>
/// All serialisable sim state, as flat arrays of plain data addressed by index (ADR 0002).
/// Tiles are indexed x + y*Width + z*Width*Height. Goblins are indexed 0..GoblinCount-1.
/// </summary>
public sealed class SimState
{
	public const int MaxGoblins = 30;
	/// <summary>Units of each material a Pile tile holds. Materials count separately, so a tile can hold this many of each.</summary>
	public const int PileCapacity = 30;

	public int Seed { get; set; }
	public long Tick { get; set; }
	public int Speed { get; set; } = 1;
	public bool Paused { get; set; }
	// Fractional ticks owed from frame time, in [0, 1). Saved so a restored sim ticks on the same frames.
	public double TickDebt { get; set; }
	public SimRandom Rng { get; set; } = new();

	public int Width { get; set; }
	public int Height { get; set; }
	public int Depth { get; set; }
	public byte[] TileKinds { get; set; } = [];
	public byte[] TileMaterials { get; set; } = [];
	// A DesignationKind per tile, and ticks spent digging it so far. Cancelling a designation clears the progress.
	public byte[] Designations { get; set; } = [];
	public byte[] DigProgress { get; set; } = [];
	/// <summary>
	/// Units of each material on each tile: <see cref="CountedMaterials"/> counts per tile, in Material order, at
	/// tile * CountedMaterials + (material - 1). Read through <see cref="ItemCount"/>, add through <see cref="AddItems"/>.
	/// </summary>
	public int[] TileItems { get; set; } = [];
	/// <summary>
	/// 1 on a Pile tile, which only a Floor tile can be. The flag is what stores a tile's units: units on a Pile
	/// tile are stored, units anywhere else are loose. Read through <see cref="IsPile"/>.
	/// </summary>
	public byte[] Piles { get; set; } = [];
	/// <summary>
	/// A <see cref="RoomKind"/> per tile for every Room but the Pile, which <see cref="Piles"/> keeps because its flag is what stores
	/// units. A tile holds one Room, so a tile is set in at most one of the two. Read through <see cref="RoomAt"/>.
	/// </summary>
	public byte[] Rooms { get; set; } = [];
	/// <summary>
	/// Per farm tile (Grub Pit or Fungus Farm), the units its last Tend yielded that have not spawned yet: 0 to
	/// <see cref="ProductionRules.GrubPerTend"/> on a Grub Pit, 0 or <see cref="ProductionRules.FungusPerTend"/> on a Fungus Farm.
	/// A tile with some left is still producing and offers no Tend. <see cref="FarmSystem"/> counts it down.
	/// </summary>
	public byte[] FarmUnspawned { get; set; } = [];
	/// <summary>
	/// Ticks counted toward the next unit of a producing farm tile, 0 while nothing is left to spawn; below
	/// <see cref="ProductionRules.GrubSpawnTicks"/> or <see cref="ProductionRules.FungusGrowTicks"/>, which is past a byte.
	/// </summary>
	public ushort[] FarmTicks { get; set; } = [];
	// Per tile, the region (1 to MaxGoblins) of walkable tiles that a goblin stands in and can walk to, or 0 for a tile no
	// goblin can path to. Goblins never leave their region, so a unit can only be stored on a Pile tile of its own region.
	// Derived and not saved, like Reachable; loading rebuilds it. Read through IsHaulReachable.
	[JsonIgnore]
	public byte[] HaulRegion { get; set; } = [];
	// 1 on a tile some goblin can path to that holds a loose unit: a cheap filter for the claim loop, which looks at every tile.
	// Derived and not saved; loading rebuilds it. Read through IsHaulReachable, which also checks the unit's storage.
	[JsonIgnore]
	public byte[] HaulReachable { get; set; } = [];
	// Per region and counted material (in Material order), at (region - 1) * CountedMaterials + (material - 1): 1 when some
	// Pile tile of the region has room for one more unit of the material. Derived and not saved; loading rebuilds it. Read through HasStorageRoom.
	[JsonIgnore]
	public byte[] StorageRoom { get; set; } = new byte[MaxGoblins * CountedMaterials];
	// 1 on a designated tile some goblin can path to a tile it can dig from. Derived from the tiles, the
	// designations and the goblin positions, so it is not saved: loading rebuilds it (see JobSystem.RecheckReachability).
	[JsonIgnore]
	public byte[] Reachable { get; set; } = [];

	public int GoblinCount { get; set; }
	public int[] GoblinX { get; set; } = new int[MaxGoblins];
	public int[] GoblinY { get; set; } = new int[MaxGoblins];
	public int[] GoblinZ { get; set; } = new int[MaxGoblins];
	public byte[] GoblinRole { get; set; } = new byte[MaxGoblins];
	/// <summary>A <see cref="Goblins.Sim.GoblinTask"/> per goblin. Change through <see cref="GoblinTasks"/> only.</summary>
	public byte[] GoblinTask { get; set; } = new byte[MaxGoblins];
	/// <summary>
	/// The tile a goblin has claimed, or -1 while it has none: a designated tile while Walking or Digging, a tile with a
	/// loose unit while Fetching or PickingUp. Change through <see cref="GoblinTasks"/> only.
	/// </summary>
	public int[] GoblinJobTile { get; set; } = NoJobs();
	/// <summary>
	/// Ticks spent on the current step of a walk. Change through <see cref="GoblinTasks"/> only; the one
	/// exception is <see cref="JobSystem"/>'s movement, which counts the ticks and resets them after each step.
	/// </summary>
	public byte[] GoblinStepTicks { get; set; } = new byte[MaxGoblins];
	/// <summary>The tile a wandering goblin is walking to, or -1 while it is not wandering. Change through <see cref="GoblinTasks"/> only.</summary>
	public int[] GoblinWanderTile { get; set; } = NoJobs();
	/// <summary>
	/// Ticks an idle goblin still pauses before it wanders; 0 until the pause is drawn from the Rng.
	/// Change through <see cref="GoblinTasks"/> only.
	/// </summary>
	public byte[] GoblinWanderTicks { get; set; } = new byte[MaxGoblins];
	/// <summary>The <see cref="Goblins.Sim.Material"/> a goblin carries while Carrying or Dropping; None otherwise. Change through <see cref="GoblinTasks"/> only.</summary>
	public byte[] GoblinCarry { get; set; } = new byte[MaxGoblins];
	/// <summary>
	/// Ticks spent so far picking a unit up, dropping it off, eating it, drinking it, tending or sleeping; while Sleeping, ticks counted
	/// toward the next point of Sleep. Change through <see cref="GoblinTasks"/> only.
	/// </summary>
	public byte[] GoblinWorkTicks { get; set; } = new byte[MaxGoblins];
	/// <summary>
	/// The Grub Need per goblin, 0 to <see cref="NeedRules.MeterMax"/>. <see cref="NeedsSystem"/> drains it and a meal in
	/// <see cref="GoblinTasks"/> restores it. Numbers live in <see cref="NeedRules"/>.
	/// </summary>
	public byte[] GoblinGrub { get; set; } = new byte[MaxGoblins];
	/// <summary>The Grog Need per goblin, kept as <see cref="GoblinGrub"/> is. A waiting carrier drains it at double rate.
	/// Empty until a world fills it, so a save written before Grog existed is rejected rather than read as every goblin at 0.</summary>
	public byte[] GoblinGrog { get; set; } = [];
	/// <summary>
	/// The Sleep Need per goblin, kept as <see cref="GoblinGrub"/> is. A sleeping goblin does not drain it: it only climbs, one
	/// point per <see cref="NeedRules.HeapRecoveryTicks"/> or <see cref="NeedRules.FloorRecoveryTicks"/> ticks. Empty until a world
	/// fills it, so a save written before Sleep existed is rejected rather than read as every goblin at 0.
	/// </summary>
	public byte[] GoblinSleep { get; set; } = [];

	/// <summary>
	/// Chaos per goblin, 0 to <see cref="ChaosRules.Max"/>: accumulated frustration that <see cref="ChaosSystem"/> raises from unmet Needs,
	/// waiting for storage and lost Hauls, and lowers when the goblin is content. Empty until a world fills it, so a save written before
	/// Chaos existed is rejected rather than read as every goblin calm.
	/// </summary>
	public byte[] GoblinChaos { get; set; } = [];
	/// <summary>
	/// Ticks left before a goblin may act out again, 0 when it may: set to <see cref="ChaosRules.CooldownTicks"/> when an act ends, whether it
	/// finished or was interrupted, and counted down by <see cref="ChaosSystem"/>. Empty until a world fills it, like <see cref="GoblinChaos"/>.
	/// </summary>
	public ushort[] GoblinActCooldown { get; set; } = [];

	/// <summary>
	/// The other goblin of a fight, or -1 while a goblin is in none: the target while Pursuing, the victim while Scuffling, the instigator while
	/// Scuffled, and the one it fought while Recovering. Change through <see cref="GoblinTasks"/> only. Empty until a world fills it, like <see cref="GoblinChaos"/>.
	/// </summary>
	public int[] GoblinFightPartner { get; set; } = [];
	/// <summary>
	/// Sim ticks left in the fight stage a goblin is in (pursuit, scuffle or recovery), 0 otherwise. <see cref="GoblinTasks"/> counts it down on
	/// every sim tick whatever the goblin is doing, so a Starving goblin is no slower, and the two fighters of a scuffle always hold the same number.
	/// Empty until a world fills it, like <see cref="GoblinChaos"/>.
	/// </summary>
	public ushort[] GoblinFightTicks { get; set; } = [];

	/// <summary>
	/// The non-Pile tile a thief has chosen to consume its unit on, or -1 while a goblin is not stealing: fixed when the theft begins and never replaced.
	/// Change through <see cref="GoblinTasks"/> only. Empty until a world fills it, like <see cref="GoblinChaos"/>.
	/// </summary>
	public int[] GoblinTheftDestination { get; set; } = [];
	/// <summary>
	/// The <see cref="Material"/> (Grub or Grog) a thief set out to take, and so the one it carries once it has; None while a goblin is not stealing.
	/// Change through <see cref="GoblinTasks"/> only. Empty until a world fills it, like <see cref="GoblinChaos"/>.
	/// </summary>
	public byte[] GoblinTheftSupply { get; set; } = [];

	/// <summary>The per-goblin meter of a Need.</summary>
	public byte[] MeterOf(Need need) => need switch
	{
		Need.Grub => GoblinGrub,
		Need.Grog => GoblinGrog,
		Need.Sleep => GoblinSleep,
		_ => throw new ArgumentOutOfRangeException(nameof(need), need, "Only Grub, Grog and Sleep have a meter."),
	};

	/// <summary>The materials a tile keeps a count for: every <see cref="Goblins.Sim.Material"/> but None, which the enum numbers 1 upward without gaps.</summary>
	public static readonly int CountedMaterials = Enum.GetValues<Material>().Length - 1;

	public int TileIndex(int x, int y, int z) => x + y * Width + z * Width * Height;

	public int ItemCount(int tile, Material material) => TileItems[ItemSlot(tile, material)];

	public void AddItems(int tile, Material material, int units)
	{
		if (units < 0)
			throw new ArgumentOutOfRangeException(nameof(units), units, "Use RemoveItems to take units away.");
		TileItems[ItemSlot(tile, material)] += units;
	}

	/// <summary>Takes units off a tile. Throws rather than let a count go below zero.</summary>
	public void RemoveItems(int tile, Material material, int units)
	{
		if (units < 0)
			throw new ArgumentOutOfRangeException(nameof(units), units, "Use AddItems to put units down.");
		var slot = ItemSlot(tile, material);
		if (TileItems[slot] < units)
			throw new InvalidOperationException($"Tile {tile} holds {TileItems[slot]} {material}, not {units}.");
		TileItems[slot] -= units;
	}

	public bool IsPile(int tile) => Piles[tile] != 0;

	/// <summary>The Room painted on a tile, or None. A tile holds one Room.</summary>
	public RoomKind RoomAt(int tile) => IsPile(tile) ? RoomKind.Pile : (RoomKind)Rooms[tile];

	/// <summary>
	/// Paints <paramref name="room"/> on a tile, or clears it with None, replacing any Room the tile held. A Grub Pit tile that goes
	/// loses the units it has not yet spawned. The caller has checked the tile can take it; <see cref="RoomRules"/> does.
	/// </summary>
	public void SetRoom(int tile, RoomKind room)
	{
		if (!Enum.IsDefined(room))
			throw new ArgumentOutOfRangeException(nameof(room), room, "Unknown Room.");
		Piles[tile] = (byte)(room == RoomKind.Pile ? 1 : 0);
		Rooms[tile] = (byte)(room == RoomKind.Pile ? RoomKind.None : room);
		FarmUnspawned[tile] = 0;
		FarmTicks[tile] = 0;
	}

	/// <summary>Units of <paramref name="material"/> on a Pile tile; 0 anywhere else.</summary>
	public int StoredCount(int tile, Material material) => IsPile(tile) ? ItemCount(tile, material) : 0;

	/// <summary>Units of <paramref name="material"/> on a tile that is not a Pile; 0 on a Pile tile.</summary>
	public int LooseCount(int tile, Material material) => IsPile(tile) ? 0 : ItemCount(tile, material);

	/// <summary>The first material (in Material order) with a loose unit on the tile, or None when it holds none or is a Pile.</summary>
	public Material LooseMaterial(int tile)
	{
		for (var material = Material.Dirt; (int)material <= CountedMaterials; material++)
			if (LooseCount(tile, material) > 0)
				return material;
		return Material.None;
	}

	/// <summary>Loose units of every material on a tile; 0 on a Pile tile. Each one is its own Haul, so this is how many goblins may claim the tile.</summary>
	public int LooseTotal(int tile)
	{
		var total = 0;
		for (var material = Material.Dirt; (int)material <= CountedMaterials; material++)
			total += LooseCount(tile, material);
		return total;
	}

	/// <summary>
	/// True when a Pile tile in the same region as <paramref name="tile"/> has room for one more unit of <paramref name="material"/>;
	/// false for a tile no goblin can reach. Cached; see <see cref="JobSystem.RecheckReachability"/>.
	/// </summary>
	public bool HasStorageRoom(int tile, Material material)
	{
		var region = HaulRegion[tile];
		return region != 0 && StorageRoom[(region - 1) * CountedMaterials + (int)material - 1] != 0;
	}

	/// <summary>Whether a Pile tile has room for one more unit of <paramref name="material"/>. A tile that is not a Pile has none.</summary>
	public bool PileHasRoom(int tile, Material material) => IsPile(tile) && ItemCount(tile, material) < PileCapacity;

	/// <summary>The first material (in Material order) with a loose unit on the tile that has storage to go to, or None.</summary>
	public Material HaulableMaterial(int tile)
	{
		for (var material = Material.Dirt; (int)material <= CountedMaterials; material++)
			if (LooseCount(tile, material) > 0 && HasStorageRoom(tile, material))
				return material;
		return Material.None;
	}

	/// <summary>Loose units on a tile whose material has storage to go to. A blocked material does not count, so it never stops another from being hauled.</summary>
	public int HaulableTotal(int tile)
	{
		var total = 0;
		for (var material = Material.Dirt; (int)material <= CountedMaterials; material++)
			if (HasStorageRoom(tile, material))
				total += LooseCount(tile, material);
		return total;
	}

	static int ItemSlot(int tile, Material material)
	{
		if ((int)material < 1 || (int)material > CountedMaterials)
			throw new ArgumentOutOfRangeException(nameof(material), material, "Tiles count a real Material only, not None.");
		return tile * CountedMaterials + (int)material - 1;
	}

	public TileKind KindAt(int tile) => (TileKind)TileKinds[tile];

	public Material MaterialAt(int tile) => (Material)TileMaterials[tile];

	public DesignationKind DesignationAt(int tile) => (DesignationKind)Designations[tile];

	/// <summary>
	/// False for an undesignated tile, and for a designated one no goblin can reach yet. A cleared Designation
	/// is never reachable, even while its cached flag is stale.
	/// </summary>
	public bool IsReachable(int tile) => Designations[tile] != 0 && Reachable[tile] != 0;

	/// <summary>
	/// False for a tile with no loose unit on it, and for one whose Hauls all wait: no goblin can reach the unit, or no
	/// Pile tile in the unit's region has room for its material. True when at least one of its units can be hauled. A unit
	/// picked up since the last recheck is never reachable, even while its cached flags are stale.
	/// </summary>
	public bool IsHaulReachable(int tile) => HaulReachable[tile] != 0 && HaulableMaterial(tile) != Material.None;

	/// <summary>Whether the loose units of <paramref name="material"/> on a tile can be hauled: there are some, and a goblin can reach both them and room for them in their region.</summary>
	public bool IsHaulReachable(int tile, Material material) =>
		HaulReachable[tile] != 0 && LooseCount(tile, material) > 0 && HasStorageRoom(tile, material);

	public static int[] NoJobs()
	{
		var jobs = new int[MaxGoblins];
		Array.Fill(jobs, -1);
		return jobs;
	}
}
