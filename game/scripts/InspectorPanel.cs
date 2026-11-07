using System.Linq;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>Draws an <see cref="Inspection"/>: a title, what it is doing, meter bars for a goblin, and stock rows for a tile.</summary>
public partial class InspectorPanel : VBoxContainer
{
	readonly Label _title;
	readonly Label _doing;
	readonly GridContainer _meters;
	readonly MeterBar[] _bars = new MeterBar[3];
	readonly Label[] _values = new Label[3];
	readonly VBoxContainer _stock;
	readonly Label _note;
	Inspection _shown = Inspection.Nothing;

	public InspectorPanel()
	{
		AddThemeConstantOverride("separation", 6);
		AddChild(UiTheme.Text("UNDER THE CURSOR", UiTheme.HeaderLabel));
		_title = UiTheme.Text("", UiTheme.TitleLabel);
		AddChild(_title);
		_doing = UiTheme.Text("", wrap: true);
		AddChild(_doing);

		_meters = new GridContainer { Columns = 3 };
		_meters.AddThemeConstantOverride("h_separation", 8);
		_meters.AddThemeConstantOverride("v_separation", 6);
		AddChild(_meters);
		Need[] needs = [Need.Grub, Need.Grog, Need.Sleep];
		for (var i = 0; i < needs.Length; i++)
		{
			_meters.AddChild(new Label { Text = needs[i].ToString(), CustomMinimumSize = new Vector2(44, 0) });
			_bars[i] = new MeterBar { Need = needs[i] };
			_meters.AddChild(_bars[i]);
			_values[i] = new Label { CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
			_meters.AddChild(_values[i]);
		}

		_stock = new VBoxContainer();
		_stock.AddThemeConstantOverride("separation", 2);
		AddChild(_stock);
		_note = UiTheme.Text("", UiTheme.MutedLabel, wrap: true);
		AddChild(_note);
		Show(Inspection.Nothing);
	}

	public void Show(Inspection inspection)
	{
		_title.Text = inspection.Title;
		_title.Visible = inspection.Title != "";
		_doing.Text = inspection.Title == "" ? "Point at a goblin or a tile." : inspection.Doing;
		_doing.ThemeTypeVariation = inspection.Title == "" ? UiTheme.MutedLabel : "";
		_doing.Visible = _doing.Text != "";

		_meters.Visible = inspection.Meters.Count > 0;
		for (var i = 0; i < inspection.Meters.Count && i < _bars.Length; i++)
		{
			var value = inspection.Meters[i].Value;
			_bars[i].SetValue(value);
			_values[i].Text = value.ToString();
			_values[i].AddThemeColorOverride("font_color", NeedRules.IsCraving(value) ? UiTheme.WarningColor : UiTheme.TextColor);
		}

		// Rows are rebuilt only when the stock changes, not on every tick the cursor rests.
		if (!inspection.Stock.SequenceEqual(_shown.Stock))
		{
			foreach (var row in _stock.GetChildren())
				row.QueueFree();
			foreach (var line in inspection.Stock)
				_stock.AddChild(StockRow(line));
		}
		_stock.Visible = inspection.Stock.Count > 0;

		_note.Text = inspection.Note;
		_note.Visible = inspection.Note != "";
		_shown = inspection;
	}

	static HBoxContainer StockRow(StockLine line)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var color = line.Waiting ? WarrenView.WaitingHaulColor : WarrenView.MaterialColor(line.Material);
		row.AddChild(new TextureRect { Texture = UiTheme.Swatch(color), StretchMode = TextureRect.StretchModeEnum.KeepCentered });
		row.AddChild(new Label { Text = line.Material.ToString(), SizeFlagsHorizontal = SizeFlags.ExpandFill });
		var tag = line.Full ? "full" : line.Waiting ? "waiting" : "";
		if (tag != "")
		{
			var label = UiTheme.Text(tag, UiTheme.MutedLabel);
			if (line.Full)
				label.AddThemeColorOverride("font_color", WarrenView.FullCountChipColor);
			row.AddChild(label);
		}
		row.AddChild(new Label { Text = line.Count.ToString(), CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right });
		return row;
	}
}
