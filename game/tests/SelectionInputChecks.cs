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

			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				Key(main, Godot.Key.P);
				Drag(main, view, MouseButton.Left, 10, 10, 12, 11);
				ExpectPiles(s, 0, true, 10, 10, 12, 11);
				// Stairs are skipped; the tile in the box that is not Floor stays unflagged.
				s.TileKinds[s.TileIndex(20, 20, 0)] = (byte)TileKind.Stairs;
				Drag(main, view, MouseButton.Left, 20, 20, 21, 20);
				Require(!s.IsPile(s.TileIndex(20, 20, 0)) && s.IsPile(s.TileIndex(21, 20, 0)), "Pile paint did not skip Stairs.");

				// Right-drag with the Pile Tool removes Piles and leaves Designations alone.
				sim.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 12, 12, 12));
				s.AddItems(s.TileIndex(10, 10, 0), Goblins.Sim.Material.Stone, 2);
				Drag(main, view, MouseButton.Right, 10, 10, 12, 12);
				ExpectPiles(s, 0, false, 10, 10, 12, 11);
				ExpectRectangle(s, 0, DesignationKind.DigStairs, 10, 12, 12, 12);
				Require(s.LooseCount(s.TileIndex(10, 10, 0), Goblins.Sim.Material.Stone) == 2, "Removed Pile lost its stock.");

				// Right-drag with Dig stairs cancels Designations and leaves Piles alone.
				Drag(main, view, MouseButton.Left, 10, 10, 12, 12);
				Key(main, Godot.Key.P);
				Key(main, Godot.Key.S);
				Drag(main, view, MouseButton.Right, 10, 10, 12, 12);
				ExpectRectangle(s, 0, DesignationKind.None, 10, 12, 12, 12);
				ExpectPiles(s, 0, true, 10, 10, 12, 11);
			});

			foreach (var button in new[] { MouseButton.Left, MouseButton.Right })
			foreach (var key in new[] { Godot.Key.D, Godot.Key.S, Godot.Key.P, Godot.Key.Escape, Godot.Key.Period, Godot.Key.Comma })
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

			CountLabelChecks();

			GD.Print("Selection input checks passed (16 scenarios, count labels).");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	// A Pile tile holds at most 30 of a material, but a label must still read inside its 18 px cell, with the 2 px left inset, for any count.
	static void CountLabelChecks()
	{
		var expected = new (int Count, string Label)[]
		{
			(0, ""), (-1, ""), (1, "1"), (9, "9"), (10, "10"), (99, "99"), (100, "100"), (999, "999"),
			(1000, "1k"), (1999, "1k"), (2000, "2k"), (9999, "9k"), (10_000, "10k"), (99_999, "99k"),
			(100_000, "99k"), (1_000_000, "99k"), (int.MaxValue, "99k"),
		};
		var font = ThemeDB.FallbackFont;
		foreach (var (count, label) in expected)
		{
			var actual = CountLabel.Format(count);
			Require(actual == label, $"CountLabel.Format({count}) was \"{actual}\", expected \"{label}\".");
			var width = font.GetStringSize(actual, HorizontalAlignment.Left, -1, WarrenView.CountFontSize).X;
			Require(width + 2 <= WarrenView.CellSize, $"Count {count} (\"{actual}\") is {width} px wide and overflows its cell.");
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

	static void ExpectPiles(SimState state, int z, bool piled, int x0, int y0, int x1, int y1)
	{
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
			Require(state.IsPile(state.TileIndex(x, y, z)) == piled, $"Expected Pile {(piled ? "set" : "clear")} at ({x}, {y}, {z}).");
	}

	static void Require(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}
}
