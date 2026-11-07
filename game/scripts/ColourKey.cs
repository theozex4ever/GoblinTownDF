using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>What the map's colours mean, under a button that folds it away. Not a Legend: those are chronicle entries.</summary>
public partial class ColourKey : VBoxContainer
{
	readonly VBoxContainer _rows;

	public ColourKey()
	{
		AddThemeConstantOverride("separation", 4);
		var toggle = new Button
		{
			Name = "ColourKeyToggle",
			Text = "What the colours mean…",
			ToggleMode = true,
			ButtonPressed = false,
			Flat = true,
			FocusMode = FocusModeEnum.None,
			Alignment = HorizontalAlignment.Left,
		};
		AddChild(toggle);
		_rows = new VBoxContainer { Visible = false };
		_rows.AddThemeConstantOverride("separation", 2);
		AddChild(_rows);
		toggle.Toggled += shown => _rows.Visible = shown;

		Row("A goblin", WarrenView.GoblinColor);
		Row("A goblin one level off", WarrenView.FaintGoblinColor);
		Row("Starving", WarrenView.StarvingGoblinColor);
		Row("Pip: craving Grub", WarrenView.NeedColor(Need.Grub));
		Row("Pip: craving Grog", WarrenView.NeedColor(Need.Grog));
		Row("Pip: craving a lie-down", WarrenView.NeedColor(Need.Sleep));
		Row("Ring: shirking", WarrenView.ShirkColor, outline: true);
		Row("Ring: in a fight (crossed mid-scuffle)", WarrenView.FightColor, outline: true);
		Row("Ring: getting over a fight", WarrenView.FightRestColor, outline: true);
		Row("Ring and mask: stealing", WarrenView.ThiefColor, outline: true);
		MaterialRow();
		Row("Loose, no Pile has room for it", WarrenView.WaitingHaulColor);
		Row("A full Pile count: thirty", WarrenView.FullCountChipColor);
		Row("Marked for digging", WarrenView.DesignationColor, outline: true);
		Row("Marked, out of reach", WarrenView.UnreachableColor, outline: true);
	}

	// Dirt, Stone, Grub, Fungus and Grog side by side: the colour of a loose square, a carried square and a Pile count.
	void MaterialRow()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 2);
		for (var material = Goblins.Sim.Material.Dirt; (int)material <= SimState.CountedMaterials; material++)
			row.AddChild(UiTheme.Swatch(WarrenView.MaterialColor(material)));
		row.AddChild(new Control { CustomMinimumSize = new Vector2(4, 0) });
		row.AddChild(UiTheme.Text("Materials", UiTheme.MutedLabel));
		_rows.AddChild(row);
	}

	void Row(string text, Color color, bool outline = false)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(UiTheme.Swatch(color, outline));
		row.AddChild(UiTheme.Text(text, UiTheme.MutedLabel));
		_rows.AddChild(row);
	}
}
