using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

static class SimTestHelpers
{
	// One tick of wall time at 1×.
	const double OneTick = 0.1;

	/// <summary>
	/// A new world as P2 made it: the first five Grunts of the cluster and no starting Grub or Grog on the ground. For
	/// tests that script hauls, claims or item counts and would otherwise be thrown off by the two extra Grunts and
	/// the 21 loose units P3 adds.
	/// </summary>
	public static Simulation NewWorldWithFiveGrunts(int seed)
	{
		var sim = Simulation.NewWorld(seed);
		sim.State.GoblinCount = 5;
		Array.Clear(sim.State.TileItems);
		sim.RecheckReachability();
		return sim;
	}

	/// <summary>
	/// A new world trimmed to goblin 0 alone, with no starting supplies, for tests that script one goblin's route and
	/// would otherwise share the work with the other Grunts.
	/// </summary>
	public static Simulation NewWorldWithOneGrunt(int seed)
	{
		var sim = NewWorldWithFiveGrunts(seed);
		sim.State.GoblinCount = 1;
		sim.RecheckReachability();
		return sim;
	}

	/// <summary>
	/// Runs <paramref name="ticks"/> ticks one at a time, and after each one asserts that every goblin's
	/// Task still satisfies <see cref="GoblinTasks.FindViolation"/>, printing the violation if not.
	/// </summary>
	public static void RunTicks(Simulation sim, int ticks)
	{
		for (var i = 0; i < ticks; i++)
		{
			Assert.Equal(1, sim.Advance(OneTick));
			var violation = GoblinTasks.FindViolation(sim.State);
			Assert.True(violation is null, $"Tick {sim.State.Tick}: {violation}");
		}
	}
}
