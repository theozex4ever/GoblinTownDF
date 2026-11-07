using System;
using System.Collections.Generic;
using Godot;

namespace AThousandGoblinsDeep;

/// <summary>
/// One toggle button per Tool, grouped Dig and Rooms, with the picked Tool's hint beneath. It only reports clicks;
/// <see cref="Main"/> picks the Tool and calls <see cref="Sync"/>, so the buttons follow the keys too.
/// </summary>
public partial class ToolPalette : VBoxContainer
{
	readonly Dictionary<Tool, Button> _buttons = new();
	readonly Label _hint;

	public event Action<Tool>? ToolClicked;

	public ToolPalette()
	{
		AddThemeConstantOverride("separation", 4);
		AddSection("DIG", ToolInfo.DigTools);
		AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
		AddSection("ROOMS", ToolInfo.RoomTools);
		AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
		_hint = UiTheme.Text("", UiTheme.MutedLabel, wrap: true);
		AddChild(_hint);
	}

	public string Hint => _hint.Text;

	void AddSection(string title, Tool[] tools)
	{
		AddChild(UiTheme.Text(title, UiTheme.HeaderLabel));
		foreach (var tool in tools)
		{
			var button = new Button
			{
				Name = $"Tool{tool}",
				Text = $"{ToolInfo.Label(tool)} · {OS.GetKeycodeString(ToolInfo.KeyOf(tool))}",
				Icon = UiTheme.Swatch(ToolInfo.Swatch(tool)),
				TooltipText = ToolInfo.Tooltip(tool),
				ToggleMode = true,
				// No focus, or Space would press the last Tool clicked instead of pausing.
				FocusMode = FocusModeEnum.None,
				Alignment = HorizontalAlignment.Left,
			};
			button.Pressed += () => ToolClicked?.Invoke(tool);
			_buttons[tool] = button;
			AddChild(button);
		}
	}

	public void Sync(Tool picked)
	{
		foreach (var (tool, button) in _buttons)
			button.SetPressedNoSignal(tool == picked);
		_hint.Text = ToolInfo.Hint(picked);
	}
}
