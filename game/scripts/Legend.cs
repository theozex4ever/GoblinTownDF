using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>What the map's colours mean, under a button that folds it away.</summary>
public partial class Legend : VBoxContainer
{
	readonly VBoxContainer _rows;

	public Legend()
	{
		AddThemeConstantOverride("separation", 4);
		var toggle = new Button
		{
			Name = "LegendToggle",
			Text = "What the colours mean",
			ToggleMode = true,
			ButtonPressed = true,
			Flat = true,
			FocusMode = FocusModeEnum.None,
			Alignment = HorizontalAlignment.Left,
		};
		AddChild(toggle);
		_rows = new VBoxContainer();
		_rows.AddThemeConstantOverride("separation", 2);
		AddChild(_rows);
		toggle.Toggled += shown => _rows.Visible = shown;

		Row("A goblin", WarrenView.GoblinColor);
		Row("A goblin one level off", WarrenView.FaintGoblinColor);
		Row("Starving", WarrenView.StarvingGoblinColor);
		Row("Pip: craving Grub", WarrenView.NeedColor(Need.Grub));
		Row("Pip: craving Grog", WarrenView.NeedColor(Need.Grog));
		Row("Pip: craving a lie-down", WarrenView.NeedColor(Need.Sleep));
		Row("Loose, no Pile has room for it", WarrenView.WaitingHaulColor);
		Row("A full Pile count: thirty", WarrenView.FullCountChipColor);
		Row("Marked for digging", UiTheme.OutlineSwatch(WarrenView.DesignationColor));
		Row("Marked, out of reach", UiTheme.OutlineSwatch(WarrenView.UnreachableColor));
	}

	void Row(string text, Color color) => Row(text, UiTheme.Swatch(color));

	void Row(string text, Texture2D swatch)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(new TextureRect { Texture = swatch, StretchMode = TextureRect.StretchModeEnum.KeepCentered });
		row.AddChild(UiTheme.Text(text, UiTheme.MutedLabel));
		_rows.AddChild(row);
	}
}
