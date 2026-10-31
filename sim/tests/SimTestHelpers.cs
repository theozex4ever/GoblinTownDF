using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

static class SimTestHelpers
{
	// One tick of wall time at 1×.
	const double OneTick = 0.1;

	/// <summary>
	/// A new world trimmed to goblin 0 alone, for tests that script one goblin's route and would
	/// otherwise share the work with the other four Grunts.
	/// </summary>
	public static Simulation NewWorldWithOneGrunt(int seed)
	{
		var sim = Simulation.NewWorld(seed);
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
