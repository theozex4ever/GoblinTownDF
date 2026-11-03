namespace Goblins.Sim;

/// <summary>
/// Every number of the Needs, kept together so they can be tuned after play and shared with the Grog and Sleep Needs
/// that follow. A Need is a 0 to <see cref="MeterMax"/> meter that drains over time; below <see cref="CravingThreshold"/> the
/// goblin has a Craving, and at 0 it is Starving (Grub only).
/// </summary>
public static class NeedRules
{
	public const int MeterMax = 100;
	/// <summary>A goblin craves a Need when its meter falls below this.</summary>
	public const int CravingThreshold = 30;
	/// <summary>A meter starts at a number in this range, inclusive, drawn from the Rng.</summary>
	public const int StartMin = 70;
	public const int StartMax = 100;

	/// <summary>The Grub meter loses 1 point every this many ticks.</summary>
	public const int GrubDrainTicks = 25;

	/// <summary>Ticks to eat one unit, and what it adds to the meter.</summary>
	public const int EatTicks = 10;
	public const int EatRestore = 50;

	/// <summary>A Starving goblin walks and works on every this many ticks, so at half speed.</summary>
	public const int StarvingActEvery = 2;

	/// <summary>A meter's starting value: <see cref="StartMin"/> to <see cref="StartMax"/>, drawn from <paramref name="rng"/>.</summary>
	public static byte Start(SimRandom rng) => (byte)(StartMin + rng.NextInt(StartMax - StartMin + 1));

	public static bool IsCraving(int meter) => meter < CravingThreshold;

	public static bool IsStarving(int meter) => meter <= 0;

	/// <summary>The meter after a meal, held at <see cref="MeterMax"/>.</summary>
	public static byte Restore(int meter) => (byte)Math.Min(MeterMax, meter + EatRestore);

	/// <summary>False on the ticks a Starving goblin sits out; it acts on every <see cref="StarvingActEvery"/>th tick.</summary>
	public static bool ActsThisTick(int grub, long tick) => !IsStarving(grub) || tick % StarvingActEvery == 0;
}

/// <summary>The Needs' own system: the slow drain of every goblin's meters. Cravings and meals are worked by <see cref="JobSystem"/>.</summary>
public static class NeedsSystem
{
	/// <summary>Runs once per tick, after the tick counter has advanced: Grub drops 1 point on every <see cref="NeedRules.GrubDrainTicks"/>th tick.</summary>
	public static void Drain(SimState s)
	{
		if (s.Tick % NeedRules.GrubDrainTicks != 0)
			return;
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.GoblinGrub[g] > 0)
				s.GoblinGrub[g]--;
	}
}
