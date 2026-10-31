using System.Text.Json;
using System.Text.Json.Nodes;
using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

public class LooseItemsTests
{
	static Designate DigAt(DesignationKind kind, int x, int y, int z) => new(kind, z, x, y, x, y);

	// A fresh world's goblin stands at (16, 16, 0), so (16, 15, 0) is one step away.
	[Fact]
	public void A_fresh_world_holds_no_items()
	{
		var s = Simulation.NewWorld(seed: 1).State;

		for (var tile = 0; tile < s.TileKinds.Length; tile++)
		{
			Assert.Equal(0, s.ItemCount(tile, Material.Dirt));
			Assert.Equal(0, s.ItemCount(tile, Material.Stone));
		}
	}

	[Fact]
	public void Digging_stairs_into_dirt_leaves_one_loose_dirt_on_the_Stairs_tile()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		var target = s.TileIndex(16, 15, 0);
		sim.Apply(DigAt(DesignationKind.DigStairs, 16, 15, 0));

		RunTicks(sim, 40);

		Assert.Equal(TileKind.Stairs, s.KindAt(target));
		Assert.Equal(1, s.ItemCount(target, Material.Dirt));
		Assert.Equal(0, s.ItemCount(target, Material.Stone));
	}

	[Fact]
	public void Digging_a_dirt_tile_leaves_one_loose_dirt_and_nothing_elsewhere()
	{
		var sim = WorldWithShaftAt(16, 15);
		var s = sim.State;
		var target = s.TileIndex(17, 15, 1);
		sim.Apply(DigAt(DesignationKind.Dig, 17, 15, 1));

		RunTicks(sim, 60);

		Assert.Equal(TileKind.Floor, s.KindAt(target));
		Assert.Equal(1, s.ItemCount(target, Material.Dirt));
		Assert.Equal(0, s.ItemCount(target, Material.Stone));
		Assert.Equal(1, TotalItems(s));
	}

	[Fact]
	public void Digging_a_stone_tile_leaves_one_loose_stone()
	{
		var sim = WorldWithShaftAt(16, 15);
		var s = sim.State;
		var target = s.TileIndex(17, 15, 1);
		s.TileMaterials[target] = (byte)Material.Stone;
		sim.Apply(DigAt(DesignationKind.Dig, 17, 15, 1));

		RunTicks(sim, 80);

		Assert.Equal(TileKind.Floor, s.KindAt(target));
		Assert.Equal(1, s.ItemCount(target, Material.Stone));
		Assert.Equal(0, s.ItemCount(target, Material.Dirt));
	}

	[Fact]
	public void Nothing_drops_before_the_dig_finishes()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		sim.Apply(DigAt(DesignationKind.DigStairs, 16, 15, 0));

		RunTicks(sim, 10);

		Assert.Equal(0, TotalItems(sim.State));
	}

	[Fact]
	public void A_goblin_walks_over_a_tile_holding_a_loose_item()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		// Goblin at (16, 16); a loose item at (16, 15) lies on its way to the dig at (16, 13).
		var loose = s.TileIndex(16, 15, 0);
		s.AddItems(loose, Material.Dirt, 1);
		var seen = false;
		sim.Apply(DigAt(DesignationKind.DigStairs, 16, 13, 0));

		for (var i = 0; i < 40; i++)
		{
			RunTicks(sim, 1);
			seen |= s.GoblinX[0] == 16 && s.GoblinY[0] == 15 && s.GoblinZ[0] == 0;
		}

		Assert.True(seen, "The goblin never stepped onto the tile with the loose item.");
		Assert.Equal(TileKind.Stairs, s.KindAt(s.TileIndex(16, 13, 0)));
		Assert.Equal(1, s.ItemCount(loose, Material.Dirt));
	}

	[Fact]
	public void Items_survive_the_json_round_trip()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		sim.State.AddItems(sim.State.TileIndex(3, 4, 1), Material.Stone, 2);
		sim.State.AddItems(sim.State.TileIndex(3, 4, 1), Material.Dirt, 1);

		var restored = SimJson.Deserialize(SimJson.Serialize(sim));

		Assert.Equal(sim.State.TileItems, restored.State.TileItems);
		Assert.Equal(2, restored.State.ItemCount(restored.State.TileIndex(3, 4, 1), Material.Stone));
		Assert.Equal(1, restored.State.ItemCount(restored.State.TileIndex(3, 4, 1), Material.Dirt));
	}

	[Fact]
	public void A_save_with_a_negative_item_count_is_rejected()
	{
		var sim = Simulation.NewWorld(seed: 1);
		sim.State.TileItems[5] = -1;

		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Theory]
	[InlineData("null")]
	[InlineData("[]")]
	[InlineData("[0,0,0]")]
	public void A_save_with_a_missing_or_wrong_length_item_array_is_rejected(string value)
	{
		var node = JsonNode.Parse(SimJson.Serialize(Simulation.NewWorld(seed: 1)))!;
		node["TileItems"] = JsonNode.Parse(value);

		Assert.Throws<JsonException>(() => SimJson.Deserialize(node.ToJsonString()));
	}

	[Fact]
	public void An_item_array_one_entry_too_long_is_rejected()
	{
		var sim = Simulation.NewWorld(seed: 1);
		sim.State.TileItems = [.. sim.State.TileItems, 0];

		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Fact]
	public void Replaying_a_seed_and_command_log_gives_identical_counts()
	{
		var log = new (int Tick, Command Command)[]
		{
			(0, DigAt(DesignationKind.DigStairs, 16, 15, 0)),
			(3, new Designate(DesignationKind.DigStairs, 0, 20, 20, 22, 21)),
			(100, DigAt(DesignationKind.DigStairs, 16, 15, 1)),
		};

		var first = Replay(seed: 8, log, ticks: 400);
		var second = Replay(seed: 8, log, ticks: 400);

		Assert.Equal(first.State.TileItems, second.State.TileItems);
		Assert.True(TotalItems(first.State) >= 3);
		Assert.Equal(SimJson.Serialize(first), SimJson.Serialize(second));
	}

	static Simulation WorldWithShaftAt(int x, int y)
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.TileKinds[s.TileIndex(x, y, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[s.TileIndex(x, y, 1)] = (byte)TileKind.Stairs;
		(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]) = (x, y, 1);
		return sim;
	}

	static int TotalItems(SimState s) => s.TileItems.Sum();

	static Simulation Replay(int seed, (int Tick, Command Command)[] log, int ticks)
	{
		var sim = Simulation.NewWorld(seed);
		var next = 0;
		for (var tick = 0; tick < ticks; tick++)
		{
			while (next < log.Length && log[next].Tick == tick)
				sim.Apply(log[next++].Command);
			sim.Advance(0.1);
		}
		return sim;
	}
}
