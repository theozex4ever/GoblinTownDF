using System;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>Draws one z-level of the warren as grey boxes. Reads sim state; never writes it.</summary>
public partial class WarrenView : Node2D
{
	public const int CellSize = 18;

	public static readonly Color FloorColor = new(0.62f, 0.62f, 0.64f);
	static readonly Color DirtColor = new(0.30f, 0.26f, 0.22f);
	static readonly Color StoneColor = new(0.36f, 0.40f, 0.48f);
	static readonly Color GridColor = new(0f, 0f, 0f, 0.15f);
	public static readonly Color StairsColor = new(0.78f, 0.74f, 0.58f);
	static readonly Color StairsMarkColor = new(0.25f, 0.22f, 0.15f);
	public static readonly Color DesignationColor = new(1f, 0.85f, 0.2f);
	public static readonly Color UnreachableColor = new(0.55f, 0.55f, 0.55f);
	static readonly Color ProgressColor = new(1f, 0.85f, 0.2f, 0.55f);
	// A right-drag unmakes, so its box is red whatever the Tool.
	public static readonly Color RemovalColor = new(0.92f, 0.36f, 0.28f);
	static readonly Color HoverColor = new(1f, 1f, 1f, 0.85f);
	// A dark rim around each goblin on the viewed level, so green bodies stand out on grey floor and green Piles.
	public static readonly Color GoblinOutlineColor = new(0.08f, 0.10f, 0.06f, 0.85f);
	public static readonly Color PausedColor = new(0.95f, 0.72f, 0.25f);
	public const int PausedFontSize = 13;
	public static readonly Color GoblinColor = new(0.35f, 0.75f, 0.30f);
	public static readonly Color FaintGoblinColor = new(0.35f, 0.75f, 0.30f, 0.35f);
	// A Starving goblin's body, tinted from healthy green to a sickly yellow.
	public static readonly Color StarvingGoblinColor = new(0.78f, 0.72f, 0.22f);
	// A shirking goblin wears a lilac ring and a lilac band across its body.
	public static readonly Color ShirkColor = new(0.80f, 0.45f, 0.95f);
	public const int PipSize = 4;
	static readonly Color LooseDirtColor = new(0.55f, 0.36f, 0.18f);
	static readonly Color GrubColor = new(0.93f, 0.52f, 0.48f);
	static readonly Color FungusColor = new(0.64f, 0.42f, 0.82f);
	static readonly Color GrogColor = new(0.18f, 0.72f, 0.74f);
	public static readonly Color WaitingHaulColor = new(0.55f, 0.55f, 0.55f);
	public static readonly Color PileTint = new(0.70f, 0.92f, 0.55f, 0.6f);
	public static readonly Color GrubPitTint = new(0.82f, 0.60f, 0.28f, 0.6f);
	public static readonly Color FungusFarmTint = new(0.30f, 0.58f, 0.42f, 0.6f);
	public static readonly Color BreweryTint = new(0.75f, 0.32f, 0.28f, 0.6f);
	// A Brewery whose Brews wait, because no Pile in reach has room for Grog.
	public static readonly Color WaitingBreweryTint = new(0.55f, 0.55f, 0.55f, 0.6f);
	public static readonly Color SleepingHeapTint = new(0.42f, 0.48f, 0.90f, 0.6f);
	static readonly Color SleepColor = new(0.45f, 0.52f, 0.95f);
	// The "zzz" over a sleeping goblin.
	public static readonly Color ZzzColor = new(0.92f, 0.96f, 1f);
	public const int ZzzFontSize = 9;
	// A material whose count has reached the Pile's capacity is drawn dark on a bright chip.
	public static readonly Color FullCountChipColor = new(1f, 0.85f, 0.2f);
	static readonly Color FullCountTextColor = new(0.12f, 0.10f, 0.06f);
	public const int CountFontSize = 9;
	public const int CarriedSize = 6;

	public Simulation? Sim { get; set; }
	public int ViewZ { get; set; }

	/// <summary>The cells a drag in progress has swept (corners in any order), outlined as it goes.</summary>
	public (Vector2I From, Vector2I To)? Selection { get; set; }

	/// <summary>The colour of the drag box: what the Tool makes, or <see cref="RemovalColor"/> for a right-drag.</summary>
	public Color SelectionColor { get; set; } = Colors.White;

	/// <summary>The map cell under the cursor, outlined; null with the cursor off the map.</summary>
	public Vector2I? Hover { get; set; }

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

			// A Room tile is tinted. A Pile tile shows its two best stored counts, each in its material's colour: a full count first,
			// then the larger. A count at the Pile's capacity sits on a bright chip. A loose item is a small square in the
			// same colour (stone matches solid stone), greyed while its Haul waits for a Pile with room for its material
			// that it can be carried to.
			if (s.IsPile(tile))
			{
				DrawRect(rect, PileTint);
				PileCounts.Pick(s, tile, out var first, out var second);
				DrawPileCount(s, tile, first, 0, rect);
				DrawPileCount(s, tile, second, 1, rect);
			}
			else
			{
				if (s.RoomAt(tile) == RoomKind.GrubPit)
					DrawRect(rect, GrubPitTint);
				else if (s.RoomAt(tile) == RoomKind.FungusFarm)
					DrawRect(rect, FungusFarmTint);
				else if (s.RoomAt(tile) == RoomKind.Brewery)
					DrawRect(rect, BrewSystem.IsWaiting(s, tile) ? WaitingBreweryTint : BreweryTint);
				else if (s.RoomAt(tile) == RoomKind.SleepingHeap)
					DrawRect(rect, SleepingHeapTint);
				for (var material = Goblins.Sim.Material.Dirt; (int)material <= SimState.CountedMaterials; material++)
					if (s.LooseCount(tile, material) > 0)
						DrawRect(LooseSquare(rect, (int)material - 1), s.IsHaulReachable(tile, material) ? MaterialColor(material) : WaitingHaulColor);
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
			if (dz == 0 && (GoblinTask)s.GoblinTask[g] == GoblinTask.Shirking)
				DrawRect(MarkerRect(s, g, dz).Grow(2), ShirkColor);
			if (dz == 0)
				DrawRect(MarkerRect(s, g, dz).Grow(1), GoblinOutlineColor);
			if (Math.Abs(dz) <= 1)
				DrawRect(MarkerRect(s, g, dz), dz == 0 ? BodyColor(s, g) : FaintGoblinColor);
		}

		// A shirker carries nothing, so its band has the body to itself; the pip and a hovering cursor still draw over it.
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.GoblinZ[g] == ViewZ && (GoblinTask)s.GoblinTask[g] == GoblinTask.Shirking)
				DrawRect(ShirkBandRect(MarkerRect(s, g, 0)), ShirkColor);

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
			DrawRect(CarriedRect(MarkerRect(s, g, 0), slot, carriers), MaterialColor(carried));
		}

		// A sleeping goblin carries nothing, so its "zzz" has the body to itself.
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.GoblinZ[g] == ViewZ && (GoblinTask)s.GoblinTask[g] == GoblinTask.Sleeping)
				DrawString(ThemeDB.FallbackFont, ZzzBaseline(MarkerRect(s, g, 0)), "zzz", HorizontalAlignment.Left, -1, ZzzFontSize, ZzzColor);

		// A craving goblin wears a pip in the colour of its Need, over the body, anything it carries and its "zzz". With
		// Cravings for several Needs it wears the one it would act on first.
		for (var g = 0; g < s.GoblinCount; g++)
		{
			var craving = NeedRules.LeadingCraving(s.GoblinGrub[g], s.GoblinGrog[g], s.GoblinSleep[g]);
			if (s.GoblinZ[g] == ViewZ && craving != Need.None)
				DrawRect(PipRect(MarkerRect(s, g, 0)), NeedColor(craving));
		}

		if (Selection is { } sel)
		{
			var min = new Vector2I(Math.Min(sel.From.X, sel.To.X), Math.Min(sel.From.Y, sel.To.Y));
			var max = new Vector2I(Math.Max(sel.From.X, sel.To.X), Math.Max(sel.From.Y, sel.To.Y));
			var box = new Rect2(min.X * CellSize, min.Y * CellSize, (max.X - min.X + 1) * CellSize, (max.Y - min.Y + 1) * CellSize);
			DrawRect(box, new Color(SelectionColor, 0.3f));
			DrawRect(box, SelectionColor, filled: false, width: 2);
		}

		if (Hover is { } hover)
			DrawRect(new Rect2(hover.X * CellSize, hover.Y * CellSize, CellSize, CellSize), HoverColor, filled: false, width: 1);

		// Paused, the map wears a frame and a label just outside its edge, so nothing on the map itself is covered.
		if (s.Paused)
		{
			var map = new Rect2(0, 0, s.Width * CellSize, s.Height * CellSize);
			DrawRect(map.Grow(2), PausedColor, filled: false, width: 2);
			DrawString(ThemeDB.FallbackFont, new Vector2(0, -6), "PAUSED · the warren holds its breath", HorizontalAlignment.Left, -1, PausedFontSize, PausedColor);
		}
	}

	/// <summary>A goblin's body colour: green, or tinted while it is Starving.</summary>
	public static Color BodyColor(SimState s, int goblin) => NeedRules.IsStarving(s.GoblinGrub[goblin]) ? StarvingGoblinColor : GoblinColor;

	/// <summary>The pip of a craving goblin, in the top right corner of its <paramref name="marker"/>, half outside it.</summary>
	public static Rect2 PipRect(Rect2 marker) => new(marker.End.X - PipSize + 1, marker.Position.Y - 1, PipSize, PipSize);

	/// <summary>The band across the middle of a shirking goblin's <paramref name="marker"/>: its full width and 3 px tall, clear of the pip in the top right corner.</summary>
	public static Rect2 ShirkBandRect(Rect2 marker) => new(marker.Position.X, marker.Position.Y + (int)marker.Size.Y / 2 - 1, marker.Size.X, 3);

	/// <summary>Where the "zzz" of a sleeping goblin starts: along the lower part of its <paramref name="marker"/>, clear of the pip in the top right corner.</summary>
	public static Vector2 ZzzBaseline(Rect2 marker) => marker.Position + new Vector2(0, ZzzFontSize + 2);

	/// <summary>The colour of a Need's pip: its material's colour for Grub and Grog.</summary>
	public static Color NeedColor(Need need) => need switch
	{
		Need.Grub => GrubColor,
		Need.Grog => GrogColor,
		Need.Sleep => SleepColor,
		_ => throw new ArgumentOutOfRangeException(nameof(need), need, "No colour for this Need."),
	};

	/// <summary>The colour of a material's loose squares, its Pile counts and the square on a carrying goblin.</summary>
	public static Color MaterialColor(Goblins.Sim.Material material) => material switch
	{
		Goblins.Sim.Material.Dirt => LooseDirtColor,
		Goblins.Sim.Material.Stone => StoneColor,
		Goblins.Sim.Material.Grub => GrubColor,
		Goblins.Sim.Material.Fungus => FungusColor,
		Goblins.Sim.Material.Grog => GrogColor,
		_ => throw new ArgumentOutOfRangeException(nameof(material), material, "No colour for this material."),
	};

	public const int LooseSize = 6;

	/// <summary>
	/// The loose square of the material in <paramref name="slot"/> (its number less one), in a tile's <paramref name="rect"/>:
	/// a grid of squares filling the tile, three to a row, in Material order. Nine materials fit before it needs a new layout.
	/// </summary>
	public static Rect2 LooseSquare(Rect2 rect, int slot)
	{
		var columns = CellSize / LooseSize;
		return new Rect2(rect.Position.X + slot % columns * LooseSize, rect.Position.Y + slot / columns * LooseSize, LooseSize, LooseSize);
	}

	/// <summary>Where the count in <paramref name="slot"/> (0 or 1) starts on a Pile tile.</summary>
	public static Vector2 CountBaseline(Rect2 rect, int slot) => rect.Position + new Vector2(2, 9 + slot * (CellSize - 11));

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

	/// <summary>The chip behind a full count whose text starts at <paramref name="baseline"/>: one pixel of margin each side, and the glyph height above the baseline.</summary>
	public static Rect2 CountChipRect(Vector2 baseline, float textWidth) =>
		new(baseline.X - 1, baseline.Y - (CountFontSize - 2), textWidth + 2, CountFontSize - 2);

	// No material in the slot draws nothing, so an empty Pile is only its tint.
	void DrawPileCount(SimState s, int tile, Goblins.Sim.Material material, int slot, Rect2 rect)
	{
		if (material != Goblins.Sim.Material.None)
			DrawCount(s.StoredCount(tile, material), CountBaseline(rect, slot), MaterialColor(material));
	}

	// A count at the Pile's capacity is highlighted.
	void DrawCount(int count, Vector2 baseline, Color color)
	{
		if (count <= 0)
			return;

		var label = CountLabel.Format(count);
		if (count >= SimState.PileCapacity)
		{
			var width = ThemeDB.FallbackFont.GetStringSize(label, HorizontalAlignment.Left, -1, CountFontSize).X;
			DrawRect(CountChipRect(baseline, width), FullCountChipColor);
			color = FullCountTextColor;
		}
		DrawString(ThemeDB.FallbackFont, baseline, label, HorizontalAlignment.Left, -1, CountFontSize, color);
	}
}
