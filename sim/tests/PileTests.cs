using System.Text.Json;
using System.Text.Json.Nodes;
using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

public class PileTests
{
	static PaintPile PaintAt(int x, int y, int z) => new(z, x, y, x, y);

	static RemovePile RemoveAt(int x, int y, int z) => new(z, x, y, x, y);

	static int CountPiles(SimState s) => s.Piles.Count(p => p != 0);

	[Fact]
	public void A_fresh_world_has_no_Piles()
	{
		Assert.Equal(0, CountPiles(Simulation.NewWorld(seed: 1).State));
	}

	[Fact]
	public void Painting_a_rectangle_flags_exactly_its_Floor_tiles_as_Pile()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;

		sim.Apply(new PaintPile(0, 4, 5, 7, 6));

		Assert.Equal(4 * 2, CountPiles(s));
		for (var y = 5; y <= 6; y++)
		for (var x = 4; x <= 7; x++)
			Assert.True(s.IsPile(s.TileIndex(x, y, 0)), $"({x}, {y}) is not a Pile.");
	}

	[Fact]
	public void Painting_corners_in_any_order_and_a_single_click_both_work()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;

		sim.Apply(new PaintPile(0, 7, 6, 4, 5));
		Assert.Equal(8, CountPiles(s));

		sim.Apply(PaintAt(10, 10, 0));
		Assert.Equal(9, CountPiles(s));
		Assert.True(s.IsPile(s.TileIndex(10, 10, 0)));
	}

	[Fact]
	public void Painting_skips_Solid_Stairs_and_Open_air_tiles()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var floor = s.TileIndex(5, 5, 0);
		var stairs = s.TileIndex(6, 5, 0);
		var air = s.TileIndex(7, 5, 0);
		var solid = s.TileIndex(8, 5, 0);
		s.TileKinds[stairs] = (byte)TileKind.Stairs;
		s.TileKinds[air] = (byte)TileKind.OpenAir;
		s.TileKinds[solid] = (byte)TileKind.Solid;

		sim.Apply(new PaintPile(0, 5, 5, 8, 5));

		Assert.True(s.IsPile(floor));
		Assert.False(s.IsPile(stairs));
		Assert.False(s.IsPile(air));
		Assert.False(s.IsPile(solid));
		Assert.Equal(1, CountPiles(s));
	}

	[Fact]
	public void Painting_a_solid_level_flags_nothing()
	{
		var sim = Simulation.NewWorld(seed: 1);

		sim.Apply(new PaintPile(1, 0, 0, 31, 31));

		Assert.Equal(0, CountPiles(sim.State));
	}

	[Fact]
	public void A_rectangle_running_off_the_map_is_clipped_and_one_wholly_off_the_map_paints_nothing()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;

		sim.Apply(new PaintPile(0, 30, 30, 40, 40));
		Assert.Equal(4, CountPiles(s));

		sim.Apply(new PaintPile(0, 40, 40, 50, 50));
		sim.Apply(new PaintPile(0, -5, -5, -1, -1));
		Assert.Equal(4, CountPiles(s));
	}

	[Fact]
	public void Painting_on_a_level_off_the_map_throws()
	{
		var sim = Simulation.NewWorld(seed: 1);

		Assert.Throws<ArgumentOutOfRangeException>(() => sim.Apply(PaintAt(1, 1, 3)));
		Assert.Throws<ArgumentOutOfRangeException>(() => sim.Apply(RemoveAt(1, 1, -1)));
	}

	[Fact]
	public void Painting_over_a_loose_unit_stores_it_on_the_spot()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(5, 5, 0);
		s.AddItems(tile, Material.Dirt, 2);
		s.AddItems(tile, Material.Stone, 1);
		Assert.Equal(2, s.LooseCount(tile, Material.Dirt));
		Assert.Equal(0, s.StoredCount(tile, Material.Dirt));

		sim.Apply(PaintAt(5, 5, 0));

		Assert.Equal(0, s.LooseCount(tile, Material.Dirt));
		Assert.Equal(0, s.LooseCount(tile, Material.Stone));
		Assert.Equal(2, s.StoredCount(tile, Material.Dirt));
		Assert.Equal(1, s.StoredCount(tile, Material.Stone));
		Assert.Equal(2, s.ItemCount(tile, Material.Dirt));
	}

	[Fact]
	public void Removing_Pile_tiles_keeps_their_counts_which_become_loose()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(5, 5, 0);
		sim.Apply(PaintAt(5, 5, 0));
		s.AddItems(tile, Material.Dirt, 3);
		s.AddItems(tile, Material.Stone, 4);

		sim.Apply(RemoveAt(5, 5, 0));

		Assert.False(s.IsPile(tile));
		Assert.Equal(3, s.ItemCount(tile, Material.Dirt));
		Assert.Equal(4, s.ItemCount(tile, Material.Stone));
		Assert.Equal(3, s.LooseCount(tile, Material.Dirt));
		Assert.Equal(4, s.LooseCount(tile, Material.Stone));
		Assert.Equal(0, s.StoredCount(tile, Material.Dirt));
	}

	[Fact]
	public void Removing_takes_only_Pile_tiles_in_the_rectangle_and_skips_the_rest()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 4, 4, 5, 5));
		sim.Apply(PaintAt(10, 10, 0));

		sim.Apply(new RemovePile(0, 5, 5, 4, 4));

		Assert.Equal(1, CountPiles(s));
		Assert.True(s.IsPile(s.TileIndex(10, 10, 0)));

		// Removing from tiles with no Pile is harmless.
		sim.Apply(new RemovePile(0, 0, 0, 31, 3));
		Assert.Equal(1, CountPiles(s));
	}

	[Fact]
	public void Removing_Piles_leaves_Designations_alone_and_cancelling_leaves_Piles_alone()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var piled = s.TileIndex(20, 20, 0);
		var designated = s.TileIndex(21, 20, 0);
		sim.Apply(PaintAt(20, 20, 0));
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 21, 20, 21, 20));

		sim.Apply(new CancelDesignation(0, 20, 20, 21, 20));
		Assert.True(s.IsPile(piled));
		Assert.Equal(DesignationKind.None, s.DesignationAt(designated));

		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 21, 20, 21, 20));
		sim.Apply(new RemovePile(0, 20, 20, 21, 20));
		Assert.False(s.IsPile(piled));
		Assert.Equal(DesignationKind.DigStairs, s.DesignationAt(designated));
	}

	[Fact]
	public void A_Pile_tile_dug_to_Stairs_clears_the_Pile_and_its_stock_becomes_loose()
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(16, 15, 0);
		sim.Apply(PaintAt(16, 15, 0));
		s.AddItems(tile, Material.Stone, 2);
		sim.Apply(new Designate(DesignationKind.DigStairs, 0, 16, 15, 16, 15));

		RunTicks(sim, 40);

		Assert.Equal(TileKind.Stairs, s.KindAt(tile));
		Assert.False(s.IsPile(tile));
		Assert.Equal(2, s.LooseCount(tile, Material.Stone));
		Assert.Equal(1, s.LooseCount(tile, Material.Dirt));
		// The state stays a valid save: no Pile flag is left on the Stairs.
		SimJson.Deserialize(SimJson.Serialize(sim));
	}

	[Fact]
	public void Piles_survive_the_json_round_trip()
	{
		var sim = Simulation.NewWorld(seed: 1);
		sim.Apply(new PaintPile(0, 3, 3, 6, 4));
		sim.State.AddItems(sim.State.TileIndex(3, 3, 0), Material.Stone, 2);

		var restored = SimJson.Deserialize(SimJson.Serialize(sim));

		Assert.Equal(sim.State.Piles, restored.State.Piles);
		Assert.Equal(8, CountPiles(restored.State));
		Assert.Equal(2, restored.State.StoredCount(restored.State.TileIndex(3, 3, 0), Material.Stone));
	}

	[Theory]
	[InlineData(TileKind.Solid)]
	[InlineData(TileKind.Stairs)]
	[InlineData(TileKind.OpenAir)]
	public void A_save_with_a_Pile_flag_on_a_non_Floor_tile_is_rejected(TileKind kind)
	{
		var sim = Simulation.NewWorld(seed: 1);
		var tile = sim.State.TileIndex(5, 5, 0);
		sim.State.Piles[tile] = 1;
		sim.State.TileKinds[tile] = (byte)kind;

		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Fact]
	public void A_save_with_a_Pile_flag_other_than_0_or_1_is_rejected()
	{
		var sim = Simulation.NewWorld(seed: 1);
		sim.State.Piles[sim.State.TileIndex(5, 5, 0)] = 2;

		Assert.Throws<JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}

	[Theory]
	[InlineData("null")]
	[InlineData("[]")]
	[InlineData("[0,0,0]")]
	public void A_save_with_a_missing_or_wrong_length_Pile_array_is_rejected(string value)
	{
		var node = JsonNode.Parse(SimJson.Serialize(Simulation.NewWorld(seed: 1)))!;
		node["Piles"] = JsonNode.Parse(value);

		Assert.Throws<JsonException>(() => SimJson.Deserialize(node.ToJsonString()));
	}

	[Fact]
	public void Replaying_a_seed_and_command_log_with_paint_and_remove_gives_identical_state()
	{
		var log = new (int Tick, Command Command)[]
		{
			(0, new Designate(DesignationKind.DigStairs, 0, 20, 20, 22, 21)),
			(2, new PaintPile(0, 14, 14, 18, 18)),
			(30, new RemovePile(0, 16, 14, 18, 15)),
			(31, new Designate(DesignationKind.DigStairs, 0, 15, 15, 15, 15)),
			(200, new PaintPile(0, 20, 20, 22, 22)),
			(300, new RemovePile(0, 0, 0, 31, 31)),
		};

		var first = Replay(seed: 12, log, ticks: 400);
		var second = Replay(seed: 12, log, ticks: 400);

		Assert.Equal(SimJson.Serialize(first), SimJson.Serialize(second));
		Assert.Equal(0, CountPiles(first.State));
		Assert.True(first.State.TileItems.Sum() >= 4);
	}

	[Fact]
	public void A_run_saved_while_Piles_and_stock_exist_replays_the_rest_of_the_log_identically()
	{
		var log = new (int Tick, Command Command)[]
		{
			(2, new PaintPile(0, 14, 14, 18, 18)),
			(31, new Designate(DesignationKind.DigStairs, 0, 15, 15, 15, 15)),
			(250, new RemovePile(0, 17, 17, 18, 18)),
			(300, new PaintPile(0, 0, 0, 31, 31)),
			(350, new RemovePile(0, 0, 0, 31, 31)),
		};
		var original = Simulation.NewWorld(seed: 12);
		var stocked = original.State.TileIndex(17, 17, 0);
		original.State.AddItems(stocked, Material.Stone, 2);
		original.State.AddItems(original.State.TileIndex(18, 18, 0), Material.Dirt, 3);
		var next = RunLog(original, log, from: 0, to: 200);

		// The checkpoint holds Piles with stored stock, and the original is still mid-run.
		Assert.True(CountPiles(original.State) > 0);
		Assert.Equal(2, original.State.StoredCount(stocked, Material.Stone));
		var restored = SimJson.Deserialize(SimJson.Serialize(original));
		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));

		RunLog(original, log, from: 200, to: 400, next);
		RunLog(restored, log, from: 200, to: 400, next);

		Assert.Equal(SimJson.Serialize(original), SimJson.Serialize(restored));
		// The last remove turned all stock loose again, and nothing was lost on the way.
		Assert.Equal(0, CountPiles(original.State));
		Assert.Equal(2, original.State.LooseCount(stocked, Material.Stone));
	}

	static Simulation Replay(int seed, (int Tick, Command Command)[] log, int ticks)
	{
		var sim = Simulation.NewWorld(seed);
		RunLog(sim, log, from: 0, to: ticks);
		return sim;
	}

	// Runs ticks [from, to), applying each logged Command at its tick. Returns the index of the next unapplied entry.
	static int RunLog(Simulation sim, (int Tick, Command Command)[] log, int from, int to, int next = 0)
	{
		for (var tick = from; tick < to; tick++)
		{
			while (next < log.Length && log[next].Tick == tick)
				sim.Apply(log[next++].Command);
			sim.Advance(0.1);
		}
		return next;
	}
}
