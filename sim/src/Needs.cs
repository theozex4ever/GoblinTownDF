namespace Goblins.Sim;

/// <summary>
/// Every number of the Needs and of the production that feeds them (<see cref="ProductionRules"/> below), kept together so they can be tuned after play and shared with the Sleep Need that follows.
/// A Need is a 0 to <see cref="MeterMax"/> meter that drains over time; below <see cref="CravingThreshold"/> the
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
	/// <summary>The Grog meter loses 1 point every this many ticks, and a waiting carrier's loses <see cref="WaitingGrogDrain"/>.</summary>
	public const int GrogDrainTicks = 35;
	/// <summary>Points a waiting carrier's Grog meter loses on each <see cref="GrogDrainTicks"/>th tick: double the rate of any other goblin.</summary>
	public const int WaitingGrogDrain = 2;

	/// <summary>Ticks to eat one unit, and what it adds to the meter.</summary>
	public const int EatTicks = 10;
	public const int EatRestore = 50;

	/// <summary>A Starving goblin walks and works on every this many ticks, so at half speed.</summary>
	public const int StarvingActEvery = 2;

	/// <summary>A meter's starting value: <see cref="StartMin"/> to <see cref="StartMax"/>, drawn from <paramref name="rng"/>.</summary>
	public static byte Start(SimRandom rng) => (byte)(StartMin + rng.NextInt(StartMax - StartMin + 1));

	public static bool IsCraving(int meter) => meter < CravingThreshold;

	public static bool IsStarving(int meter) => meter <= 0;

	/// <summary>The meter after a meal or a drink, held at <see cref="MeterMax"/>.</summary>
	public static byte Restore(int meter) => (byte)Math.Min(MeterMax, meter + EatRestore);

	/// <summary>False on the ticks a Starving goblin sits out; it acts on every <see cref="StarvingActEvery"/>th tick.</summary>
	public static bool ActsThisTick(int grub, long tick) => !IsStarving(grub) || tick % StarvingActEvery == 0;

	/// <summary>
	/// The Need that wins when a goblin has Cravings for both: the lower meter, and Grub on a tie. Returns Grub or Grog, the
	/// Material that satisfies the Need; the loser is <see cref="Other"/>.
	/// </summary>
	public static Material WinningNeed(int grub, int grog) => grub <= grog ? Material.Grub : Material.Grog;

	public static Material Other(Material need) => need == Material.Grub ? Material.Grog : Material.Grub;

	/// <summary>The Need a goblin would act on now, or None when it has no Craving: the lower of the Cravings, Grub on a tie.</summary>
	public static Material LeadingCraving(int grub, int grog)
	{
		var winner = WinningNeed(grub, grog);
		var winning = winner == Material.Grub ? grub : grog;
		var other = winner == Material.Grub ? grog : grub;
		return IsCraving(winning) ? winner : IsCraving(other) ? Other(winner) : Material.None;
	}
}

/// <summary>The numbers of the Rooms that produce, kept beside <see cref="NeedRules"/> so both can be tuned after play.</summary>
public static class ProductionRules
{
	/// <summary>Working ticks one Tend of a Grub Pit tile takes.</summary>
	public const int TendTicks = 20;
	/// <summary>Grub one Tend yields, spawned one at a time.</summary>
	public const int GrubPerTend = 3;
	/// <summary>Ticks from the end of a Tend to its first Grub, and between one Grub and the next.</summary>
	public const int GrubSpawnTicks = 200;
}

/// <summary>The Needs' own system: the slow drain of every goblin's meters. Cravings and meals are worked by <see cref="JobSystem"/>.</summary>
public static class NeedsSystem
{
	/// <summary>
	/// Runs once per tick, after the tick counter has advanced: Grub drops 1 point on every <see cref="NeedRules.GrubDrainTicks"/>th
	/// tick, and Grog 1 point on every <see cref="NeedRules.GrogDrainTicks"/>th, or <see cref="NeedRules.WaitingGrogDrain"/> for a
	/// waiting carrier. Nothing is stored beyond the meters, so a save taken at any tick drains the same.
	/// </summary>
	public static void Drain(SimState s)
	{
		if (s.Tick % NeedRules.GrubDrainTicks == 0)
			for (var g = 0; g < s.GoblinCount; g++)
				if (s.GoblinGrub[g] > 0)
					s.GoblinGrub[g]--;
		if (s.Tick % NeedRules.GrogDrainTicks == 0)
			for (var g = 0; g < s.GoblinCount; g++)
				s.GoblinGrog[g] = (byte)Math.Max(0, s.GoblinGrog[g] - (IsWaitingCarrier(s, g) ? NeedRules.WaitingGrogDrain : 1));
	}

	/// <summary>
	/// True for a goblin holding a unit that no Pile in its reach has room for: Carrying (or Dropping, which has just lost its
	/// room) with no storage left for its material in its region. Reads the storage cache that
	/// <see cref="JobSystem.RecheckReachability"/> keeps, as the claim loop does.
	/// </summary>
	public static bool IsWaitingCarrier(SimState s, int goblin)
	{
		if ((GoblinTask)s.GoblinTask[goblin] is not (GoblinTask.Carrying or GoblinTask.Dropping))
			return false;
		var here = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		return !s.HasStorageRoom(here, (Material)s.GoblinCarry[goblin]);
	}
}
