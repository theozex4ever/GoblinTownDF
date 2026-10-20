using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

public class TimeControlTests
{
	// 60 frames of one sixtieth of a second: one second of wall time.
	static int RunOneSecond(Simulation sim)
	{
		var ticks = 0;
		for (var frame = 0; frame < 60; frame++)
			ticks += sim.Advance(1.0 / 60.0);
		return ticks;
	}

	[Fact]
	public void A_new_world_runs_ten_ticks_per_second_at_1x()
	{
		var sim = Simulation.NewWorld(seed: 1);

		Assert.Equal(1, sim.State.Speed);
		Assert.False(sim.State.Paused);
		Assert.Equal(10, RunOneSecond(sim));
		Assert.Equal(10, sim.State.Tick);
	}

	[Theory]
	[InlineData(2, 20)]
	[InlineData(4, 40)]
	public void Speed_multiplies_ticks_per_second_without_changing_tick_length(int speed, int expectedTicks)
	{
		var sim = Simulation.NewWorld(seed: 1);
		sim.Apply(new SetSpeed(speed));

		Assert.Equal(expectedTicks, RunOneSecond(sim));
		Assert.Equal(expectedTicks, sim.State.Tick);
	}

	[Fact]
	public void Pause_stops_the_ticks_and_unpausing_resumes_them()
	{
		var sim = Simulation.NewWorld(seed: 1);
		sim.Apply(new SetPaused(true));

		Assert.Equal(0, RunOneSecond(sim));
		Assert.Equal(0, sim.State.Tick);

		sim.Apply(new SetPaused(false));
		Assert.Equal(10, RunOneSecond(sim));
	}

	[Fact]
	public void Time_spent_paused_is_not_owed_afterwards()
	{
		var sim = Simulation.NewWorld(seed: 1);
		sim.Apply(new SetPaused(true));
		sim.Advance(5.0);
		sim.Apply(new SetPaused(false));

		Assert.Equal(1, sim.Advance(0.1));
	}

	[Fact]
	public void A_long_stall_cannot_cause_a_burst_of_ticks()
	{
		var sim = Simulation.NewWorld(seed: 1);

		Assert.True(sim.Advance(60.0) <= 10);
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(-1.0)]
	[InlineData(0.0)]
	public void A_frame_time_that_is_not_positive_runs_nothing_and_owes_nothing(double frameSeconds)
	{
		var sim = Simulation.NewWorld(seed: 1);

		Assert.Equal(0, sim.Advance(frameSeconds));
		Assert.Equal(10, RunOneSecond(sim));
	}

	[Fact]
	public void Speed_must_be_1_2_or_4()
	{
		var sim = Simulation.NewWorld(seed: 1);

		Assert.Throws<ArgumentOutOfRangeException>(() => sim.Apply(new SetSpeed(3)));
		Assert.Equal(1, sim.State.Speed);
	}

	[Fact]
	public void Speed_and_pause_commands_emit_events_once_per_change()
	{
		var sim = Simulation.NewWorld(seed: 1);

		sim.Apply(new SetSpeed(2));
		sim.Apply(new SetSpeed(2));
		sim.Apply(new SetPaused(true));

		Assert.Equal(new SimEvent[] { new SpeedChanged(2), new PausedChanged(true) }, sim.DrainEvents());
		Assert.Empty(sim.DrainEvents());
	}
}
