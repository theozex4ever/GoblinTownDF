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
	static readonly Color StairsColor = new(0.78f, 0.74f, 0.58f);
	static readonly Color StairsMarkColor = new(0.25f, 0.22f, 0.15f);
	static readonly Color DesignationColor = new(1f, 0.85f, 0.2f);
	static readonly Color ProgressColor = new(1f, 0.85f, 0.2f, 0.55f);
	static readonly Color SelectionColor = new(1f, 1f, 1f, 0.9f);
	static readonly Color SelectionFillColor = new(1f, 1f, 1f, 0.15f);
	static readonly Color GoblinColor = new(0.35f, 0.75f, 0.30f);
	static readonly Color FaintGoblinColor = new(0.35f, 0.75f, 0.30f, 0.35f);

	public Simulation? Sim { get; set; }
	public int ViewZ { get; set; }

	/// <summary>The cells a drag in progress has swept (corners in any order), outlined as it goes.</summary>
	public (Vector2I From, Vector2I To)? Selection { get; set; }

	public override void _Draw()
	{
		if (Sim is null)
			return;

		var s = Sim.State;
		for (var y = 0; y < s.Height; y++)
		for (var x = 0; x < s.Width; x++)
		{
			var tile = s.TileIndex(x, y, ViewZ);
			var kind = s.KindAt(tile);
			var color = kind switch
			{
				TileKind.Solid => DirtColor,
				TileKind.Stairs => StairsColor,
				_ => FloorColor,
			};
			var rect = new Rect2(x * CellSize, y * CellSize, CellSize, CellSize);
			DrawRect(rect, color);
			DrawRect(rect, GridColor, filled: false);

			// Stairs get rungs on every level they link, so both ends read as stairs.
			if (kind == TileKind.Stairs)
				for (var rung = 1; rung <= 3; rung++)
				{
					var ry = rect.Position.Y + rung * CellSize / 4f;
					DrawLine(new Vector2(rect.Position.X + 3, ry), new Vector2(rect.End.X - 3, ry), StairsMarkColor, 2);
				}

			if (s.DesignationAt(tile) == DesignationKind.None)
				continue;

			var progress = s.DigProgress[tile];
			if (progress > 0)
			{
				var fill = (float)progress / MaterialRules.DigTicks(s.MaterialAt(tile));
				var height = rect.Size.Y * fill;
				DrawRect(new Rect2(rect.Position.X, rect.End.Y - height, rect.Size.X, height), ProgressColor);
			}
			DrawRect(rect.Grow(-1), DesignationColor, filled: false, width: 2);
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

		if (Selection is { } sel)
		{
			var min = new Vector2I(Math.Min(sel.From.X, sel.To.X), Math.Min(sel.From.Y, sel.To.Y));
			var max = new Vector2I(Math.Max(sel.From.X, sel.To.X), Math.Max(sel.From.Y, sel.To.Y));
			var box = new Rect2(min.X * CellSize, min.Y * CellSize, (max.X - min.X + 1) * CellSize, (max.Y - min.Y + 1) * CellSize);
			DrawRect(box, SelectionFillColor);
			DrawRect(box, SelectionColor, filled: false, width: 2);
		}
	}
}
