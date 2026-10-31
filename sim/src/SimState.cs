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
	/// Units of each material on each tile: <see cref="CountedMaterials"/> counts per tile, Dirt then Stone, at
	/// tile * CountedMaterials + (material - 1). Read through <see cref="ItemCount"/>, add through <see cref="AddItems"/>.
	/// </summary>
	public int[] TileItems { get; set; } = [];
	/// <summary>
	/// 1 on a Pile tile, which only a Floor tile can be. The flag is what stores a tile's units: units on a Pile
	/// tile are stored, units anywhere else are loose. Read through <see cref="IsPile"/>.
	/// </summary>
	public byte[] Piles { get; set; } = [];
	// 1 on a tile some goblin can path to. Derived and not saved, like Reachable; loading rebuilds it. Read through
	// IsHaulReachable, which also checks the tile still holds a loose unit with storage to go to.
	[JsonIgnore]
	public byte[] HaulReachable { get; set; } = [];
	// Per counted material (Dirt, then Stone): 1 when some Pile tile a goblin can path to has room for one more unit of it.
	// Derived and not saved; loading rebuilds it. Read through HasStorageRoom.
	[JsonIgnore]
	public byte[] StorageRoom { get; set; } = new byte[CountedMaterials];
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
	/// <summary>Ticks spent so far picking a unit up or dropping it off. Change through <see cref="GoblinTasks"/> only.</summary>
	public byte[] GoblinWorkTicks { get; set; } = new byte[MaxGoblins];

	/// <summary>The materials a tile keeps a count for: Dirt and Stone (every Material but None).</summary>
	public const int CountedMaterials = 2;

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

	/// <summary>Units of <paramref name="material"/> on a Pile tile; 0 anywhere else.</summary>
	public int StoredCount(int tile, Material material) => IsPile(tile) ? ItemCount(tile, material) : 0;

	/// <summary>Units of <paramref name="material"/> on a tile that is not a Pile; 0 on a Pile tile.</summary>
	public int LooseCount(int tile, Material material) => IsPile(tile) ? 0 : ItemCount(tile, material);

	/// <summary>The first material (Dirt, then Stone) with a loose unit on the tile, or None when it holds none or is a Pile.</summary>
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

	/// <summary>True when a reachable Pile tile has room for one more unit of <paramref name="material"/>. Cached; see <see cref="JobSystem.RecheckReachability"/>.</summary>
	public bool HasStorageRoom(Material material) => StorageRoom[(int)material - 1] != 0;

	/// <summary>Whether a Pile tile has room for one more unit of <paramref name="material"/>. A tile that is not a Pile has none.</summary>
	public bool PileHasRoom(int tile, Material material) => IsPile(tile) && ItemCount(tile, material) < PileCapacity;

	/// <summary>The first material (Dirt, then Stone) with a loose unit on the tile that has storage to go to, or None.</summary>
	public Material HaulableMaterial(int tile)
	{
		for (var material = Material.Dirt; (int)material <= CountedMaterials; material++)
			if (LooseCount(tile, material) > 0 && HasStorageRoom(material))
				return material;
		return Material.None;
	}

	/// <summary>Loose units on a tile whose material has storage to go to. A blocked material does not count, so it never stops another from being hauled.</summary>
	public int HaulableTotal(int tile)
	{
		var total = 0;
		for (var material = Material.Dirt; (int)material <= CountedMaterials; material++)
			if (HasStorageRoom(material))
				total += LooseCount(tile, material);
		return total;
	}

	static int ItemSlot(int tile, Material material)
	{
		if ((int)material < 1 || (int)material > CountedMaterials)
			throw new ArgumentOutOfRangeException(nameof(material), material, "Tiles count Dirt and Stone only.");
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
	/// Pile tile a goblin can reach has room for its material. True when at least one of its units can be hauled. A unit
	/// picked up since the last recheck is never reachable, even while its cached flag is stale.
	/// </summary>
	public bool IsHaulReachable(int tile) => HaulReachable[tile] != 0 && HaulableMaterial(tile) != Material.None;

	/// <summary>Whether the loose units of <paramref name="material"/> on a tile can be hauled: there are some, and a goblin can reach both them and room for them.</summary>
	public bool IsHaulReachable(int tile, Material material) =>
		HaulReachable[tile] != 0 && LooseCount(tile, material) > 0 && HasStorageRoom(material);

	public static int[] NoJobs()
	{
		var jobs = new int[MaxGoblins];
		Array.Fill(jobs, -1);
		return jobs;
	}
}
