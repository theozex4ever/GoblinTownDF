using System;
using Godot;

namespace AThousandGoblinsDeep;

/// <summary>
/// The shared selection gesture: press a mouse button on the map, drag, release, and it emits the
/// rectangle of cells swept. A click is a 1×1 rectangle. It knows nothing about tools, so Dig,
/// Dig stairs and cancel each give it a button and decide for themselves what the rectangle means.
/// </summary>
public sealed class SelectionGesture
{
	readonly MouseButton _button;
	readonly Func<Vector2, Vector2I> _cellAt;
	readonly Vector2I _mapSize;
	Vector2I _anchor;
	Vector2I _current;

	/// <param name="cellAt">Turns a screen position into the (unclipped) map cell under it.</param>
	public SelectionGesture(MouseButton button, Vector2I mapSize, Func<Vector2, Vector2I> cellAt)
	{
		_button = button;
		_mapSize = mapSize;
		_cellAt = cellAt;
	}

	/// <summary>Raised on release with the two corners of the rectangle, in the order they were dragged.</summary>
	public event Action<Vector2I, Vector2I>? Selected;

	/// <summary>Raised whenever the swept rectangle changes or the gesture ends, so a view can redraw its outline.</summary>
	public event Action? Changed;

	public bool Dragging { get; private set; }

	/// <summary>The rectangle swept so far, corners in any order; meaningful while <see cref="Dragging"/>.</summary>
	public (Vector2I From, Vector2I To) Rectangle => (_anchor, _current);

	/// <summary>Feeds an input event in. Returns true when the gesture used it.</summary>
	public bool Handle(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton { Pressed: true } press when press.ButtonIndex == _button:
				var cell = _cellAt(press.Position);
				if (cell.X < 0 || cell.Y < 0 || cell.X >= _mapSize.X || cell.Y >= _mapSize.Y)
					return false;
				_anchor = _current = cell;
				Dragging = true;
				Changed?.Invoke();
				return true;

			case InputEventMouseMotion motion when Dragging:
				var over = _cellAt(motion.Position);
				_current = new Vector2I(Math.Clamp(over.X, 0, _mapSize.X - 1), Math.Clamp(over.Y, 0, _mapSize.Y - 1));
				Changed?.Invoke();
				return true;

			case InputEventMouseButton { Pressed: false } release when Dragging && release.ButtonIndex == _button:
				var end = _cellAt(release.Position);
				_current = new Vector2I(Math.Clamp(end.X, 0, _mapSize.X - 1), Math.Clamp(end.Y, 0, _mapSize.Y - 1));
				var (from, to) = Rectangle;
				Dragging = false;
				Changed?.Invoke();
				Selected?.Invoke(from, to);
				return true;
		}
		return false;
	}

	/// <summary>Drops a drag in progress without emitting anything.</summary>
	public void Cancel()
	{
		if (!Dragging)
			return;
		Dragging = false;
		Changed?.Invoke();
	}
}
