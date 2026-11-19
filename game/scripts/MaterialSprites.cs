using System;
using Godot;
using Goblins.Sim;
using Material = Goblins.Sim.Material;

namespace AThousandGoblinsDeep;

/// <summary>The shared item artwork used on the map and in the HUD. Has no simulation state.</summary>
internal static class MaterialSprites
{
	static readonly Texture2D?[] Normal = new Texture2D?[SimState.CountedMaterials + 1];
	static readonly Texture2D?[] Waiting = new Texture2D?[SimState.CountedMaterials + 1];

	public static Texture2D Get(Material material, bool waiting = false)
	{
		var index = (int)material;
		if (index < 1 || index > SimState.CountedMaterials)
			throw new ArgumentOutOfRangeException(nameof(material), material, "No sprite for this material.");
		var normal = Normal[index] ??= GD.Load<Texture2D>($"res://art/materials/{material.ToString().ToLowerInvariant()}.png");
		if (!waiting)
			return normal;
		if (Waiting[index] is not { } grey)
		{
			using var image = normal.GetImage();
			image.AdjustBcs(0.8f, 1, 0);
			Waiting[index] = grey = ImageTexture.CreateFromImage(image);
		}
		return grey;
	}

	public static TextureRect Icon(Material material, bool waiting = false) => new()
	{
		Texture = Get(material, waiting),
		TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
		ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
		StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		CustomMinimumSize = new Vector2(16, 16),
		MouseFilter = Control.MouseFilterEnum.Ignore,
	};
}
