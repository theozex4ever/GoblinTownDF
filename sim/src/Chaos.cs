namespace Goblins.Sim;

/// <summary>
/// Every number of Chaos, kept together so they can be tuned after play. Chaos is a goblin's accumulated frustration, 0 to
/// <see cref="Max"/>; at <see cref="ActingOutThreshold"/> an otherwise free goblin acts out. Shirking and fighting are the acts so far.
/// </summary>
public static class ChaosRules
{
	public const int Max = 100;

	/// <summary>On every this many sim ticks, each Need below the Craving threshold adds <see cref="UnmetNeedGain"/>, or a contented goblin loses <see cref="RecoveryAmount"/>.</summary>
	public const int UnmetNeedInterval = 10;
	public const int UnmetNeedGain = 1;
	public const int RecoveryAmount = 1;

	/// <summary>On every this many sim ticks, a waiting carrier gains <see cref="WaitingGain"/>, separately from any unmet Need.</summary>
	public const int WaitingInterval = 20;
	public const int WaitingGain = 1;

	/// <summary>Chaos a hauler gains, once, when another goblin consuming supplies cancels its claimed Haul.</summary>
	public const int LostHaulGain = 15;

	/// <summary>A goblin at or above this may begin acting out.</summary>
	public const int ActingOutThreshold = 70;

	/// <summary>Ticks a shirker loafs on the spot before it finishes.</summary>
	public const int ShirkTicks = 100;
	/// <summary>Chaos removed when an act is finished. An interrupted act gives none.</summary>
	public const int ActRelief = 40;
	/// <summary>Ticks after an act ends, finished or interrupted, before the goblin may act out again.</summary>
	public const int CooldownTicks = 300;

	/// <summary>Ticks an instigator may pursue its target before the attempt is abandoned.</summary>
	public const int PursuitTicks = 100;
	/// <summary>Ticks a scuffle lasts once contact is made.</summary>
	public const int ScuffleTicks = 30;
	/// <summary>Ticks both fighters take no work after a scuffle that ran its course.</summary>
	public const int RecoveryTicks = 100;
	/// <summary>Chaos the victim gains, once, when the scuffle starts.</summary>
	public const int VictimGain = 15;
}

/// <summary>Chaos' own system: how it rises and falls with the sim-wide clock, and who may act out. Starting an act is worked by <see cref="JobSystem"/>.</summary>
public static class ChaosSystem
{
	/// <summary>Adds to a goblin's Chaos, held within 0 to <see cref="ChaosRules.Max"/>. A negative amount removes.</summary>
	public static void Add(SimState s, int goblin, int amount) =>
		s.GoblinChaos[goblin] = (byte)Math.Clamp(s.GoblinChaos[goblin] + amount, 0, ChaosRules.Max);

	/// <summary>
	/// Runs once per tick, after the tick counter has advanced and the Needs have drained: counts down every cooldown, then on the sim-wide
	/// <see cref="ChaosRules.UnmetNeedInterval"/> ticks adds 1 for each Need below the Craving threshold, whether or not supply is reachable,
	/// except the Need the goblin is restoring right now by eating, drinking (a thief scoffing its stolen unit included) or sleeping; or, when no Need is below the threshold at all and the
	/// goblin is not a waiting carrier, removes 1. On the sim-wide <see cref="ChaosRules.WaitingInterval"/> ticks a waiting carrier gains 1 more.
	/// Nothing is stored beyond Chaos and the cooldown, so a save taken at any tick continues the same.
	/// </summary>
	public static void Tick(SimState s)
	{
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.GoblinActCooldown[g] > 0)
				s.GoblinActCooldown[g]--;
		GoblinTasks.CountDownFights(s);

		var needsInterval = s.Tick % ChaosRules.UnmetNeedInterval == 0;
		var waitingInterval = s.Tick % ChaosRules.WaitingInterval == 0;
		if (!needsInterval && !waitingInterval)
			return;

		for (var g = 0; g < s.GoblinCount; g++)
		{
			var waiting = NeedsSystem.IsWaitingCarrier(s, g);
			if (needsInterval)
			{
				var task = (GoblinTask)s.GoblinTask[g];
				var restoring = task is GoblinTask.Eating or GoblinTask.Drinking or GoblinTask.Sleeping ? GoblinTasks.NeedOf(task)
					: task == GoblinTask.Scoffing ? NeedOfSupply((Material)s.GoblinCarry[g])
					: Need.None;
				var low = 0;
				var unmet = 0;
				Count(s.GoblinGrub[g], Need.Grub, restoring, ref low, ref unmet);
				Count(s.GoblinGrog[g], Need.Grog, restoring, ref low, ref unmet);
				Count(s.GoblinSleep[g], Need.Sleep, restoring, ref low, ref unmet);
				if (unmet > 0)
					Add(s, g, unmet * ChaosRules.UnmetNeedGain);
				else if (low == 0 && !waiting)
					Add(s, g, -ChaosRules.RecoveryAmount);
			}
			if (waitingInterval && waiting)
				Add(s, g, ChaosRules.WaitingGain);
		}
	}

	// A thief consuming its stolen unit is eating or drinking it, so it is restoring the Need that supply feeds.
	static Need NeedOfSupply(Material supply) => supply switch
	{
		Material.Grub => Need.Grub,
		Material.Grog => Need.Grog,
		_ => Need.None,
	};

	// A Need below the Craving threshold is low; it is unmet unless the goblin is restoring it this very moment.
	static void Count(int meter, Need need, Need restoring, ref int low, ref int unmet)
	{
		if (!NeedRules.IsCraving(meter))
			return;
		low++;
		if (need != restoring)
			unmet++;
	}

	/// <summary>True when a goblin may begin to act out now: enough Chaos, no cooldown, and not already satisfying a Need, sleeping, shirking or in a fight.</summary>
	public static bool CanActOut(SimState s, int goblin)
	{
		var task = (GoblinTask)s.GoblinTask[goblin];
		return s.GoblinChaos[goblin] >= ChaosRules.ActingOutThreshold
			&& s.GoblinActCooldown[goblin] == 0
			&& !GoblinTasks.IsSatisfyingTask(task)
			&& !GoblinTasks.IsActingOutTask(task);
	}
}
