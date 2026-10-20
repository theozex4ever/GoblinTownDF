using System;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

public partial class Main : Node
{
	const string KeyHint = "Space naps the warren. 1, 2, 4 hurry it. , and . change level. The Grunt approves of all of it.";

	Simulation _sim = null!;
	WarrenView _view = null!;
	Label _depthLabel = null!;
	int _viewZ;

	public override void _Ready()
	{
		var seed = (int)Time.GetUnixTimeFromSystem();
		_sim = Simulation.NewWorld(seed);
		GD.Print($"A Thousand Goblins Deep: boot ok (seed {seed})");

		_view = new WarrenView { Sim = _sim, Position = new Vector2(288, 36) };
		AddChild(_view);

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
			case Key.Comma: ChangeLevel(-1); break;
			case Key.Period: ChangeLevel(1); break;
			default: return;
		}
		GetViewport().SetInputAsHandled();
	}

	void ChangeLevel(int dz)
	{
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
		_depthLabel.Text = $"Depth {_viewZ} · {BandName(_viewZ)} · {pace}";
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
