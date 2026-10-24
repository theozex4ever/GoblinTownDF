using System;
using System.Linq;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>Headless input checks through Main's engine callbacks and the rendered sim state.</summary>
public partial class SelectionInputChecks : Node
{
	public override void _Ready()
	{
		try
		{
			WithMain((main, view, sim) =>
			{
				sim.Apply(new Designate(DesignationKind.DigStairs, 0, 4, 4, 7, 7));
				sim.Apply(new Designate(DesignationKind.DigStairs, 0, 9, 9, 9, 9));
				// Right-drag works before any tool has been selected, with reversed corners.
				Drag(main, view, MouseButton.Right, 7, 7, 4, 4);
				ExpectRectangle(sim.State, 0, DesignationKind.None, 4, 4, 7, 7);
				ExpectRectangle(sim.State, 0, DesignationKind.DigStairs, 9, 9, 9, 9);
				Drag(main, view, MouseButton.Right, 9, 9, 9, 9);
				ExpectRectangle(sim.State, 0, DesignationKind.None, 9, 9, 9, 9);
				Require(view.Selection is null, "Completed cancellation kept its preview.");
			});

			WithMain((main, view, sim) =>
			{
				Drag(main, view, MouseButton.Left, 4, 4, 5, 5);
				ExpectRectangle(sim.State, 0, DesignationKind.None, 4, 4, 5, 5);
				Key(main, Godot.Key.S);
				Drag(main, view, MouseButton.Left, 4, 4, 5, 5);
				ExpectRectangle(sim.State, 0, DesignationKind.DigStairs, 4, 4, 5, 5);
				Key(main, Godot.Key.Period);
				Key(main, Godot.Key.D);
				Drag(main, view, MouseButton.Left, 4, 4, 5, 5);
				ExpectRectangle(sim.State, 1, DesignationKind.Dig, 4, 4, 5, 5);
				Drag(main, view, MouseButton.Right, 4, 4, 5, 5);
				ExpectRectangle(sim.State, 1, DesignationKind.None, 4, 4, 5, 5);
				ExpectRectangle(sim.State, 0, DesignationKind.DigStairs, 4, 4, 5, 5);
			});

			WithMain((main, view, sim) =>
			{
				sim.Apply(new Designate(DesignationKind.DigStairs, 0, 4, 4, 7, 7));
				// The release position counts even if no motion event preceded it.
				Button(main, view, MouseButton.Right, true, 4, 4);
				Button(main, view, MouseButton.Right, false, 7, 7);
				ExpectRectangle(sim.State, 0, DesignationKind.None, 4, 4, 7, 7);
			});

			foreach (var button in new[] { MouseButton.Left, MouseButton.Right })
			foreach (var key in new[] { Godot.Key.D, Godot.Key.S, Godot.Key.Escape, Godot.Key.Period, Godot.Key.Comma })
				WithMain((main, view, sim) =>
				{
					Key(main, Godot.Key.S);
					sim.Apply(new Designate(DesignationKind.DigStairs, 0, 4, 4, 7, 7));
					var before = SimJson.Serialize(sim);
					Button(main, view, button, true, 4, 4);
					Move(main, view, 7, 7);
					Require(view.Selection is not null, "Drag did not show its preview.");
					Key(main, key);
					Require(view.Selection is null, $"{key} kept a stale drag preview.");
					Button(main, view, button, false, 7, 7);
					Require(SimJson.Serialize(sim) == before, $"{key} allowed a cancelled {button} drag to submit.");
				});

			GD.Print("Selection input checks passed (13 scenarios).");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	void WithMain(Action<Main, WarrenView, Simulation> check)
	{
		var main = new Main();
		AddChild(main);
		try
		{
			var view = main.GetChildren().OfType<WarrenView>().Single();
			var sim = view.Sim!;
			sim.Apply(new SetPaused(true));
			check(main, view, sim);
		}
		finally
		{
			RemoveChild(main);
			main.Free();
		}
	}

	static Vector2 Position(WarrenView view, int x, int y) =>
		view.Position + new Vector2((x + 0.5f) * WarrenView.CellSize, (y + 0.5f) * WarrenView.CellSize);

	static void Button(Main main, WarrenView view, MouseButton button, bool pressed, int x, int y)
	{
		using var input = new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = Position(view, x, y) };
		main._UnhandledInput(input);
	}

	static void Move(Main main, WarrenView view, int x, int y)
	{
		using var input = new InputEventMouseMotion { Position = Position(view, x, y) };
		main._UnhandledInput(input);
	}

	static void Drag(Main main, WarrenView view, MouseButton button, int x0, int y0, int x1, int y1)
	{
		Button(main, view, button, true, x0, y0);
		Move(main, view, x1, y1);
		Button(main, view, button, false, x1, y1);
	}

	static void Key(Main main, Godot.Key key)
	{
		using var input = new InputEventKey { Keycode = key, Pressed = true };
		main._UnhandledKeyInput(input);
	}

	static void ExpectRectangle(SimState state, int z, DesignationKind kind, int x0, int y0, int x1, int y1)
	{
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
			Require(state.DesignationAt(state.TileIndex(x, y, z)) == kind, $"Expected {kind} at ({x}, {y}, {z}).");
	}

	static void Require(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}
}
