using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>Draws the <see cref="WarrenSummary"/>: a stored and loose count per material, then the goblins by state.</summary>
public partial class SummaryPanel : VBoxContainer
{
	static readonly Goblins.Sim.Material[] Shown =
		[Goblins.Sim.Material.Grub, Goblins.Sim.Material.Grog, Goblins.Sim.Material.Fungus, Goblins.Sim.Material.Dirt, Goblins.Sim.Material.Stone];

	readonly Label[] _stored = new Label[Shown.Length];
	readonly Label[] _loose = new Label[Shown.Length];
	readonly Label _goblins;
	readonly Label[] _states = new Label[4];
	readonly Label _starving;

	public SummaryPanel()
	{
		AddThemeConstantOverride("separation", 6);
		AddChild(UiTheme.Text("WARREN", UiTheme.HeaderLabel));

		var stock = new GridContainer { Columns = 4 };
		stock.AddThemeConstantOverride("h_separation", 8);
		stock.AddThemeConstantOverride("v_separation", 2);
		AddChild(stock);
		stock.AddChild(new Control());
		stock.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
		stock.AddChild(Column("stored"));
		stock.AddChild(Column("loose"));
		for (var i = 0; i < Shown.Length; i++)
		{
			stock.AddChild(UiTheme.Swatch(WarrenView.MaterialColor(Shown[i])));
			stock.AddChild(new Label { Text = Shown[i].ToString() });
			_stored[i] = Number();
			stock.AddChild(_stored[i]);
			_loose[i] = Number();
			stock.AddChild(_loose[i]);
		}

		AddChild(new Control { CustomMinimumSize = new Vector2(0, 2) });
		_goblins = new Label();
		AddChild(_goblins);
		var states = new GridContainer { Columns = 4 };
		states.AddThemeConstantOverride("h_separation", 8);
		AddChild(states);
		string[] names = ["working", "idle", "craving", "asleep"];
		for (var i = 0; i < names.Length; i++)
		{
			states.AddChild(new Label { Text = names[i], SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = UiTheme.MutedLabel });
			_states[i] = Number();
			states.AddChild(_states[i]);
		}
		_starving = new Label();
		_starving.AddThemeColorOverride("font_color", UiTheme.WarningColor);
		AddChild(_starving);
	}

	static Label Column(string text)
	{
		var label = UiTheme.Text(text, UiTheme.MutedLabel);
		label.HorizontalAlignment = HorizontalAlignment.Right;
		label.CustomMinimumSize = new Vector2(48, 0);
		return label;
	}

	static Label Number() => new() { HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(28, 0) };

	public void Show(WarrenSummary summary)
	{
		for (var i = 0; i < Shown.Length; i++)
		{
			SetCount(_stored[i], summary.Stored(Shown[i]));
			SetCount(_loose[i], summary.Loose(Shown[i]));
		}
		_goblins.Text = summary.Goblins == 1 ? "1 Grunt" : $"{summary.Goblins} Grunts";
		SetCount(_states[0], summary.Working);
		SetCount(_states[1], summary.Idle);
		SetCount(_states[2], summary.Craving);
		SetCount(_states[3], summary.Asleep);
		_starving.Text = summary.Starving switch
		{
			0 => "",
			1 => "1 Starving, and very dignified about it.",
			var n => $"{n} Starving, and very dignified about it.",
		};
		_starving.Visible = summary.Starving > 0;
	}

	// A zero is muted, so the eye goes to what the warren actually has.
	static void SetCount(Label label, int count)
	{
		var text = count.ToString();
		if (label.Text == text)
			return;
		label.Text = text;
		label.AddThemeColorOverride("font_color", count == 0 ? UiTheme.MutedColor : UiTheme.TextColor);
	}
}
