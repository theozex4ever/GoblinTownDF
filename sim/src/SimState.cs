using System.Text.Json.Serialization;

namespace Goblins.Sim;

/// <summary>
/// All serialisable sim state, as flat arrays of plain data addressed by index (ADR 0002).
/// Tiles are indexed x + y*Width + z*Width*Height. Goblins are indexed 0..GoblinCount-1.
/// </summary>
public sealed class SimState
{
	public const int MaxGoblins = 30;

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
	// 1 on a designated tile some goblin can path to a tile it can dig from. Derived from the tiles, the
	// designations and the goblin positions, so it is not saved: loading rebuilds it (see JobSystem.RecheckReachability).
	[JsonIgnore]
	public byte[] Reachable { get; set; } = [];

	public int GoblinCount { get; set; }
	public int[] GoblinX { get; set; } = new int[MaxGoblins];
	public int[] GoblinY { get; set; } = new int[MaxGoblins];
	public int[] GoblinZ { get; set; } = new int[MaxGoblins];
	public byte[] GoblinRole { get; set; } = new byte[MaxGoblins];
	public byte[] GoblinTask { get; set; } = new byte[MaxGoblins];
	// The designated tile a goblin has claimed, or -1 while it has none.
	public int[] GoblinJobTile { get; set; } = NoJobs();
	// Ticks spent on the current step of a walk.
	public byte[] GoblinStepTicks { get; set; } = new byte[MaxGoblins];

	public int TileIndex(int x, int y, int z) => x + y * Width + z * Width * Height;

	public TileKind KindAt(int tile) => (TileKind)TileKinds[tile];

	public Material MaterialAt(int tile) => (Material)TileMaterials[tile];

	public DesignationKind DesignationAt(int tile) => (DesignationKind)Designations[tile];

	/// <summary>
	/// False for an undesignated tile, and for a designated one no goblin can reach yet. A cleared Designation
	/// is never reachable, even while its cached flag is stale.
	/// </summary>
	public bool IsReachable(int tile) => Designations[tile] != 0 && Reachable[tile] != 0;

	public static int[] NoJobs()
	{
		var jobs = new int[MaxGoblins];
		Array.Fill(jobs, -1);
		return jobs;
	}
}
