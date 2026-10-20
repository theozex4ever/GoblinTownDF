using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

public class SimRandomTests
{
	static int[] Draw(SimRandom rng) => Enumerable.Range(0, 20).Select(_ => rng.NextInt(1000)).ToArray();

	[Fact]
	public void The_same_seed_draws_the_same_numbers()
	{
		Assert.Equal(Draw(new SimRandom(5)), Draw(new SimRandom(5)));
	}

	[Fact]
	public void Different_seeds_draw_different_numbers()
	{
		Assert.NotEqual(Draw(new SimRandom(5)), Draw(new SimRandom(6)));
	}

	[Fact]
	public void NextInt_stays_below_the_bound()
	{
		var rng = new SimRandom(5);
		for (var i = 0; i < 1000; i++)
			Assert.InRange(rng.NextInt(6), 0, 5);
	}

	[Fact]
	public void The_world_rng_is_seeded_at_creation()
	{
		var a = Simulation.NewWorld(seed: 9);
		var b = Simulation.NewWorld(seed: 9);
		var c = Simulation.NewWorld(seed: 10);

		Assert.Equal(a.State.Rng.State, b.State.Rng.State);
		Assert.NotEqual(a.State.Rng.State, c.State.Rng.State);
	}
}
