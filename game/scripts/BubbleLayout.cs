using System;
using System.Collections.Generic;
using Godot;

namespace AThousandGoblinsDeep;

/// <summary>Where a chat bubble goes on the map, and how big it is. Pure geometry plus the font's measure; nothing here draws.</summary>
public static class BubbleLayout
{
	public const int FontSize = 10;
	public const int Pad = 3;
	public const int SymbolSize = 12;
	public const int Gap = 4;
	public const float MaxTextWidth = 120;

	/// <summary>The size of a bubble: its symbol, then its goblin's name over its line, which wraps within <see cref="MaxTextWidth"/>.</summary>
	public static Vector2 Measure(Bubble bubble)
	{
		var font = ThemeDB.FallbackFont;
		var name = font.GetStringSize(bubble.Name, HorizontalAlignment.Left, -1, FontSize);
		var line = font.GetMultilineStringSize(bubble.Line, HorizontalAlignment.Left, MaxTextWidth, FontSize);
		var text = new Vector2(Math.Min(MaxTextWidth, Math.Max(name.X, line.X)), name.Y + line.Y);
		return new Vector2(Pad + SymbolSize + Gap + (float)Math.Ceiling(text.X) + Pad, Math.Max(SymbolSize, (float)Math.Ceiling(text.Y)) + 2 * Pad);
	}

	/// <summary>
	/// The rectangle for a bubble of <paramref name="size"/> over the goblin's tile <paramref name="cell"/>, inside a map of <paramref name="map"/>
	/// pixels. It tries above the tile, then below, centred and then flush to either side, then beside it, then further out, and takes the first spot that covers
	/// no bubble already <paramref name="placed"/> and no goblin tile in <paramref name="goblinCells"/>; failing that, the first that covers
	/// no bubble and not its own tile; failing that, the first that does not cover its own tile.
	/// </summary>
	public static Rect2 Place(Rect2 cell, Vector2 size, Vector2 map, IReadOnlyList<Rect2> placed, IReadOnlyList<Rect2> goblinCells)
	{
		var candidates = new List<Rect2>(26);
		var xs = new[] { cell.GetCenter().X - size.X / 2, cell.Position.X, cell.End.X - size.X };
		for (var ring = 0; ring < 4; ring++)
		{
			var rise = ring * (size.Y + 2);
			foreach (var above in new[] { true, false })
			foreach (var x in xs)
			{
				var y = above ? cell.Position.Y - Gap - size.Y - rise : cell.End.Y + Gap + rise;
				candidates.Add(Clamp(new Rect2(x, y, size), map));
			}
			// Beside the tile, level with it, when above and below are taken.
			if (ring == 0)
			{
				var y = cell.GetCenter().Y - size.Y / 2;
				candidates.Add(Clamp(new Rect2(cell.End.X + Gap, y, size), map));
				candidates.Add(Clamp(new Rect2(cell.Position.X - Gap - size.X, y, size), map));
			}
		}

		foreach (var strict in new[] { 2, 1, 0 })
		foreach (var candidate in candidates)
		{
			if (candidate.Intersects(cell))
				continue;
			if (strict >= 1 && Hits(candidate, placed))
				continue;
			if (strict >= 2 && Hits(candidate, goblinCells))
				continue;
			return candidate;
		}
		return candidates[0];
	}

	static bool Hits(Rect2 rect, IReadOnlyList<Rect2> others)
	{
		foreach (var other in others)
			if (rect.Intersects(other))
				return true;
		return false;
	}

	// Whole pixels, so the text and symbol draw crisp, and a pixel in from the map's edge so the rim shows.
	static Rect2 Clamp(Rect2 rect, Vector2 map) => new(
		(float)Math.Floor(Math.Clamp(rect.Position.X, 1, Math.Max(1, map.X - rect.Size.X - 1))),
		(float)Math.Floor(Math.Clamp(rect.Position.Y, 1, Math.Max(1, map.Y - rect.Size.Y - 1))),
		rect.Size);
}
