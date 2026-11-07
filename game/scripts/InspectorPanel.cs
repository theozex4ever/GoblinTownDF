using System.Collections.Generic;
using System.Linq;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>
/// Draws an <see cref="Inspection"/>: for a tile with goblins, one block per goblin in index order (name, what it is doing, Need and Chaos
/// bars); otherwise a title, what the tile is doing, and stock rows.
/// </summary>
public partial class InspectorPanel : VBoxContainer
{
	readonly Label _title;
	readonly Label _doing;
	readonly VBoxContainer _goblins;
	readonly List<GoblinBlock> _blocks = [];
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
		_goblins = new VBoxContainer();
		_goblins.AddThemeConstantOverride("separation", 10);
		AddChild(_goblins);
		_stock = new VBoxContainer();
		_stock.AddThemeConstantOverride("separation", 2);
		AddChild(_stock);
		_note = UiTheme.Text("", UiTheme.MutedLabel, wrap: true);
		AddChild(_note);
		Show(Inspection.Nothing);
	}

	/// <summary>How many goblin entries are shown: one per goblin on the inspected tile.</summary>
	public int EntryCount => _blocks.Count(block => block.Visible);

	/// <summary>The nth shown goblin entry, as laid out now; for checks on where it sits.</summary>
	public Control Entry(int index) => _blocks[index];

	public void Show(Inspection inspection)
	{
		var goblins = inspection.Goblins;
		_title.Text = inspection.Title;
		_title.Visible = inspection.Title != "" && goblins.Count == 0;
		_doing.Text = inspection.Title == "" ? "Point at a goblin or a tile." : inspection.Doing;
		_doing.ThemeTypeVariation = inspection.Title == "" ? UiTheme.MutedLabel : "";
		_doing.Visible = goblins.Count == 0 && _doing.Text != "";

		// One block per goblin, made once and reused; the note is left off when several share the tile, to keep the panel short.
		while (_blocks.Count < goblins.Count)
		{
			var block = new GoblinBlock();
			_blocks.Add(block);
			_goblins.AddChild(block);
		}
		for (var i = 0; i < _blocks.Count; i++)
		{
			_blocks[i].Visible = i < goblins.Count;
			if (i < goblins.Count)
				_blocks[i].Show(goblins[i], withNote: goblins.Count == 1);
		}
		_goblins.Visible = goblins.Count > 0;

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
		_note.Visible = inspection.Note != "" && goblins.Count == 0;
		_shown = inspection;
	}

	static HBoxContainer StockRow(StockLine line)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var color = line.Waiting ? WarrenView.WaitingHaulColor : WarrenView.MaterialColor(line.Material);
		row.AddChild(UiTheme.Swatch(color));
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

	/// <summary>One goblin of the tile: its name, what it is doing (in the shirking colour while it shirks), its three Need bars and its Chaos bar.</summary>
	sealed partial class GoblinBlock : VBoxContainer
	{
		readonly Label _title = UiTheme.Text("", UiTheme.TitleLabel);
		readonly Label _doing = UiTheme.Text("", wrap: true);
		readonly MeterBar[] _bars = new MeterBar[4];
		readonly Label[] _values = new Label[4];
		readonly Label _note = UiTheme.Text("", UiTheme.MutedLabel, wrap: true);

		public GoblinBlock()
		{
			AddThemeConstantOverride("separation", 4);
			AddChild(_title);
			AddChild(_doing);
			var meters = new GridContainer { Columns = 3 };
			meters.AddThemeConstantOverride("h_separation", 8);
			meters.AddThemeConstantOverride("v_separation", 6);
			AddChild(meters);
			string[] names = ["Grub", "Grog", "Sleep", "Chaos"];
			Need[] needs = [Need.Grub, Need.Grog, Need.Sleep, Need.None];
			for (var i = 0; i < names.Length; i++)
			{
				meters.AddChild(new Label { Text = names[i], CustomMinimumSize = new Vector2(52, 0) });
				_bars[i] = new MeterBar { Need = needs[i], IsChaos = i == 3 };
				meters.AddChild(_bars[i]);
				_values[i] = new Label { CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
				meters.AddChild(_values[i]);
			}
			AddChild(_note);
		}

		public void Show(GoblinReading reading, bool withNote)
		{
			_title.Text = reading.Title;
			_doing.Text = reading.Doing;
			_doing.AddThemeColorOverride("font_color", reading.Shirking ? WarrenView.ShirkColor : UiTheme.TextColor);
			for (var i = 0; i < _bars.Length; i++)
			{
				var value = i < reading.Meters.Count ? reading.Meters[i].Value : reading.Chaos;
				var warn = i < reading.Meters.Count ? NeedRules.IsCraving(value) : value >= ChaosRules.ActingOutThreshold;
				_bars[i].SetValue(value);
				_values[i].Text = value.ToString();
				_values[i].AddThemeColorOverride("font_color", warn ? UiTheme.WarningColor : UiTheme.TextColor);
			}
			_note.Text = reading.Note;
			_note.Visible = withNote && reading.Note != "";
		}
	}
}
