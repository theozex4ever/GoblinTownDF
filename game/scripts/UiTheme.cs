using System.Collections.Generic;
using Godot;

namespace AThousandGoblinsDeep;

/// <summary>
/// The one Theme every HUD Control uses, built in code so the grey box needs no asset files. Every Resource made here is
/// handed to a node and its managed wrapper disposed at once: the node keeps its own reference, and no wrapper is left
/// for the garbage collector to race the engine's shutdown.
/// </summary>
public static class UiTheme
{
	public static readonly Color Background = new(0.10f, 0.09f, 0.08f);
	public static readonly Color PanelColor = new(0.16f, 0.145f, 0.125f);
	public static readonly Color BorderColor = new(0.26f, 0.235f, 0.20f);
	public static readonly Color TextColor = new(0.91f, 0.88f, 0.82f);
	public static readonly Color MutedColor = new(0.64f, 0.60f, 0.54f);
	public static readonly Color AccentColor = new(0.42f, 0.75f, 0.31f);
	public static readonly Color WarningColor = new(0.92f, 0.36f, 0.28f);

	static readonly Color ButtonColor = new(0.22f, 0.20f, 0.17f);
	static readonly Color ButtonHoverColor = new(0.28f, 0.255f, 0.22f);
	static readonly Color ButtonPressedColor = new(0.23f, 0.33f, 0.17f);

	/// <summary>A small all-caps section heading, muted.</summary>
	public const string HeaderLabel = "HeaderLabel";
	/// <summary>The large first line of a panel, such as the Inspector's title.</summary>
	public const string TitleLabel = "TitleLabel";
	/// <summary>Secondary text: hints, notes and captions.</summary>
	public const string MutedLabel = "MutedLabel";

	// Godot's own default is 16; the HUD reads a size up from it.
	public const int FontSize = 17;

	/// <summary>Gives <paramref name="root"/>, and so every Control under it, this Theme.</summary>
	public static void ApplyTo(Control root)
	{
		var made = new List<Resource>();
		using var theme = Build(made);
		root.Theme = theme;
		foreach (var resource in made)
			resource.Dispose();
	}

	static Theme Build(List<Resource> made)
	{
		var theme = new Theme { DefaultFontSize = FontSize };
		StyleBoxFlat Box(Color fill, Color border, int padding)
		{
			var box = new StyleBoxFlat { BgColor = fill, BorderColor = border };
			box.SetBorderWidthAll(1);
			box.SetCornerRadiusAll(4);
			box.SetContentMarginAll(padding);
			made.Add(box);
			return box;
		}

		theme.SetStylebox("panel", "PanelContainer", Box(PanelColor, BorderColor, padding: 10));
		theme.SetColor("font_color", "Label", TextColor);

		theme.SetTypeVariation(HeaderLabel, "Label");
		theme.SetColor("font_color", HeaderLabel, MutedColor);
		theme.SetFontSize("font_size", HeaderLabel, 12);

		theme.SetTypeVariation(TitleLabel, "Label");
		theme.SetFontSize("font_size", TitleLabel, 20);

		theme.SetTypeVariation(MutedLabel, "Label");
		theme.SetColor("font_color", MutedLabel, MutedColor);
		theme.SetFontSize("font_size", MutedLabel, 14);

		var normal = Box(ButtonColor, BorderColor, padding: 6);
		var hover = Box(ButtonHoverColor, BorderColor, padding: 6);
		var pressed = Box(ButtonPressedColor, AccentColor, padding: 6);
		theme.SetStylebox("normal", "Button", normal);
		theme.SetStylebox("hover", "Button", hover);
		theme.SetStylebox("pressed", "Button", pressed);
		theme.SetStylebox("hover_pressed", "Button", pressed);
		theme.SetStylebox("disabled", "Button", normal);
		var noFocus = new StyleBoxEmpty();
		made.Add(noFocus);
		theme.SetStylebox("focus", "Button", noFocus);
		foreach (var name in new[] { "font_color", "font_hover_color", "font_focus_color" })
			theme.SetColor(name, "Button", TextColor);
		theme.SetColor("font_pressed_color", "Button", Colors.White);
		theme.SetColor("font_hover_pressed_color", "Button", Colors.White);
		theme.SetConstant("h_separation", "Button", 6);

		theme.SetStylebox("panel", "TooltipPanel", Box(Background, BorderColor, padding: 8));
		theme.SetColor("font_color", "TooltipLabel", TextColor);

		var line = new StyleBoxLine { Color = BorderColor, Thickness = 1 };
		made.Add(line);
		theme.SetStylebox("separator", "HSeparator", line);
		return theme;
	}

	/// <summary>A 12 px square of <paramref name="color"/>, solid or, with <paramref name="outline"/>, a two-pixel frame, as a TextureRect.</summary>
	public static TextureRect Swatch(Color color, bool outline = false)
	{
		using var texture = SwatchTexture(color, outline);
		return new TextureRect { Texture = texture, StretchMode = TextureRect.StretchModeEnum.KeepCentered };
	}

	/// <summary>Gives <paramref name="button"/> a solid square of <paramref name="color"/> as its icon.</summary>
	public static void SetSwatchIcon(Button button, Color color)
	{
		using var texture = SwatchTexture(color, outline: false);
		button.Icon = texture;
	}

	static ImageTexture SwatchTexture(Color color, bool outline)
	{
		const int size = 12;
		using var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(color);
		if (outline)
			image.FillRect(new Rect2I(2, 2, size - 4, size - 4), Colors.Transparent);
		return ImageTexture.CreateFromImage(image);
	}

	/// <summary>A Label in one of this Theme's type variations.</summary>
	public static Label Text(string text, string variation = "", bool wrap = false)
	{
		var label = new Label { Text = text, ThemeTypeVariation = variation };
		if (wrap)
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		return label;
	}
}
