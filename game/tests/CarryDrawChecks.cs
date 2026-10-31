using System;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>
/// Checks where carried-material squares are drawn. The layout is pure geometry and always runs. The pixel checks
/// render the warren into an image and read pixels, so they need a real renderer: with --headless they print a skip
/// and pass, which means a run with no display does not check drawing.
/// </summary>
public partial class CarryDrawChecks : Node
{
	static readonly Color DirtSquare = new(0.55f, 0.36f, 0.18f);
	static readonly Color StoneSquare = new(0.36f, 0.40f, 0.48f);
	static readonly Color GoblinBody = new(0.35f, 0.75f, 0.30f);

	public override async void _Ready()
	{
		try
		{
			LayoutChecks();

			if (DisplayServer.GetName() == "headless")
			{
				GD.Print("Carry draw checks: layout checks passed; pixel checks skipped, no renderer under --headless.");
				GetTree().Quit();
				return;
			}

			await CoveredByBodyCheck();
			await FullTileCheck();
			GD.Print("Carry draw checks passed (layout, 2 pixel scenarios).");
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
			GoblinTasks.FinishPickUp(s, g, g % 2 == 0 ? Goblins.Sim.Material.Dirt : Goblins.Sim.Material.Stone);
		}
		var image = await Render(sim);

		for (var g = 0; g < s.GoblinCount; g++)
		{
			var square = Square(s, g, slot: g, carriers: s.GoblinCount);
			ExpectColor(image, square.X, square.Y, g % 2 == 0 ? DirtSquare : StoneSquare, $"Carrier {g}'s slot does not show its material.");
		}

		var cell = WarrenView.CellSize;
		for (var y = (tileY - 1) * cell; y < (tileY + 2) * cell; y++)
		for (var x = (tileX - 1) * cell; x < (tileX + 2) * cell; x++)
		{
			var inMarker = x >= tileX * cell + 3 && x < (tileX + 1) * cell - 3 && y >= tileY * cell + 3 && y < (tileY + 1) * cell - 3;
			if (inMarker)
				continue;
			var pixel = image.GetPixel(x, y);
			Require(!IsNear(pixel, DirtSquare) && !IsNear(pixel, StoneSquare), $"A carried colour was drawn at ({x}, {y}), outside the carrier's marker.");
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
