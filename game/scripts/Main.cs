using System;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

public partial class Main : Node
{
	enum Tool
	{
		None,
		Dig,
		DigStairs,
		Pile,
	}

	const string KeyHint = "Space naps the warren. 1, 2, 4 hurry it. , and . change level. D digs, S makes stairs, P heaps things in a pile; drag a box. Right-drag unsays it. The Grunt takes it personally.";

	Simulation _sim = null!;
	WarrenView _view = null!;
	Label _depthLabel = null!;
	Label _hoverLabel = null!;
	// The map cell under the cursor, from the last mouse motion; kept so the hover line follows the counts as the sim changes them.
	Vector2I _hoverCell = new(-1, -1);
	SelectionGesture _selection = null!;
	SelectionGesture _removal = null!;
	int _viewZ;
	// The tool stays picked until Escape or its own key puts it away.
	Tool _tool = Tool.None;

	public override void _Ready()
	{
		var seed = (int)Time.GetUnixTimeFromSystem();
		_sim = Simulation.NewWorld(seed);
		GD.Print($"A Thousand Goblins Deep: boot ok (seed {seed})");

		_view = new WarrenView { Sim = _sim, Position = new Vector2(288, 36) };
		AddChild(_view);

		var s = _sim.State;
		_selection = new SelectionGesture(MouseButton.Left, new Vector2I(s.Width, s.Height), CellAt);
		_removal = new SelectionGesture(MouseButton.Right, new Vector2I(s.Width, s.Height), CellAt);
		_selection.Selected += OnSelected;
		_removal.Selected += OnRemoved;
		_selection.Changed += UpdateSelection;
		_removal.Changed += UpdateSelection;

		_depthLabel = new Label { Position = new Vector2(288, 8) };
		AddChild(_depthLabel);
		_hoverLabel = new Label { Position = new Vector2(288, 36 + s.Height * WarrenView.CellSize + 4) };
		AddChild(_hoverLabel);
		// The hint wraps in the margin left of the map: unwrapped it is about 1070 px wide, and two lines
		// under the map would run past the bottom of the 648 px window.
		AddChild(new Label
		{
			Text = KeyHint,
			Position = new Vector2(8, 36),
			CustomMinimumSize = new Vector2(272, 0),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		});

		Refresh();
	}

	public override void _Process(double delta)
	{
		if (_sim.Advance(delta) > 0)
		{
			_view.QueueRedraw();
			UpdateHover();
		}
		if (_sim.DrainEvents().Count > 0)
			UpdateDepthLabel();
	}

	/// <summary>The hover line: the Grub and Grog meters of the goblin under the cursor, else every count on the Pile tile under it, else empty.</summary>
	public string HoverText => _hoverLabel.Text;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
		{
			_hoverCell = CellAt(motion.Position);
			UpdateHover();
		}

		// Only one button owns a drag; removal is available with no tool picked.
		if (_selection.Dragging)
		{
			if (_selection.Handle(@event))
				GetViewport().SetInputAsHandled();
		}
		else if (_removal.Dragging)
		{
			if (_removal.Handle(@event))
				GetViewport().SetInputAsHandled();
		}
		else if (_removal.Handle(@event) || (_tool != Tool.None && _selection.Handle(@event)))
			GetViewport().SetInputAsHandled();
	}

	void UpdateHover()
	{
		var s = _sim.State;
		var inMap = _hoverCell.X >= 0 && _hoverCell.Y >= 0 && _hoverCell.X < s.Width && _hoverCell.Y < s.Height;
		if (!inMap)
		{
			_hoverLabel.Text = "";
			return;
		}
		var tile = s.TileIndex(_hoverCell.X, _hoverCell.Y, _viewZ);
		var goblin = GoblinHover.GoblinAt(s, tile);
		_hoverLabel.Text = goblin >= 0 ? GoblinHover.HoverLine(s, goblin) : PileCounts.HoverLine(s, tile);
	}

	void UpdateSelection()
	{
		_view.Selection = _removal.Dragging ? _removal.Rectangle
			: _selection.Dragging ? _selection.Rectangle : null;
		_view.QueueRedraw();
	}

	void CancelSelection()
	{
		_selection.Cancel();
		_removal.Cancel();
	}

	Vector2I CellAt(Vector2 position)
	{
		var cell = (position - _view.Position) / WarrenView.CellSize;
		return new Vector2I(Mathf.FloorToInt(cell.X), Mathf.FloorToInt(cell.Y));
	}

	// The sim clips the rectangle to the map, so a drag that ran off the edge still lands.
	void OnSelected(Vector2I from, Vector2I to)
	{
		Command command = _tool switch
		{
			Tool.Dig => new Designate(DesignationKind.Dig, _viewZ, from.X, from.Y, to.X, to.Y),
			Tool.DigStairs => new Designate(DesignationKind.DigStairs, _viewZ, from.X, from.Y, to.X, to.Y),
			Tool.Pile => new PaintPile(_viewZ, from.X, from.Y, to.X, to.Y),
			_ => throw new InvalidOperationException("A selection needs a tool."),
		};
		_sim.Apply(command);
		_view.QueueRedraw();
		UpdateHover();
	}

	// Right-drag removes what the active Tool makes: Designations for Dig and Dig stairs (and with no Tool), Piles for Pile.
	void OnRemoved(Vector2I from, Vector2I to)
	{
		Command command = _tool == Tool.Pile
			? new RemovePile(_viewZ, from.X, from.Y, to.X, to.Y)
			: new CancelDesignation(_viewZ, from.X, from.Y, to.X, to.Y);
		_sim.Apply(command);
		_view.QueueRedraw();
		UpdateHover();
	}

	void PickTool(Tool tool)
	{
		CancelSelection();
		_tool = _tool == tool ? Tool.None : tool;
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
			case Key.D: PickTool(Tool.Dig); break;
			case Key.S: PickTool(Tool.DigStairs); break;
			case Key.P: PickTool(Tool.Pile); break;
			case Key.Escape: CancelSelection(); _tool = Tool.None; UpdateDepthLabel(); break;
			case Key.Comma: ChangeLevel(-1); break;
			case Key.Period: ChangeLevel(1); break;
			default: return;
		}
		GetViewport().SetInputAsHandled();
	}

	void ChangeLevel(int dz)
	{
		// A drag belongs to the level it started on.
		CancelSelection();
		_viewZ = Math.Clamp(_viewZ + dz, 0, _sim.State.Depth - 1);
		Refresh();
	}

	void Refresh()
	{
		_view.ViewZ = _viewZ;
		_view.QueueRedraw();
		UpdateDepthLabel();
		UpdateHover();
	}

	void UpdateDepthLabel()
	{
		var s = _sim.State;
		var pace = s.Paused ? "paused" : $"{s.Speed}×";
		var tool = _tool switch
		{
			Tool.Dig => " · dig",
			Tool.DigStairs => " · stairs",
			Tool.Pile => " · pile",
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
