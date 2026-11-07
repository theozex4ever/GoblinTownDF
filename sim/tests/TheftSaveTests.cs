using System.Text.Json;
using System.Text.Json.Nodes;
using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

// Theft and the whole-state save: a save taken at any stage continues the same, and a save that breaks the rules of a stage is rejected.
public partial class TheftTests
{
	[Theory]
	[MemberData(nameof(Stages))]
	public void A_save_taken_at_any_stage_of_a_theft_continues_identically(GoblinTask stage)
	{
		var sim = InStage(stage);
		var json = SimJson.Serialize(sim);

		var restored = SimJson.Deserialize(json);
		Assert.Equal(json, SimJson.Serialize(restored));
		Assert.Equal(stage, TaskOf(restored.State, 0));
		if (stage == GoblinTask.Stalled)
		{
			// Open a place in both worlds the same way, partway through.
			RunTicks(sim, 20);
			RunTicks(restored, 20);
			sim.Apply(new RemovePile(0, 11, 10, 11, 10));
			restored.Apply(new RemovePile(0, 11, 10, 11, 10));
		}
		RunTicks(sim, 400);
		RunTicks(restored, 400);

		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(restored));
		Assert.False(GoblinTasks.IsTheftTask(TaskOf(restored.State, 0)) && stage != GoblinTask.Stalled, "The theft should have run its course.");
	}

	[Fact]
	public void A_save_taken_mid_pickup_finishes_the_pickup_once_with_the_event_after_loading()
	{
		var sim = InStage(GoblinTask.Pilfering);
		RunTicks(sim, 2);
		var restored = SimJson.Deserialize(SimJson.Serialize(sim));
		restored.DrainEvents();

		RunTicks(restored, 3);

		Assert.Equal(GoblinTask.MakingOff, TaskOf(restored.State, 0));
		Assert.Single(restored.DrainEvents().OfType<TheftPickedUp>());
		// Three on the Pile and two loose beside it: the pickup moved one unit into the thief's hands and nothing else.
		Assert.Equal(5, Total(restored.State, Material.Grub));
		Assert.Equal(2, restored.State.ItemCount(At(restored.State, 10, 13), Material.Grub));
	}

	// A save of goblin 0 at the given stage, after the given change to the state.
	static string SaveAt(GoblinTask stage, Action<SimState>? change = null)
	{
		var sim = InStage(stage);
		change?.Invoke(sim.State);
		return SimJson.Serialize(sim);
	}

	static void Rejects(GoblinTask stage, Action<SimState> change) =>
		Assert.Throws<JsonException>(() => SimJson.Deserialize(SaveAt(stage, change)));

	[Theory]
	[InlineData(GoblinTask.Sneaking, 10, 1, 0)]
	[InlineData(GoblinTask.Pilfering, 10, 1, 0)]
	[InlineData(GoblinTask.Sneaking, 10, 13, 1)]
	[InlineData(GoblinTask.Pilfering, 10, 13, 1)]
	[InlineData(GoblinTask.Sneaking, 10, 13, 0)]
	[InlineData(GoblinTask.Pilfering, 10, 13, 0)]
	public void A_save_before_pickup_rejects_a_destination_outside_the_local_source_area(GoblinTask stage, int x, int y, int z)
	{
		// The source is (10, 13, 0): twelve steps away, another level, or the source itself could never be selected.
		Rejects(stage, s => s.GoblinTheftDestination[0] = At(s, x, y, z));
	}

	[Theory]
	[InlineData(GoblinTask.Sneaking)]
	[InlineData(GoblinTask.Pilfering)]
	public void A_save_before_pickup_accepts_a_destination_exactly_four_steps_from_the_source(GoblinTask stage)
	{
		var json = SaveAt(stage, s => s.GoblinTheftDestination[0] = At(s, 10, 9));

		Assert.Equal(json, SimJson.Serialize(SimJson.Deserialize(json)));
	}

	[Fact]
	public void A_save_without_the_theft_arrays_or_with_the_wrong_number_of_values_is_rejected()
	{
		foreach (var name in new[] { "GoblinTheftDestination", "GoblinTheftSupply" })
		{
			var root = JsonNode.Parse(SaveAt(GoblinTask.Sneaking))!.AsObject();
			root.Remove(name);
			Assert.Throws<JsonException>(() => SimJson.Deserialize(root.ToJsonString()));
		}
		Rejects(GoblinTask.Sneaking, s => s.GoblinTheftDestination = new int[3]);
		Rejects(GoblinTask.Sneaking, s => s.GoblinTheftSupply = new byte[3]);
	}

	[Fact]
	public void A_save_with_a_Sneaking_goblin_that_breaks_the_rules_of_the_stage_is_rejected()
	{
		SimJson.Deserialize(SaveAt(GoblinTask.Sneaking));
		Rejects(GoblinTask.Sneaking, s => s.GoblinTheftSupply[0] = (byte)Material.None);
		Rejects(GoblinTask.Sneaking, s => s.GoblinTheftSupply[0] = (byte)Material.Dirt);
		Rejects(GoblinTask.Sneaking, s => s.GoblinTheftDestination[0] = -1);
		Rejects(GoblinTask.Sneaking, s => s.GoblinTheftDestination[0] = s.TileKinds.Length);
		Rejects(GoblinTask.Sneaking, s => s.GoblinJobTile[0] = -1);
		Rejects(GoblinTask.Sneaking, s => s.GoblinJobTile[0] = s.TileKinds.Length);
		Rejects(GoblinTask.Sneaking, s => s.GoblinCarry[0] = (byte)Material.Grub);
		Rejects(GoblinTask.Sneaking, s => s.GoblinActCooldown[0] = 5);
		Rejects(GoblinTask.Sneaking, s => s.GoblinWanderTile[0] = 5);
		Rejects(GoblinTask.Sneaking, s => s.GoblinWorkTicks[0] = 1);
		Rejects(GoblinTask.Sneaking, s => s.GoblinFightPartner[0] = 0);
	}

	[Fact]
	public void A_save_with_a_Pilfering_goblin_that_breaks_the_rules_of_the_stage_is_rejected()
	{
		SimJson.Deserialize(SaveAt(GoblinTask.Pilfering));
		SimJson.Deserialize(SaveAt(GoblinTask.Pilfering, s => s.GoblinWorkTicks[0] = 4));
		Rejects(GoblinTask.Pilfering, s => s.GoblinWorkTicks[0] = 5);
		Rejects(GoblinTask.Pilfering, s => s.GoblinX[0] += 1);
		Rejects(GoblinTask.Pilfering, s => s.GoblinTheftDestination[0] = -1);
		Rejects(GoblinTask.Pilfering, s => s.GoblinCarry[0] = (byte)Material.Grub);
	}

	[Fact]
	public void A_save_with_a_MakingOff_goblin_that_breaks_the_rules_of_the_stage_is_rejected()
	{
		SimJson.Deserialize(SaveAt(GoblinTask.MakingOff));
		Rejects(GoblinTask.MakingOff, s => s.GoblinCarry[0] = (byte)Material.None);
		Rejects(GoblinTask.MakingOff, s => s.GoblinCarry[0] = (byte)Material.Grog);
		Rejects(GoblinTask.MakingOff, s => s.GoblinJobTile[0] = s.TileIndex(10, 13, 0));
		Rejects(GoblinTask.MakingOff, s => s.GoblinTheftDestination[0] = -1);
		Rejects(GoblinTask.MakingOff, s => s.GoblinTheftSupply[0] = (byte)Material.Grog);
		Rejects(GoblinTask.MakingOff, s => s.GoblinActCooldown[0] = 1);
		Rejects(GoblinTask.MakingOff, s => s.GoblinWorkTicks[0] = 1);
	}

	[Fact]
	public void A_save_with_a_Scoffing_goblin_that_breaks_the_rules_of_the_stage_is_rejected()
	{
		SimJson.Deserialize(SaveAt(GoblinTask.Scoffing));
		SimJson.Deserialize(SaveAt(GoblinTask.Scoffing, s => s.GoblinWorkTicks[0] = 9));
		Rejects(GoblinTask.Scoffing, s => s.GoblinWorkTicks[0] = 10);
		Rejects(GoblinTask.Scoffing, s => s.GoblinY[0] += 1);
		Rejects(GoblinTask.Scoffing, s => s.GoblinCarry[0] = (byte)Material.None);
		Rejects(GoblinTask.Scoffing, s => s.GoblinJobTile[0] = s.TileIndex(10, 13, 0));
		Rejects(GoblinTask.Scoffing, s => s.GoblinActCooldown[0] = 1);
	}

	[Fact]
	public void A_save_with_a_Stalled_goblin_that_breaks_the_rules_of_the_stage_is_rejected()
	{
		SimJson.Deserialize(SaveAt(GoblinTask.Stalled));
		Rejects(GoblinTask.Stalled, s => s.GoblinCarry[0] = (byte)Material.None);
		Rejects(GoblinTask.Stalled, s => s.GoblinTheftSupply[0] = (byte)Material.None);
		Rejects(GoblinTask.Stalled, s => s.GoblinTheftDestination[0] = s.TileIndex(11, 10, 0));
		Rejects(GoblinTask.Stalled, s => s.GoblinActCooldown[0] = 1);
		Rejects(GoblinTask.Stalled, s => s.GoblinWorkTicks[0] = 1);
	}

	[Fact]
	public void A_save_with_a_theft_plan_on_a_goblin_that_is_not_stealing_is_rejected()
	{
		SimJson.Deserialize(SaveAt(GoblinTask.Sneaking, s =>
		{
			GoblinTasks.EndTheft(s, 0, dropTile: -1);
		}));
		Rejects(GoblinTask.Sneaking, s =>
		{
			GoblinTasks.EndTheft(s, 0, dropTile: -1);
			s.GoblinTheftDestination[0] = 5;
		});
		Rejects(GoblinTask.Sneaking, s =>
		{
			GoblinTasks.EndTheft(s, 0, dropTile: -1);
			s.GoblinTheftSupply[0] = (byte)Material.Grub;
		});
	}
}

// Replay with every acting-out behaviour present.
public partial class TheftTests
{
	// The P3 warren with its supplies stored on a Pile and every goblin close to acting out: the same seed and the same commands play out the same.
	static Simulation Mischief(int seed)
	{
		var sim = Simulation.NewWorld(seed);
		sim.Apply(new PaintPile(0, 18, 16, 18, 16));
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 10, 11, 11));
		for (var g = 0; g < sim.State.GoblinCount; g++)
			sim.State.GoblinChaos[g] = (byte)(60 + g * 6);
		return sim;
	}

	static (int Shirks, int Fights, int Thefts) Play(Simulation sim, int ticks)
	{
		var (shirks, fights, thefts) = (0, 0, 0);
		for (var i = 0; i < ticks; i++)
		{
			sim.Advance(0.1);
			foreach (var e in sim.DrainEvents())
			{
				shirks += e is ShirkStarted ? 1 : 0;
				fights += e is ScuffleStarted ? 1 : 0;
				thefts += e is TheftPickedUp ? 1 : 0;
			}
		}
		return (shirks, fights, thefts);
	}

	[Fact]
	public void The_same_seed_and_commands_replay_the_same_with_shirking_fights_and_theft_all_happening_and_a_mid_run_save_agrees()
	{
		var a = Mischief(9);
		var (shirks, fights, thefts) = Play(a, 3000);
		Assert.True(shirks > 0 && fights > 0 && thefts > 0, $"The run must show all three behaviours: {shirks} shirks, {fights} fights, {thefts} thefts.");

		var b = Mischief(9);
		Play(b, 3000);
		Assert.Equal(SimJson.Serialize(a), SimJson.Serialize(b));
		var other = Mischief(10);
		Play(other, 3000);
		Assert.NotEqual(SimJson.Serialize(a), SimJson.Serialize(other));

		// A save taken part-way through, with whichever acts are under way, replays the rest identically.
		var c = Mischief(9);
		Play(c, 1200);
		var restored = SimJson.Deserialize(SimJson.Serialize(c));
		Play(c, 1800);
		Play(restored, 1800);
		Assert.Equal(SimJson.Serialize(a), SimJson.Serialize(c));
		Assert.Equal(SimJson.Serialize(c), SimJson.Serialize(restored));
	}
}
