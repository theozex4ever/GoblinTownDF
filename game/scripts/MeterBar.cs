using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>
/// One Need meter as a bar in its Need's colour, with a tick at the Craving threshold; the bar turns red below it. With <see cref="IsChaos"/> it is a
/// Chaos meter instead: its own colour, a tick where a goblin may begin acting out, and red from there up.
/// </summary>
public partial class MeterBar : Control
{
	static readonly Color TrackColor = new(0.08f, 0.07f, 0.06f);
	static readonly Color ThresholdColor = new(1f, 1f, 1f, 0.55f);

	public static readonly Color ChaosColor = new(0.80f, 0.45f, 0.95f);

	public Need Need { get; set; }
	public bool IsChaos { get; set; }
	public int Value { get; private set; }

	public MeterBar()
	{
		CustomMinimumSize = new Vector2(0, 12);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void SetValue(int value)
	{
		if (value == Value)
			return;
		Value = value;
		QueueRedraw();
	}

	public override void _Draw()
	{
		var track = new Rect2(Vector2.Zero, Size);
		DrawRect(track, TrackColor);
		var max = IsChaos ? ChaosRules.Max : NeedRules.MeterMax;
		var fill = Mathf.Clamp(Value / (float)max, 0f, 1f);
		var color = IsChaos
			? Value >= ChaosRules.ActingOutThreshold ? UiTheme.WarningColor : ChaosColor
			: NeedRules.IsCraving(Value) ? UiTheme.WarningColor : WarrenView.NeedColor(Need);
		DrawRect(new Rect2(0, 0, Size.X * fill, Size.Y), color);
		var x = Mathf.Round(Size.X * (IsChaos ? ChaosRules.ActingOutThreshold : NeedRules.CravingThreshold) / max);
		DrawLine(new Vector2(x, -2), new Vector2(x, Size.Y + 2), ThresholdColor, 1);
	}
}
