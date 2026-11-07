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
		GrubPit,
		FungusFarm,
		Brewery,
		Heap,
	}

	const string KeyHint = "Space naps the warren. 1, 2, 4 hurry it. , and . change level. D digs, S makes stairs, P heaps things in a pile, G sets a pit aside for grubs, who need minding, F plants a fungus farm, but only down the stairs, since fungus will not grow where anyone can see it, B sets up a brewery, where a goblin stands over the fungus until it gives in and becomes grog, and H lays out a heap to sleep on, which is a bed with the pretensions removed; drag a box. Right-drag unsays it. The Grunt takes it personally.";

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

		var s = _sim.State;
		_view = new WarrenView { Sim = _sim };
		BuildLayout(new Vector2(s.Width, s.Height) * WarrenView.CellSize);

		_selection = new SelectionGesture(MouseButton.Left, new Vector2I(s.Width, s.Height), CellAt);
		_removal = new SelectionGesture(MouseButton.Right, new Vector2I(s.Width, s.Height), CellAt);
		_selection.Selected += OnSelected;
		_removal.Selected += OnRemoved;
		_selection.Changed += UpdateSelection;
		_removal.Changed += UpdateSelection;

		Refresh();
	}

	/// <summary>The view of the warren, wherever the layout has put it.</summary>
	public WarrenView View => _view;

	// A top bar over three columns: the left panel, the map centred in what is left, and the right panel. Panels stop the
	// mouse, so a click on one never reaches the map; everything else lets it through to _UnhandledInput.
	void BuildLayout(Vector2 mapSize)
	{
		var root = new Control { Theme = UiTheme.Build(), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		var background = new ColorRect { Color = UiTheme.Background, MouseFilter = Control.MouseFilterEnum.Ignore };
		background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(background);

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		foreach (var side in new[] { "left", "top", "right", "bottom" })
			margin.AddThemeConstantOverride($"margin_{side}", 8);
		root.AddChild(margin);

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 8);
		margin.AddChild(column);

		var topBar = new PanelContainer();
		column.AddChild(topBar);
		_depthLabel = new Label();
		topBar.AddChild(_depthLabel);

		var body = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 8);
		column.AddChild(body);

		var left = new PanelContainer { CustomMinimumSize = new Vector2(LeftPanelWidth, 0) };
		body.AddChild(left);
		left.AddChild(UiTheme.Text(KeyHint, UiTheme.MutedLabel, wrap: true));

		var mapArea = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		body.AddChild(mapArea);
		var mapFrame = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = mapSize };
		mapArea.AddChild(mapFrame);
		mapFrame.AddChild(_view);

		var right = new PanelContainer { CustomMinimumSize = new Vector2(RightPanelWidth, 0) };
		body.AddChild(right);
		_hoverLabel = UiTheme.Text("", wrap: true);
		right.AddChild(_hoverLabel);
	}

	const int LeftPanelWidth = 230;
	const int RightPanelWidth = 290;

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

	/// <summary>The hover line: the Grub, Grog and Sleep meters of the goblin under the cursor, else every count on the Pile tile under it, else empty.</summary>
	public string HoverText => _hoverLabel.Text;

	// A drag in progress owns the mouse before the panels see it, so dragging off the map over a panel still sweeps and
	// still ends; the gesture clamps the far corner to the map. Motion also moves the hover here, so leaving the map for
	// a panel clears it.
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
			Hover(motion.Position);
		if (@event is InputEventMouse && (_selection.Dragging || _removal.Dragging) && HandleMouse(@event))
			GetViewport().SetInputAsHandled();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
			Hover(motion.Position);
		if (HandleMouse(@event))
			GetViewport().SetInputAsHandled();
	}

	void Hover(Vector2 position)
	{
		_hoverCell = CellAt(position);
		UpdateHover();
	}

	// Only one button owns a drag; removal is available with no tool picked.
	bool HandleMouse(InputEvent @event)
	{
		if (_selection.Dragging)
			return _selection.Handle(@event);
		if (_removal.Dragging)
			return _removal.Handle(@event);
		return _removal.Handle(@event) || (_tool != Tool.None && _selection.Handle(@event));
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
		var cell = (position - _view.GlobalPosition) / WarrenView.CellSize;
		return new Vector2I(Mathf.FloorToInt(cell.X), Mathf.FloorToInt(cell.Y));
	}

	// The sim clips the rectangle to the map, so a drag that ran off the edge still lands.
	void OnSelected(Vector2I from, Vector2I to)
	{
		Command command = _tool switch
		{
			Tool.Dig => new Designate(DesignationKind.Dig, _viewZ, from.X, from.Y, to.X, to.Y),
			Tool.DigStairs => new Designate(DesignationKind.DigStairs, _viewZ, from.X, from.Y, to.X, to.Y),
			Tool.Pile or Tool.GrubPit or Tool.FungusFarm or Tool.Brewery or Tool.Heap => new PaintRoom(RoomOf(_tool), _viewZ, from.X, from.Y, to.X, to.Y),
			_ => throw new InvalidOperationException("A selection needs a tool."),
		};
		_sim.Apply(command);
		_view.QueueRedraw();
		UpdateHover();
	}

	// Right-drag removes what the active Tool makes: Designations for Dig and Dig stairs (and with no Tool), that Room for a Room's Tool.
	void OnRemoved(Vector2I from, Vector2I to)
	{
		Command command = _tool is Tool.Pile or Tool.GrubPit or Tool.FungusFarm or Tool.Brewery or Tool.Heap
			? new RemoveRoom(RoomOf(_tool), _viewZ, from.X, from.Y, to.X, to.Y)
			: new CancelDesignation(_viewZ, from.X, from.Y, to.X, to.Y);
		_sim.Apply(command);
		_view.QueueRedraw();
		UpdateHover();
	}

	static RoomKind RoomOf(Tool tool) => tool switch
	{
		Tool.Pile => RoomKind.Pile,
		Tool.GrubPit => RoomKind.GrubPit,
		Tool.FungusFarm => RoomKind.FungusFarm,
		Tool.Brewery => RoomKind.Brewery,
		Tool.Heap => RoomKind.SleepingHeap,
		_ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "This Tool makes no Room."),
	};

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
			case Key.G: PickTool(Tool.GrubPit); break;
			case Key.F: PickTool(Tool.FungusFarm); break;
			case Key.B: PickTool(Tool.Brewery); break;
			case Key.H: PickTool(Tool.Heap); break;
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
			Tool.GrubPit => " · grub pit",
			Tool.FungusFarm => " · fungus farm",
			Tool.Brewery => " · brewery",
			Tool.Heap => " · sleeping heap",
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
