using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>One Need meter as a bar in its Need's colour, with a tick at the Craving threshold; the bar turns red below it.</summary>
public partial class MeterBar : Control
{
	static readonly Color TrackColor = new(0.08f, 0.07f, 0.06f);
	static readonly Color ThresholdColor = new(1f, 1f, 1f, 0.55f);

	public Need Need { get; set; }
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
		var fill = Mathf.Clamp(Value / (float)NeedRules.MeterMax, 0f, 1f);
		var color = NeedRules.IsCraving(Value) ? UiTheme.WarningColor : WarrenView.NeedColor(Need);
		DrawRect(new Rect2(0, 0, Size.X * fill, Size.Y), color);
		var x = Mathf.Round(Size.X * NeedRules.CravingThreshold / NeedRules.MeterMax);
		DrawLine(new Vector2(x, -2), new Vector2(x, Size.Y + 2), ThresholdColor, 1);
	}
}
