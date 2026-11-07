using System;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

public partial class Main : Node
{
	Simulation _sim = null!;
	WarrenView _view = null!;
	TopBar _topBar = null!;
	ToolPalette _palette = null!;
	InspectorPanel _inspector = null!;
	SummaryPanel _summaryPanel = null!;
	readonly WarrenSummary _summary = new();
	// The map cell under the cursor, from the last mouse motion; kept so the Inspector follows the sim as it changes the tile.
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
		_topBar = new TopBar();
		_topBar.PauseClicked += () => Apply(new SetPaused(!_sim.State.Paused));
		_topBar.SpeedClicked += speed => Apply(new SetSpeed(speed));
		_topBar.LevelClicked += ChangeLevel;
		topBar.AddChild(_topBar);

		var body = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 8);
		column.AddChild(body);

		var left = new PanelContainer { CustomMinimumSize = new Vector2(LeftPanelWidth, 0) };
		body.AddChild(left);
		var leftColumn = new VBoxContainer();
		left.AddChild(leftColumn);
		_palette = new ToolPalette();
		_palette.ToolClicked += PickTool;
		leftColumn.AddChild(_palette);
		leftColumn.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
		leftColumn.AddChild(new Legend());

		var mapArea = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		body.AddChild(mapArea);
		var mapFrame = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = mapSize };
		mapArea.AddChild(mapFrame);
		mapFrame.AddChild(_view);

		var right = new PanelContainer { CustomMinimumSize = new Vector2(RightPanelWidth, 0) };
		body.AddChild(right);
		var rightColumn = new VBoxContainer();
		rightColumn.AddThemeConstantOverride("separation", 12);
		right.AddChild(rightColumn);
		_summaryPanel = new SummaryPanel();
		rightColumn.AddChild(_summaryPanel);
		rightColumn.AddChild(new HSeparator());
		_inspector = new InspectorPanel();
		rightColumn.AddChild(_inspector);
	}

	const int LeftPanelWidth = 230;
	const int RightPanelWidth = 290;

	public override void _Process(double delta)
	{
		if (_sim.Advance(delta) > 0)
		{
			_view.QueueRedraw();
			UpdateHover();
			_topBar.SyncClock(_sim.State);
		}
		if (_sim.DrainEvents().Count > 0)
			SyncHud();
		// Counted every frame, so changes made outside a tick (a Pile painted while paused) show at once.
		_summary.Count(_sim.State);
		_summaryPanel.Show(_summary);
	}

	/// <summary>The Warren summary as of the last frame.</summary>
	public WarrenSummary Summary => _summary;

	/// <summary>The depth readout in the top bar: the viewed level and its Band.</summary>
	public string DepthText => _topBar.DepthText;

	/// <summary>The clock in the top bar.</summary>
	public string ClockText => _topBar.ClockText;

	/// <summary>What the Inspector shows: the goblin or tile under the cursor on the viewed level, or <see cref="Inspection.Nothing"/> off the map.</summary>
	public Inspection Inspection { get; private set; } = Inspection.Nothing;

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
		Inspection = inMap ? Inspector.Inspect(s, s.TileIndex(_hoverCell.X, _hoverCell.Y, _viewZ)) : Inspection.Nothing;
		_inspector.Show(Inspection);
		Vector2I? hover = inMap ? _hoverCell : null;
		if (_view.Hover != hover)
		{
			_view.Hover = hover;
			_view.QueueRedraw();
		}
	}

	void UpdateSelection()
	{
		_view.Selection = _removal.Dragging ? _removal.Rectangle
			: _selection.Dragging ? _selection.Rectangle : null;
		_view.SelectionColor = _removal.Dragging ? WarrenView.RemovalColor : ToolInfo.Swatch(_tool);
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

	static Tool ToolOf(Key key)
	{
		foreach (var tool in Enum.GetValues<Tool>())
			if (tool != Tool.None && ToolInfo.KeyOf(tool) == key)
				return tool;
		throw new ArgumentOutOfRangeException(nameof(key), key, "No Tool has this key.");
	}

	/// <summary>The how-to under the Tool palette, for the picked Tool or for none.</summary>
	public string ToolHint => _palette.Hint;

	void PickTool(Tool tool)
	{
		CancelSelection();
		_tool = _tool == tool ? Tool.None : tool;
		SyncHud();
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;

		switch (key.Keycode)
		{
			case Key.Space: Apply(new SetPaused(!_sim.State.Paused)); break;
			case Key.Key1 or Key.Kp1: Apply(new SetSpeed(1)); break;
			case Key.Key2 or Key.Kp2: Apply(new SetSpeed(2)); break;
			case Key.Key4 or Key.Kp4: Apply(new SetSpeed(4)); break;
			case Key.D or Key.S or Key.P or Key.G or Key.F or Key.B or Key.H: PickTool(ToolOf(key.Keycode)); break;
			case Key.Escape: CancelSelection(); _tool = Tool.None; SyncHud(); break;
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
		SyncHud();
		UpdateHover();
	}

	// A player's Command, from a key or a button: the HUD shows its effect at once rather than next frame.
	void Apply(Command command)
	{
		_sim.Apply(command);
		SyncHud();
	}

	// Every button and readout shows the sim and view as they are now, however they got that way.
	void SyncHud()
	{
		_topBar.Sync(_sim.State, _viewZ);
		// The map's paused frame comes and goes with the pause.
		_view.QueueRedraw();
		_palette.Sync(_tool);
	}
}
