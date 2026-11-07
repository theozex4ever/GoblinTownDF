using System;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>
/// Pause, Speed and the viewed level as buttons, plus the clock. It only reports clicks; <see cref="Main"/> turns them into
/// the same Commands the keys send, then calls <see cref="Sync"/> so the buttons show the state whichever way it changed.
/// </summary>
public partial class TopBar : HBoxContainer
{
	static readonly int[] Speeds = [1, 2, 4];

	readonly Button _pause;
	readonly Button[] _speeds = new Button[Speeds.Length];
	readonly Button _up;
	readonly Button _down;
	readonly Label _depth;
	readonly Label _clock;

	public event Action? PauseClicked;
	public event Action<int>? SpeedClicked;
	/// <summary>-1 to go up a level, 1 to go down.</summary>
	public event Action<int>? LevelClicked;

	public TopBar()
	{
		AddThemeConstantOverride("separation", 6);

		_pause = MakeButton("Pause", "Pause · Space", toggle: true);
		_pause.TooltipText = "Stops the clock. The goblins stop too, mid-thought, and do not mind.";
		_pause.CustomMinimumSize = new Vector2(132, 0);
		_pause.Pressed += () => PauseClicked?.Invoke();

		for (var i = 0; i < Speeds.Length; i++)
		{
			var speed = Speeds[i];
			_speeds[i] = MakeButton($"Speed{speed}", $"{speed}×", toggle: true);
			_speeds[i].TooltipText = $"Runs the warren at {speed}× speed. Key {speed}.";
			_speeds[i].Pressed += () => SpeedClicked?.Invoke(speed);
		}

		AddChild(new VSeparator());
		_up = MakeButton("DepthUp", "Up · ,", toggle: false);
		_up.TooltipText = "Look one level up, towards the sky and its many disappointments.";
		_up.Pressed += () => LevelClicked?.Invoke(-1);
		_depth = new Label { CustomMinimumSize = new Vector2(150, 0), HorizontalAlignment = HorizontalAlignment.Center };
		AddChild(_depth);
		_down = MakeButton("DepthDown", "Down · .", toggle: false);
		_down.TooltipText = "Look one level down, where the good stuff is.";
		_down.Pressed += () => LevelClicked?.Invoke(1);

		AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
		_clock = UiTheme.Text("", UiTheme.MutedLabel);
		_clock.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		AddChild(_clock);
	}

	public string DepthText => _depth.Text;
	public string ClockText => _clock.Text;

	Button MakeButton(string name, string text, bool toggle)
	{
		// No focus, or Space would press whichever button was clicked last instead of pausing.
		var button = new Button { Name = name, Text = text, ToggleMode = toggle, FocusMode = FocusModeEnum.None };
		AddChild(button);
		return button;
	}

	public void Sync(SimState s, int viewZ)
	{
		_pause.SetPressedNoSignal(s.Paused);
		_pause.Text = s.Paused ? "Paused · Space" : "Pause · Space";
		for (var i = 0; i < Speeds.Length; i++)
			_speeds[i].SetPressedNoSignal(s.Speed == Speeds[i]);
		_up.Disabled = viewZ <= 0;
		_down.Disabled = viewZ >= s.Depth - 1;
		_depth.Text = $"Depth {viewZ} · {BandName(viewZ)}";
		SyncClock(s);
	}

	public void SyncClock(SimState s) => _clock.Text = $"Tick {s.Tick}";

	// The bands from the GDD's World and z-levels table.
	public static string BandName(int z) => z switch
	{
		0 => "Surface",
		<= 3 => "Dirt",
		<= 9 => "Stone",
		<= 13 => "Ore",
		_ => "The Deep",
	};
}
