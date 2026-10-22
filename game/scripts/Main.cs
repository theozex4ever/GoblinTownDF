using System;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

public partial class Main : Node
{
	const string KeyHint = "Space naps the warren. 1, 2, 4 hurry it. , and . change level. D digs, S makes stairs; drag a box and the Grunt takes it personally.";

	Simulation _sim = null!;
	WarrenView _view = null!;
	Label _depthLabel = null!;
	SelectionGesture _selection = null!;
	int _viewZ;
	// The tool stays picked until Escape or its own key puts it away.
	DesignationKind _tool = DesignationKind.None;

	public override void _Ready()
	{
		var seed = (int)Time.GetUnixTimeFromSystem();
		_sim = Simulation.NewWorld(seed);
		GD.Print($"A Thousand Goblins Deep: boot ok (seed {seed})");

		_view = new WarrenView { Sim = _sim, Position = new Vector2(288, 36) };
		AddChild(_view);

		var s = _sim.State;
		_selection = new SelectionGesture(MouseButton.Left, new Vector2I(s.Width, s.Height), CellAt);
		_selection.Selected += OnSelected;
		_selection.Changed += () =>
		{
			_view.Selection = _selection.Dragging ? _selection.Rectangle : null;
			_view.QueueRedraw();
		};

		_depthLabel = new Label { Position = new Vector2(288, 8) };
		AddChild(_depthLabel);
		AddChild(new Label { Text = KeyHint, Position = new Vector2(288, 36 + 32 * WarrenView.CellSize + 6) });

		Refresh();
	}

	public override void _Process(double delta)
	{
		if (_sim.Advance(delta) > 0)
			_view.QueueRedraw();
		if (_sim.DrainEvents().Count > 0)
			UpdateDepthLabel();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_tool != DesignationKind.None && _selection.Handle(@event))
			GetViewport().SetInputAsHandled();
	}

	Vector2I CellAt(Vector2 position)
	{
		var cell = (position - _view.Position) / WarrenView.CellSize;
		return new Vector2I(Mathf.FloorToInt(cell.X), Mathf.FloorToInt(cell.Y));
	}

	// The sim clips the rectangle to the map, so a drag that ran off the edge still lands.
	void OnSelected(Vector2I from, Vector2I to)
	{
		_sim.Apply(new Designate(_tool, _viewZ, from.X, from.Y, to.X, to.Y));
		_view.QueueRedraw();
	}

	void PickTool(DesignationKind tool)
	{
		_selection.Cancel();
		_tool = _tool == tool ? DesignationKind.None : tool;
		UpdateDepthLabel();
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;

		switch (key.Keycode)
		{
			case Key.Space: _sim.Apply(new SetPaused(!_sim.State.Paused)); break;
			case Key.Key1 or Key.Kp1: _sim.Apply(new SetSpeed(1)); break;
			case Key.Key2 or Key.Kp2: _sim.Apply(new SetSpeed(2)); break;
			case Key.Key4 or Key.Kp4: _sim.Apply(new SetSpeed(4)); break;
			case Key.D: PickTool(DesignationKind.Dig); break;
			case Key.S: PickTool(DesignationKind.DigStairs); break;
			case Key.Escape: _selection.Cancel(); _tool = DesignationKind.None; UpdateDepthLabel(); break;
			case Key.Comma: ChangeLevel(-1); break;
			case Key.Period: ChangeLevel(1); break;
			default: return;
		}
		GetViewport().SetInputAsHandled();
	}

	void ChangeLevel(int dz)
	{
		// A drag belongs to the level it started on.
		_selection.Cancel();
		_viewZ = Math.Clamp(_viewZ + dz, 0, _sim.State.Depth - 1);
		Refresh();
	}

	void Refresh()
	{
		_view.ViewZ = _viewZ;
		_view.QueueRedraw();
		UpdateDepthLabel();
	}

	void UpdateDepthLabel()
	{
		var s = _sim.State;
		var pace = s.Paused ? "paused" : $"{s.Speed}×";
		var tool = _tool switch
		{
			DesignationKind.Dig => " · dig",
			DesignationKind.DigStairs => " · stairs",
			_ => "",
		};
		_depthLabel.Text = $"Depth {_viewZ} · {BandName(_viewZ)} · {pace}{tool}";
	}

	// The bands from the GDD's World and z-levels table.
	static string BandName(int z) => z switch
	{
		0 => "Surface",
		<= 3 => "Dirt",
		<= 9 => "Stone",
		<= 13 => "Ore",
		_ => "The Deep",
	};
}
