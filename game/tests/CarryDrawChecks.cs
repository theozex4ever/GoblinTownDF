using System;
using System.Linq;
using Godot;
using Goblins.Sim;
using Material = Goblins.Sim.Material;

namespace AThousandGoblinsDeep;

/// <summary>
/// Checks where carried-material squares, loose squares and Pile counts are drawn. The layout is pure geometry and always runs. The pixel checks
/// render the warren into an image and read pixels, so they need a real renderer: with --headless they print a skip
/// and pass, which means a run with no display does not check drawing.
/// </summary>
public partial class CarryDrawChecks : Node
{
	static readonly Color DirtSquare = new(0.55f, 0.36f, 0.18f);
	static readonly Color StoneSquare = new(0.36f, 0.40f, 0.48f);
	static readonly Material[] Materials = Enum.GetValues<Material>()[1..];
	static readonly Color GoblinBody = new(0.35f, 0.75f, 0.30f);

	public override async void _Ready()
	{
		try
		{
			LayoutChecks();
			ColourChecks();
			LooseLayoutChecks();
			PipLayoutCheck();

			if (DisplayServer.GetName() == "headless")
			{
				GD.Print("Carry draw checks: layout checks passed; pixel checks skipped, no renderer under --headless.");
				GetTree().Quit();
				return;
			}

			await CoveredByBodyCheck();
			await FullTileCheck();
			await FullCountCheck();
			await TwoCountsCheck();
			await LooseSquaresCheck();
			await NeedMarksCheck();
			GD.Print("Carry draw checks passed (layout, colours, count order, 6 pixel scenarios).");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	// For every number of carriers on one tile up to the goblin cap, each square lies inside the marker and no two overlap.
	static void LayoutChecks()
	{
		var marker = new Rect2(3, 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		for (var carriers = 1; carriers <= SimState.MaxGoblins; carriers++)
		{
			var rects = new Rect2[carriers];
			for (var slot = 0; slot < carriers; slot++)
			{
				rects[slot] = WarrenView.CarriedRect(marker, slot, carriers);
				Require(rects[slot].Size.X >= 1 && rects[slot].Size.Y >= 1, $"{carriers} carriers: slot {slot} is empty.");
				Require(marker.Encloses(rects[slot]), $"{carriers} carriers: slot {slot} {rects[slot]} leaves the marker {marker}.");
				for (var other = 0; other < slot; other++)
					Require(!rects[slot].Intersects(rects[other]), $"{carriers} carriers: slots {other} and {slot} overlap.");
			}
		}
	}

	// Goblin 0 carries dirt and goblin 2 stands on its tile, drawn after it in goblin order.
	async System.Threading.Tasks.Task CoveredByBodyCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		(s.GoblinX[2], s.GoblinY[2]) = (s.GoblinX[0], s.GoblinY[0]);
		GoblinTasks.FinishPickUp(s, 0, Goblins.Sim.Material.Dirt);
		var image = await Render(sim);
		var square = Square(s, 0, slot: 0, carriers: 1);
		ExpectColor(image, square.X, square.Y, DirtSquare, "A carrier's square was covered by a body on its tile.");
		ExpectColor(image, square.X, square.Y - 5, GoblinBody, "The goblin body was not drawn around the square.");
	}

	// Every goblin carries on one tile: each slot shows its goblin's material, inside the marker, and the tiles around stay clean.
	async System.Threading.Tasks.Task FullTileCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var (tileX, tileY) = (s.GoblinX[0], s.GoblinY[0]);
		for (var g = 0; g < s.GoblinCount; g++)
		{
			(s.GoblinX[g], s.GoblinY[g]) = (tileX, tileY);
			GoblinTasks.FinishPickUp(s, g, Materials[g % Materials.Length]);
		}
		var image = await Render(sim);

		for (var g = 0; g < s.GoblinCount; g++)
		{
			var square = Square(s, g, slot: g, carriers: s.GoblinCount);
			ExpectColor(image, square.X, square.Y, WarrenView.MaterialColor(Materials[g % Materials.Length]), $"Carrier {g}'s slot does not show its material.");
		}

		var cell = WarrenView.CellSize;
		for (var y = (tileY - 1) * cell; y < (tileY + 2) * cell; y++)
		for (var x = (tileX - 1) * cell; x < (tileX + 2) * cell; x++)
		{
			var inMarker = x >= tileX * cell + 3 && x < (tileX + 1) * cell - 3 && y >= tileY * cell + 3 && y < (tileY + 1) * cell - 3;
			if (inMarker)
				continue;
			var pixel = image.GetPixel(x, y);
			Require(!Materials.Any(m => IsNear(pixel, WarrenView.MaterialColor(m))), $"A carried colour was drawn at ({x}, {y}), outside the carrier's marker.");
		}
	}

	// A Pile count is highlighted at 30 and only for its own material. A full count comes first, so the chip is always
	// in the top slot: 30 dirt with 5 stone, and 5 dirt with 30 stone, where stone moves ahead of the smaller dirt.
	async System.Threading.Tasks.Task FullCountCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 4, 4, 5, 4));
		s.AddItems(s.TileIndex(4, 4, 0), Material.Dirt, SimState.PileCapacity);
		s.AddItems(s.TileIndex(4, 4, 0), Material.Stone, 5);
		s.AddItems(s.TileIndex(5, 4, 0), Material.Dirt, 5);
		s.AddItems(s.TileIndex(5, 4, 0), Material.Stone, SimState.PileCapacity);
		var image = await Render(sim);

		foreach (var tileX in new[] { 4, 5 })
		{
			Require(HasChip(image, tileX, 4, slot: 0), $"Tile {tileX}: the full count is not highlighted in the top slot.");
			Require(!HasChip(image, tileX, 4, slot: 1), $"Tile {tileX}: the count below the full one is highlighted.");
		}
	}

	// Five materials stored on one tile: only the two best are drawn, each in its own colour. Tile 4 has two full counts
	// ahead of a larger count that is not full; tile 5 has no full count, so the two largest show, in Material order on a tie.
	async System.Threading.Tasks.Task TwoCountsCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 4, 4, 5, 4));
		var full = s.TileIndex(4, 4, 0);
		(Material Material, int Count)[] fullTile = [(Material.Dirt, 29), (Material.Stone, 28), (Material.Grub, 30), (Material.Fungus, 1), (Material.Grog, 30)];
		foreach (var (material, count) in fullTile)
			s.AddItems(full, material, count);
		var plain = s.TileIndex(5, 4, 0);
		(Material Material, int Count)[] plainTile = [(Material.Dirt, 3), (Material.Stone, 20), (Material.Grub, 7), (Material.Fungus, 20), (Material.Grog, 2)];
		foreach (var (material, count) in plainTile)
			s.AddItems(plain, material, count);
		var image = await Render(sim);

		ExpectCount(image, 4, slot: 0, Material.Grub, chip: true);
		ExpectCount(image, 4, slot: 1, Material.Grog, chip: true);
		ExpectCount(image, 5, slot: 0, Material.Stone, chip: false);
		ExpectCount(image, 5, slot: 1, Material.Fungus, chip: false);
	}

	// Loose units of every material on one tile each get their own square, in their own colour. Each has a Pile with room
	// to go to, so none is greyed out as waiting.
	async System.Threading.Tasks.Task LooseSquaresCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(4, 4, 0);
		sim.Apply(new PaintPile(0, 10, 10, 10, 10));
		foreach (var material in Materials)
			s.AddItems(tile, material, 1);
		sim.RecheckReachability();
		var image = await Render(sim);

		var rect = new Rect2(4 * WarrenView.CellSize, 4 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize);
		foreach (var material in Materials)
		{
			var center = WarrenView.LooseSquare(rect, (int)material - 1).GetCenter();
			ExpectColor(image, (int)center.X, (int)center.Y, WarrenView.MaterialColor(material), $"The loose {material} square is missing or the wrong colour.");
		}
	}

	// A goblin craving Grub wears a Grub-coloured pip and one craving Grog a Grog-coloured one; with both, the lower meter's
	// colour shows (Grub on a tie). A Starving goblin also has a tinted body, a fed one neither, and Grog at 0 tints nothing.
	// The pip is drawn over a carried square without hiding it.
	async System.Threading.Tasks.Task NeedMarksCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		for (var g = 0; g < s.GoblinCount; g++)
			(s.GoblinGrub[g], s.GoblinGrog[g]) = (100, 100);
		s.GoblinGrub[0] = 29;
		s.GoblinGrub[3] = 0;
		s.GoblinGrog[1] = 29;
		(s.GoblinGrub[2], s.GoblinGrog[2]) = (25, 10);
		(s.GoblinGrub[5], s.GoblinGrog[5]) = (20, 20);
		s.GoblinGrog[6] = 0;
		GoblinTasks.FinishPickUp(s, 3, Material.Dirt);
		var image = await Render(sim);

		Color Body(int g) => image.GetPixel(s.GoblinX[g] * WarrenView.CellSize + 5, s.GoblinY[g] * WarrenView.CellSize + 13);
		Vector2I Pip(int g)
		{
			var marker = new Rect2(s.GoblinX[g] * WarrenView.CellSize + 3, s.GoblinY[g] * WarrenView.CellSize + 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
			return (Vector2I)WarrenView.PipRect(marker).GetCenter();
		}

		Require(IsNear(Body(0), GoblinBody), "A craving goblin's body should stay green.");
		ExpectColor(image, Pip(0).X, Pip(0).Y, WarrenView.MaterialColor(Material.Grub), "A goblin craving Grub has no Grub pip.");
		ExpectColor(image, Pip(1).X, Pip(1).Y, WarrenView.MaterialColor(Material.Grog), "A goblin craving Grog has no Grog pip.");
		Require(IsNear(Body(1), GoblinBody), "A goblin craving Grog should stay green.");
		ExpectColor(image, Pip(2).X, Pip(2).Y, WarrenView.MaterialColor(Material.Grog), "With the lower Grog meter, the pip should be Grog's.");
		ExpectColor(image, Pip(5).X, Pip(5).Y, WarrenView.MaterialColor(Material.Grub), "On a tie the pip should be Grub's.");
		Require(IsNear(Body(3), WarrenView.StarvingGoblinColor), "A Starving goblin's body is not tinted.");
		ExpectColor(image, Pip(3).X, Pip(3).Y, WarrenView.MaterialColor(Material.Grub), "A Starving goblin has no Grub pip.");
		var square = Square(s, 3, slot: 0, carriers: 1);
		ExpectColor(image, square.X, square.Y, DirtSquare, "The pip covered the Starving carrier's square.");
		Require(IsNear(Body(4), GoblinBody), "A fed goblin's body should be green.");
		ExpectColor(image, Pip(4).X, Pip(4).Y, GoblinBody, "A fed goblin has a pip.");
		Require(IsNear(Body(6), GoblinBody), "Grog at 0 should not tint the goblin.");
		ExpectColor(image, Pip(6).X, Pip(6).Y, WarrenView.MaterialColor(Material.Grog), "A goblin with Grog at 0 has no Grog pip.");
	}

	// The pip lies inside its tile, and clear of the carried square at the middle of the marker.
	static void PipLayoutCheck()
	{
		var tile = new Rect2(0, 0, WarrenView.CellSize, WarrenView.CellSize);
		var marker = new Rect2(3, 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		var pip = WarrenView.PipRect(marker);
		Require(tile.Encloses(pip), $"The pip {pip} leaves its tile.");
		Require(!pip.Intersects(WarrenView.CarriedRect(marker, 0, 1)), "The pip overlaps the carried square.");
	}

	// The first lit pixel of a count's chip, or its text colour, is read at the count's baseline.
	static bool HasChip(Image image, int tileX, int tileY, int slot)
	{
		var rect = new Rect2(tileX * WarrenView.CellSize, tileY * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize);
		var chip = WarrenView.CountChipRect(WarrenView.CountBaseline(rect, slot), 0);
		return IsNear(image.GetPixel((int)chip.Position.X, (int)chip.Position.Y), WarrenView.FullCountChipColor);
	}

	// A count in the right slot is drawn in its material's colour, or on the chip when full: some pixel of the slot's band has it.
	static void ExpectCount(Image image, int tileX, int slot, Material material, bool chip)
	{
		Require(HasChip(image, tileX, 4, slot) == chip, $"Tile {tileX}, slot {slot}: the chip is {(chip ? "missing" : "present")}.");
		if (chip)
			return;
		var rect = new Rect2(tileX * WarrenView.CellSize, 4 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize);
		var baseline = WarrenView.CountBaseline(rect, slot);
		var found = false;
		for (var y = (int)baseline.Y - (WarrenView.CountFontSize - 2); y <= (int)baseline.Y && !found; y++)
		for (var x = (int)baseline.X; x < (int)baseline.X + 8 && !found; x++)
			found = IsNear(image.GetPixel(x, y), WarrenView.MaterialColor(material));
		Require(found, $"Tile {tileX}, slot {slot}: no {material}-coloured count was drawn.");
	}

	// Every material has a colour of its own, so a loose square or a carried square says what it is.
	static void ColourChecks()
	{
		foreach (var a in Materials)
		foreach (var b in Materials)
		{
			if (a >= b)
				continue;
			var (ca, cb) = (WarrenView.MaterialColor(a), WarrenView.MaterialColor(b));
			Require(Math.Abs(ca.R - cb.R) + Math.Abs(ca.G - cb.G) + Math.Abs(ca.B - cb.B) > 0.3f, $"{a} and {b} have colours too close to tell apart.");
		}
	}

	// The loose squares of every material fit one tile without overlap.
	static void LooseLayoutChecks()
	{
		var rect = new Rect2(0, 0, WarrenView.CellSize, WarrenView.CellSize);
		for (var i = 0; i < Materials.Length; i++)
		{
			var square = WarrenView.LooseSquare(rect, i);
			Require(rect.Encloses(square), $"Loose square {i} leaves the tile.");
			for (var j = 0; j < i; j++)
				Require(!square.Intersects(WarrenView.LooseSquare(rect, j)), $"Loose squares {i} and {j} overlap.");
		}
	}

	static (int X, int Y) Square(SimState s, int goblin, int slot, int carriers)
	{
		var marker = new Rect2(s.GoblinX[goblin] * WarrenView.CellSize + 3, s.GoblinY[goblin] * WarrenView.CellSize + 3,
			WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		var center = WarrenView.CarriedRect(marker, slot, carriers).GetCenter();
		return ((int)center.X, (int)center.Y);
	}

	async System.Threading.Tasks.Task<Image> Render(Simulation sim)
	{
		var size = new Vector2I(sim.State.Width * WarrenView.CellSize, sim.State.Height * WarrenView.CellSize);
		var viewport = new SubViewport { Size = size, RenderTargetUpdateMode = SubViewport.UpdateMode.Once };
		viewport.AddChild(new WarrenView { Sim = sim });
		AddChild(viewport);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		var image = viewport.GetTexture().GetImage();
		RemoveChild(viewport);
		viewport.Free();
		return image;
	}

	static bool IsNear(Color a, Color b) => Math.Abs(a.R - b.R) < 0.03f && Math.Abs(a.G - b.G) < 0.03f && Math.Abs(a.B - b.B) < 0.03f;

	static void ExpectColor(Image image, int x, int y, Color expected, string message)
	{
		var actual = image.GetPixel(x, y);
		Require(IsNear(actual, expected), $"{message} Pixel ({x}, {y}) was {actual}, expected {expected}.");
	}

	static void Require(bool condition, string message)
	{
		if (!condition)
			throw new Exception(message);
	}
}
