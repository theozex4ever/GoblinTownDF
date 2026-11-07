using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>
/// The Warren summary's numbers: per material, units stored on Piles and units loose (on the floor or in a goblin's hands),
/// and goblins by state. One instance is refilled each frame, so counting allocates nothing. Reads sim state; never writes it.
/// </summary>
public sealed class WarrenSummary
{
	readonly int[] _stored = new int[SimState.CountedMaterials];
	readonly int[] _loose = new int[SimState.CountedMaterials];

	public int Stored(Material material) => _stored[(int)material - 1];
	public int Loose(Material material) => _loose[(int)material - 1];

	public int Goblins { get; private set; }
	/// <summary>Sleeping, whether on a Heap, on the floor, or collapsed.</summary>
	public int Asleep { get; private set; }
	/// <summary>Awake with any Craving, whether or not it can be met.</summary>
	public int Craving { get; private set; }
	/// <summary>Awake, craving nothing, and Idle or Wandering.</summary>
	public int Idle { get; private set; }
	/// <summary>Awake, craving nothing, and on a Job.</summary>
	public int Working { get; private set; }
	/// <summary>Starving, whatever else it is doing; also counted in one of the states above.</summary>
	public int Starving { get; private set; }

	/// <summary>The one state a goblin is counted under; the first that applies wins.</summary>
	public enum GoblinState
	{
		Asleep,
		Craving,
		Idle,
		Working,
	}

	public static GoblinState StateOf(SimState s, int goblin)
	{
		var task = (GoblinTask)s.GoblinTask[goblin];
		if (task == GoblinTask.Sleeping)
			return GoblinState.Asleep;
		if (NeedRules.LeadingCraving(s.GoblinGrub[goblin], s.GoblinGrog[goblin], s.GoblinSleep[goblin]) != Need.None)
			return GoblinState.Craving;
		return task is GoblinTask.Idle or GoblinTask.Wandering ? GoblinState.Idle : GoblinState.Working;
	}

	public void Count(SimState s)
	{
		System.Array.Clear(_stored);
		System.Array.Clear(_loose);
		var tiles = s.Width * s.Height * s.Depth;
		for (var tile = 0; tile < tiles; tile++)
		for (var material = Material.Dirt; (int)material <= SimState.CountedMaterials; material++)
		{
			var count = s.ItemCount(tile, material);
			if (count > 0)
				(s.IsPile(tile) ? _stored : _loose)[(int)material - 1] += count;
		}

		Goblins = s.GoblinCount;
		Asleep = Craving = Idle = Working = Starving = 0;
		for (var g = 0; g < s.GoblinCount; g++)
		{
			if (s.GoblinCarry[g] != (byte)Material.None)
				_loose[s.GoblinCarry[g] - 1]++;
			if (NeedRules.IsStarving(s.GoblinGrub[g]))
				Starving++;

			switch (StateOf(s, g))
			{
				case GoblinState.Asleep: Asleep++; break;
				case GoblinState.Craving: Craving++; break;
				case GoblinState.Idle: Idle++; break;
				default: Working++; break;
			}
		}
	}
}
