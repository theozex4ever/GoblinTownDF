using Goblins.Sim;
using Xunit;
using static Goblins.Sim.Tests.SimTestHelpers;

namespace Goblins.Sim.Tests;

/// <summary>P3's seven Grunts, the starting Grub and Grog, and Grub, Fungus and Grog as Materials that haul and obey the Pile limit.</summary>
public class NewMaterialsTests
{
	public static TheoryData<Material> AllMaterials => new() { Material.Dirt, Material.Stone, Material.Grub, Material.Fungus, Material.Grog };
	public static TheoryData<Material> NewMaterials => new() { Material.Grub, Material.Fungus, Material.Grog };

	static GoblinTask TaskOf(SimState s, int g) => (GoblinTask)s.GoblinTask[g];

	static void Paint(Simulation sim, int x, int y) => sim.Apply(new PaintPile(0, x, y, x, y));

	// The lone goblin stands at (16, 16, 0). A unit at (16, 14) is two steps away, a Pile at (20, 14) four more.
	static Simulation OneGruntWithLooseUnit(Material material)
	{
		var sim = NewWorldWithOneGrunt(seed: 1);
		sim.State.AddItems(sim.State.TileIndex(16, 14, 0), material, 1);
		sim.RecheckReachability();
		return sim;
	}

	static int Count(SimState s, int x, int y, Material material) => s.ItemCount(s.TileIndex(x, y, 0), material);

	[Fact]
	public void A_new_warren_has_14_loose_Grub_and_7_loose_Grog_on_one_tile_beside_the_Grunts_and_no_Fungus()
	{
		var s = Simulation.NewWorld(seed: 7).State;

		var stocked = Enumerable.Range(0, s.TileItems.Length / SimState.CountedMaterials)
			.Where(tile => Enum.GetValues<Material>().Skip(1).Any(m => s.ItemCount(tile, m) > 0))
			.ToList();
		var tile = Assert.Single(stocked);
		Assert.Equal(14, s.LooseCount(tile, Material.Grub));
		Assert.Equal(7, s.LooseCount(tile, Material.Grog));
		Assert.Equal(0, s.ItemCount(tile, Material.Fungus));
		Assert.Equal(0, s.ItemCount(tile, Material.Dirt));
		Assert.Equal(0, s.ItemCount(tile, Material.Stone));
		Assert.Equal(0, s.Piles[tile]);
		Assert.Equal(TileKind.Floor, s.KindAt(tile));
		Assert.Equal(0, tile / (s.Width * s.Height));

		// Beside the cluster: one step from a Grunt, and not under one.
		var (x, y) = (tile % s.Width, tile / s.Width % s.Height);
		var distances = Enumerable.Range(0, s.GoblinCount).Select(g => Math.Abs(s.GoblinX[g] - x) + Math.Abs(s.GoblinY[g] - y)).ToList();
		Assert.Equal(1, distances.Min());
	}

	[Fact]
	public void The_same_seed_gives_the_same_warren_and_every_seed_gives_the_same_supplies()
	{
		Assert.Equal(SimJson.Serialize(Simulation.NewWorld(seed: 5)), SimJson.Serialize(Simulation.NewWorld(seed: 5)));
		Assert.Equal(Simulation.NewWorld(seed: 5).State.TileItems, Simulation.NewWorld(seed: 6).State.TileItems);
	}

	[Fact]
	public void Every_counted_Material_is_a_Material_but_None()
	{
		Assert.Equal(5, SimState.CountedMaterials);
		Assert.Equal(new[] { Material.Dirt, Material.Stone, Material.Grub, Material.Fungus, Material.Grog }, Enum.GetValues<Material>().Skip(1));
	}

	[Theory]
	[MemberData(nameof(NewMaterials))]
	public void A_loose_new_Material_is_carried_to_a_Pile_and_stored_there(Material material)
	{
		var sim = OneGruntWithLooseUnit(material);
		var s = sim.State;
		Paint(sim, 20, 14);

		RunTicks(sim, 40);

		Assert.Equal(0, Count(s, 16, 14, material));
		Assert.Equal(1, s.StoredCount(s.TileIndex(20, 14, 0), material));
		Assert.Equal(GoblinTask.Idle, TaskOf(s, 0));
		Assert.Equal((byte)Material.None, s.GoblinCarry[0]);
	}

	[Theory]
	[MemberData(nameof(AllMaterials))]
	public void A_unit_goes_to_the_nearest_Pile_with_room_for_its_material(Material material)
	{
		var sim = OneGruntWithLooseUnit(material);
		var s = sim.State;
		Paint(sim, 20, 14);
		Paint(sim, 25, 25);
		s.AddItems(s.TileIndex(20, 14, 0), material, 30);
		sim.RecheckReachability();

		RunTicks(sim, 120);

		Assert.Equal(30, Count(s, 20, 14, material));
		Assert.Equal(1, Count(s, 25, 25, material));
		Assert.Equal(0, Count(s, 16, 14, material));
	}

	[Theory]
	[MemberData(nameof(AllMaterials))]
	public void A_Pile_tile_takes_30_of_each_of_the_five_Materials_and_a_31st_of_any_one_goes_elsewhere(Material material)
	{
		var sim = OneGruntWithLooseUnit(material);
		var s = sim.State;
		Paint(sim, 20, 14);
		Paint(sim, 25, 25);
		var full = s.TileIndex(20, 14, 0);
		foreach (var other in Enum.GetValues<Material>().Skip(1))
			s.AddItems(full, other, other == material ? 29 : 30);
		sim.RecheckReachability();

		// The 30th unit fits on the nearer Pile, so the 31st has to go to the farther one.
		RunTicks(sim, 50);
		foreach (var other in Enum.GetValues<Material>().Skip(1))
			Assert.Equal(30, s.StoredCount(full, other));

		s.AddItems(s.TileIndex(16, 14, 0), material, 1);
		sim.RecheckReachability();
		RunTicks(sim, 120);

		foreach (var other in Enum.GetValues<Material>().Skip(1))
			Assert.Equal(30, s.StoredCount(full, other));
		Assert.Equal(1, s.StoredCount(s.TileIndex(25, 25, 0), material));
		Assert.Equal(0, Count(s, 16, 14, material));
	}

	[Theory]
	[MemberData(nameof(NewMaterials))]
	public void A_new_Material_with_no_Pile_room_waits_loose_and_never_blocks_another(Material material)
	{
		var sim = OneGruntWithLooseUnit(material);
		var s = sim.State;
		Paint(sim, 20, 14);
		s.AddItems(s.TileIndex(20, 14, 0), material, 30);
		s.AddItems(s.TileIndex(16, 14, 0), Material.Dirt, 1);
		sim.RecheckReachability();

		RunTicks(sim, 80);

		Assert.Equal(1, Count(s, 16, 14, material));
		Assert.False(s.IsHaulReachable(s.TileIndex(16, 14, 0), material));
		Assert.Equal(1, s.StoredCount(s.TileIndex(20, 14, 0), Material.Dirt));
	}

	[Fact]
	public void The_starting_Grub_and_Grog_are_all_hauled_to_a_painted_Pile()
	{
		var sim = Simulation.NewWorld(seed: 3);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 12, 12, 13, 13));

		RunTicks(sim, 800);

		var pile = new[] { (12, 12), (13, 12), (12, 13), (13, 13) }.Select(p => s.TileIndex(p.Item1, p.Item2, 0)).ToList();
		Assert.Equal(14, pile.Sum(t => s.StoredCount(t, Material.Grub)));
		Assert.Equal(7, pile.Sum(t => s.StoredCount(t, Material.Grog)));
		Assert.Equal(21, s.TileItems.Sum());
		Assert.All(Enumerable.Range(0, s.GoblinCount), g => Assert.Equal((byte)Material.None, s.GoblinCarry[g]));
	}

	[Fact]
	public void A_save_holding_the_new_Materials_mid_haul_round_trips_and_replays_identically()
	{
		var sim = Simulation.NewWorld(seed: 9);
		sim.Apply(new PaintPile(0, 10, 10, 11, 11));
		sim.State.AddItems(sim.State.TileIndex(20, 20, 0), Material.Fungus, 6);
		sim.RecheckReachability();
		RunTicks(sim, 30);
		Assert.Contains(Enumerable.Range(0, sim.State.GoblinCount), g => sim.State.GoblinCarry[g] != (byte)Material.None);

		var save = SimJson.Serialize(sim);
		var loaded = SimJson.Deserialize(save);
		Assert.Equal(save, SimJson.Serialize(loaded));

		RunTicks(sim, 300);
		RunTicks(loaded, 300);
		Assert.Equal(SimJson.Serialize(sim), SimJson.Serialize(loaded));
		Assert.Contains(Enum.GetValues<Material>().Skip(3), m => sim.State.TileItems.Where((_, i) => i % SimState.CountedMaterials == (int)m - 1).Sum() > 0);
	}

	[Fact]
	public void A_Pile_tile_over_30_of_a_new_Material_is_rejected_on_load()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var tile = sim.State.TileIndex(5, 5, 0);
		sim.State.Piles[tile] = 1;
		sim.State.AddItems(tile, Material.Fungus, 31);

		Assert.Throws<System.Text.Json.JsonException>(() => SimJson.Deserialize(SimJson.Serialize(sim)));
	}
}
