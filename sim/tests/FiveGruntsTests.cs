using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

public class FiveGruntsTests
{
	const double OneTick = 0.1;

	// Dig a tile of Solid standing right beside a Floor tile: one step to the stand tile, then the dig.
	[Theory]
	[InlineData(1, Material.Dirt, 20)]
	[InlineData(2, Material.Stone, 40)]
	public void A_tile_takes_its_materials_dig_ticks_to_dig(int z, Material material, int digTicks)
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		// Open a floor tile on the level next to the target, so the goblin can stand beside it.
		var target = s.TileIndex(20, 20, z);
		s.TileKinds[s.TileIndex(19, 20, z)] = (byte)TileKind.Floor;
		(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]) = (19, 20, z);
		sim.Apply(new Designate(DesignationKind.Dig, z, 20, 20, 20, 20));
		Assert.Equal(material, s.MaterialAt(target));

		RunTicks(sim, digTicks - 1);
		Assert.Equal(TileKind.Solid, s.KindAt(target));
		RunTicks(sim, 1);

		Assert.Equal(TileKind.Floor, s.KindAt(target));
		Assert.Equal(new SimEvent[] { new TileDug(20, 20, z, material) }, sim.DrainEvents());
	}

	[Fact]
	public void Five_goblins_digging_a_room_never_claim_the_same_Designation()
	{
		var sim = Simulation.NewWorld(seed: 3);
		var s = sim.State;
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 12, 12, 20, 20));

		// RunTicks asserts the GoblinTasks invariant (no shared claim) after every tick.
		var busiest = 0;
		for (var i = 0; i < 600; i++)
		{
			RunTicks(sim, 1);
			var claimed = 0;
			for (var g = 0; g < s.GoblinCount; g++)
				if (s.GoblinJobTile[g] >= 0)
					claimed++;
			busiest = Math.Max(busiest, claimed);
		}

		Assert.Equal(5, busiest);
		Assert.Contains(s.TileKinds, k => k == (byte)TileKind.Stairs);
	}

	static readonly (int Tick, Command Command)[] Log =
	[
		(0, new Designate(DesignationKind.DigStairs, 0, 12, 12, 20, 14)),
		(5, new Designate(DesignationKind.DigStairs, 0, 14, 18, 18, 20)),
		(40, new CancelDesignation(0, 14, 13, 14, 13)),
		(120, new Designate(DesignationKind.DigStairs, 0, 10, 16, 10, 16)),
	];

	static Simulation Replay(int seed, int ticks)
	{
		var sim = Simulation.NewWorld(seed);
		var next = 0;
		for (var tick = 0; tick < ticks; tick++)
		{
			while (next < Log.Length && Log[next].Tick == tick)
				sim.Apply(Log[next++].Command);
			sim.Advance(OneTick);
		}
		return sim;
	}

	[Fact]
	public void The_same_seed_and_command_log_replay_to_identical_state_with_five_goblins()
	{
		var first = Replay(seed: 11, ticks: 400);
		var second = Replay(seed: 11, ticks: 400);

		Assert.Equal(5, first.State.GoblinCount);
		Assert.Equal(SimJson.Serialize(first), SimJson.Serialize(second));
		Assert.Contains(first.State.TileKinds, k => k == (byte)TileKind.Stairs);
	}

	[Fact]
	public void Five_goblins_round_trip_through_JSON_and_carry_on_exactly_as_the_original_does()
	{
		var original = Replay(seed: 11, ticks: 150);
		var restored = SimJson.Deserialize(SimJson.Serialize(original));
		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));

		RunTicks(original, 100);
		RunTicks(restored, 100);

		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));
	}
}
