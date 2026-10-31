using System;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>
/// Renders the warren into an image and reads pixels, so a carried-material square is checked where it is drawn,
/// under any goblin body that shares its tile. Needs a real renderer: with --headless it prints a skip and passes.
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
			if (DisplayServer.GetName() == "headless")
			{
				GD.Print("Carry draw checks skipped: no renderer under --headless.");
				GetTree().Quit();
				return;
			}

			// Goblin 0 carries dirt and goblin 2 stands on its tile, drawn after it in goblin order.
			var sim = Simulation.NewWorld(seed: 1);
			var s = sim.State;
			(s.GoblinX[2], s.GoblinY[2]) = (s.GoblinX[0], s.GoblinY[0]);
			GoblinTasks.FinishPickUp(s, 0, Goblins.Sim.Material.Dirt);
			var image = await Render(sim);
			var (cx, cy) = Center(s, 0);
			ExpectColor(image, cx, cy, DirtSquare, "A carrier's square was covered by a body on its tile.");
			ExpectColor(image, cx, cy - 5, GoblinBody, "The goblin body was not drawn around the square.");

			// A second carrier on the tile takes the next slot, so both materials show.
			GoblinTasks.FinishPickUp(s, 2, Goblins.Sim.Material.Stone);
			image = await Render(sim);
			ExpectColor(image, cx, cy, DirtSquare, "The first carrier's square was lost to a second carrier.");
			ExpectColor(image, cx + WarrenView.CarriedSize, cy, StoneSquare, "The second carrier's square is not beside the first.");

			GD.Print("Carry draw checks passed (2 scenarios).");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	static (int X, int Y) Center(SimState s, int goblin) =>
		(s.GoblinX[goblin] * WarrenView.CellSize + WarrenView.CellSize / 2, s.GoblinY[goblin] * WarrenView.CellSize + WarrenView.CellSize / 2);

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

	static void ExpectColor(Image image, int x, int y, Color expected, string message)
	{
		var actual = image.GetPixel(x, y);
		var near = Math.Abs(actual.R - expected.R) < 0.03f && Math.Abs(actual.G - expected.G) < 0.03f && Math.Abs(actual.B - expected.B) < 0.03f;
		if (!near)
			throw new Exception($"{message} Pixel ({x}, {y}) was {actual}, expected {expected}.");
	}
}
