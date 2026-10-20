using System;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>Draws one z-level of the warren as grey boxes. Reads sim state; never writes it.</summary>
public partial class WarrenView : Node2D
{
	public const int CellSize = 18;

	static readonly Color FloorColor = new(0.62f, 0.62f, 0.64f);
	static readonly Color DirtColor = new(0.30f, 0.26f, 0.22f);
	static readonly Color GridColor = new(0f, 0f, 0f, 0.15f);
	static readonly Color GoblinColor = new(0.35f, 0.75f, 0.30f);
	static readonly Color FaintGoblinColor = new(0.35f, 0.75f, 0.30f, 0.35f);

	public Simulation? Sim { get; set; }
	public int ViewZ { get; set; }

	public override void _Draw()
	{
		if (Sim is null)
			return;

		var s = Sim.State;
		for (var y = 0; y < s.Height; y++)
		for (var x = 0; x < s.Width; x++)
		{
			var color = s.KindAt(s.TileIndex(x, y, ViewZ)) == TileKind.Solid ? DirtColor : FloorColor;
			var rect = new Rect2(x * CellSize, y * CellSize, CellSize, CellSize);
			DrawRect(rect, color);
			DrawRect(rect, GridColor, filled: false);
		}

		for (var g = 0; g < s.GoblinCount; g++)
		{
			var dz = s.GoblinZ[g] - ViewZ;
			if (Math.Abs(dz) > 1)
				continue;

			var inset = dz == 0 ? 3 : 6;
			var rect = new Rect2(
				s.GoblinX[g] * CellSize + inset,
				s.GoblinY[g] * CellSize + inset,
				CellSize - 2 * inset,
				CellSize - 2 * inset);
			DrawRect(rect, dz == 0 ? GoblinColor : FaintGoblinColor);
		}
	}
}
