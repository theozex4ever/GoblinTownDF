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
	static readonly Color StoneColor = new(0.36f, 0.40f, 0.48f);
	static readonly Color GridColor = new(0f, 0f, 0f, 0.15f);
	static readonly Color StairsColor = new(0.78f, 0.74f, 0.58f);
	static readonly Color StairsMarkColor = new(0.25f, 0.22f, 0.15f);
	static readonly Color DesignationColor = new(1f, 0.85f, 0.2f);
	static readonly Color UnreachableColor = new(0.55f, 0.55f, 0.55f);
	static readonly Color ProgressColor = new(1f, 0.85f, 0.2f, 0.55f);
	static readonly Color SelectionColor = new(1f, 1f, 1f, 0.9f);
	static readonly Color SelectionFillColor = new(1f, 1f, 1f, 0.15f);
	static readonly Color GoblinColor = new(0.35f, 0.75f, 0.30f);
	static readonly Color FaintGoblinColor = new(0.35f, 0.75f, 0.30f, 0.35f);
	static readonly Color LooseDirtColor = new(0.55f, 0.36f, 0.18f);
	static readonly Color WaitingHaulColor = new(0.55f, 0.55f, 0.55f);
	static readonly Color PileTint = new(0.70f, 0.92f, 0.55f, 0.6f);
	public const int CountFontSize = 9;
	public const int CarriedSize = 6;

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
				TileKind.Solid => s.MaterialAt(tile) == Goblins.Sim.Material.Stone ? StoneColor : DirtColor,
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

			// A Pile tile is tinted and shows its stored count per material, in that material's colour (dirt top
			// left, stone bottom right). A loose item is a small square in the same colour (stone matches solid stone),
			// greyed while its Haul waits for a Pile it can be carried to.
			if (s.IsPile(tile))
			{
				DrawRect(rect, PileTint);
				DrawCount(s.StoredCount(tile, Goblins.Sim.Material.Dirt), rect.Position + new Vector2(2, 9), LooseDirtColor);
				DrawCount(s.StoredCount(tile, Goblins.Sim.Material.Stone), rect.Position + new Vector2(2, CellSize - 2), StoneColor);
			}
			else
			{
				var waiting = !s.IsHaulReachable(tile);
				if (s.LooseCount(tile, Goblins.Sim.Material.Dirt) > 0)
					DrawRect(new Rect2(rect.Position.X + 3, rect.Position.Y + 3, 6, 6), waiting ? WaitingHaulColor : LooseDirtColor);
				if (s.LooseCount(tile, Goblins.Sim.Material.Stone) > 0)
					DrawRect(new Rect2(rect.End.X - 9, rect.End.Y - 9, 6, 6), waiting ? WaitingHaulColor : StoneColor);
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
			// A Designation no goblin can reach yet waits greyed out, and lights up when a dug tile opens a path.
			DrawRect(rect.Grow(-1), s.IsReachable(tile) ? DesignationColor : UnreachableColor, filled: false, width: 2);
		}

		// Every body first, then every carried square, so a goblin standing on a carrier's tile cannot cover it.
		for (var g = 0; g < s.GoblinCount; g++)
		{
			var dz = s.GoblinZ[g] - ViewZ;
			if (Math.Abs(dz) <= 1)
				DrawRect(MarkerRect(s, g, dz), dz == 0 ? GoblinColor : FaintGoblinColor);
		}

		for (var g = 0; g < s.GoblinCount; g++)
		{
			var carried = (Goblins.Sim.Material)s.GoblinCarry[g];
			if (s.GoblinZ[g] != ViewZ || carried == Goblins.Sim.Material.None)
				continue;

			// Carriers on one tile share its marker, a grid slot each in goblin order.
			var slot = 0;
			var carriers = 1;
			for (var other = 0; other < s.GoblinCount; other++)
			{
				if (other == g || s.GoblinCarry[other] == (byte)Goblins.Sim.Material.None
					|| s.GoblinX[other] != s.GoblinX[g] || s.GoblinY[other] != s.GoblinY[g] || s.GoblinZ[other] != s.GoblinZ[g])
					continue;
				carriers++;
				if (other < g)
					slot++;
			}
			DrawRect(CarriedRect(MarkerRect(s, g, 0), slot, carriers), carried == Goblins.Sim.Material.Stone ? StoneColor : LooseDirtColor);
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

	static Rect2 MarkerRect(SimState s, int goblin, int dz)
	{
		var inset = dz == 0 ? 3 : 6;
		return new Rect2(
			s.GoblinX[goblin] * CellSize + inset,
			s.GoblinY[goblin] * CellSize + inset,
			CellSize - 2 * inset,
			CellSize - 2 * inset);
	}

	/// <summary>
	/// The small square of material for carrier number <paramref name="slot"/> of <paramref name="carriers"/> on one tile.
	/// The squares form a centred grid of whole pixels inside the goblin <paramref name="marker"/>, one pixel apart: a
	/// lone carrier gets one <see cref="CarriedSize"/> square, and the squares shrink as more carriers share the tile,
	/// so any number up to the goblin cap fits and none overlap.
	/// </summary>
	public static Rect2 CarriedRect(Rect2 marker, int slot, int carriers)
	{
		var side = (int)Math.Min(marker.Size.X, marker.Size.Y);
		var columns = (int)Math.Ceiling(Math.Sqrt(carriers));
		var rows = (carriers + columns - 1) / columns;
		var size = Math.Max(1, Math.Min(CarriedSize, (side - (columns - 1)) / columns));
		var left = marker.Position.X + ((int)marker.Size.X - (columns * size + columns - 1)) / 2;
		var top = marker.Position.Y + ((int)marker.Size.Y - (rows * size + rows - 1)) / 2;
		return new Rect2(left + slot % columns * (size + 1), top + slot / columns * (size + 1), size, size);
	}

	// A zero count draws nothing, so an empty Pile is only its tint.
	void DrawCount(int count, Vector2 baseline, Color color)
	{
		if (count > 0)
			DrawString(ThemeDB.FallbackFont, baseline, CountLabel.Format(count), HorizontalAlignment.Left, -1, CountFontSize, color);
	}
}
