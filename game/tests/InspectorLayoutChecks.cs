using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>
/// Lays the real Main scene out in a viewport of a stated size and drives the Inspector through viewport input events, over several settled
/// frames: seven goblins on one Heap must all be readable and reachable in the Inspector, without the panel stretching or moving the map, and
/// the inspected tile must survive the pointer travelling from the map to the Inspector to scroll it. Geometry runs under --headless too; the
/// screenshots need a renderer and are written only when GOBLINTOWN_CAPTURE_DIR is set and there is one.
/// Every failure is collected and reported together, so a red run says everything that is wrong at once.
/// </summary>
public partial class InspectorLayoutChecks : Node
{
	const int FramesToSettle = 4;
	const int Goblins = 7;
	// The Heap in the middle of the map, where the pointer rests on it; and one on the map's east edge, which the pointer can leave straight for the Inspector.
	static readonly Vector2I Heap = new(21, 12);
	static readonly Vector2I EdgeHeap = new(31, 12);

	readonly List<string> _failures = [];
	readonly Dictionary<string, object> _report = [];
	string? _captureDir;
	SubViewport _viewport = null!;
	Main _main = null!;
	WarrenView _view = null!;
	string _label = "";
	// The tile the seven goblins are on just now.
	Vector2I _tile = Heap;

	public override async void _Ready()
	{
		try
		{
			_captureDir = OS.GetEnvironment("GOBLINTOWN_CAPTURE_DIR");
			if (_captureDir == "")
				_captureDir = null;
			if (_captureDir is not null)
				System.IO.Directory.CreateDirectory(_captureDir);

			// The default window, then the smallest this layout supports: the map's 576 px plus both panels is the narrowest and its height the shortest.
			foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1152, 660) })
				await SevenGoblinsOnOneHeap(size);

			if (_captureDir is not null)
				System.IO.File.WriteAllText(System.IO.Path.Combine(_captureDir, "measurements.json"),
					JsonSerializer.Serialize(_report, new JsonSerializerOptions { WriteIndented = true }));
			if (_failures.Count > 0)
			{
				foreach (var failure in _failures)
					GD.PrintErr(failure);
				GD.Print($"Inspector layout checks FAILED: {_failures.Count} failure(s).");
				GetTree().Quit(1);
				return;
			}
			var renderer = DisplayServer.GetName() == "headless" ? "no renderer: screenshots skipped" : _captureDir is null ? "screenshots not requested" : "screenshots written";
			GD.Print($"Inspector layout checks passed (2 viewports, seven goblins on one Heap; {renderer}).");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	async Task SevenGoblinsOnOneHeap(Vector2I size)
	{
		_label = $"{size.X}x{size.Y}";
		_tile = Heap;
		var report = new Dictionary<string, object> { ["viewport"] = new[] { size.X, size.Y } };
		_report[_label] = report;

		_viewport = new SubViewport { Size = size, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
		AddChild(_viewport);
		_main = new Main();
		_viewport.AddChild(_main);
		try
		{
			await Frames(FramesToSettle);
			_view = _main.View;
			var sim = _view.Sim!;
			var s = sim.State;
			sim.Apply(new SetPaused(true));

			// Nothing inspected yet: the layout every later frame must keep.
			Motion(new Vector2(4, 4));
			await Frames(FramesToSettle);
			var panel = Find<InspectorPanel>(_main) ?? throw new InvalidOperationException("No InspectorPanel in Main.");
			var column = Ancestor<PanelContainer>(panel) ?? throw new InvalidOperationException("The Inspector is in no panel.");
			var scroll = _main.FindChild("InspectorScroll", recursive: true, owned: false) as ScrollContainer;
			var origin0 = _view.GlobalPosition;
			var column0 = column.GetGlobalRect();
			var scroll0 = scroll?.GetGlobalRect();
			report["map origin, nothing inspected"] = Pair(origin0);
			report["right panel, nothing inspected"] = RectOf(column0);
			report["inspector scroll, nothing inspected"] = scroll0 is { } r0 ? RectOf(r0) : "none";

			// Seven goblins asleep on one Heap tile, each with its own distinct Needs and Chaos.
			sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 0, Heap.X, Heap.Y, Heap.X, Heap.Y));
			for (var g = 0; g < Goblins; g++)
			{
				(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]) = (Heap.X, Heap.Y, 0);
				GoblinTasks.BeginSleep(s, g, dropTile: -1, asleep: true);
				GoblinTasks.StartSleeping(s, g);
				(s.GoblinGrub[g], s.GoblinGrog[g], s.GoblinSleep[g], s.GoblinChaos[g]) = ((byte)(95 - 11 * g), (byte)(80 - 9 * g), (byte)(5 + 3 * g), (byte)(14 * g));
			}

			// The pointer rests on the Heap. Every settled frame keeps the map, the right panel and the inspected tile where they were.
			var onHeap = CellCentre(Heap.X, Heap.Y);
			Motion(onHeap);
			for (var frame = 0; frame < 2 * FramesToSettle; frame++)
			{
				await Frames(1);
				Expect(_view.GlobalPosition == origin0, $"frame {frame}: the map moved from {origin0} to {_view.GlobalPosition} when seven goblins were inspected.");
				Expect(column.GetGlobalRect() == column0, $"frame {frame}: the right panel went from {column0} to {column.GetGlobalRect()} when seven goblins were inspected.");
				Expect(_main.Inspection.Goblins.Count == Goblins, $"frame {frame}: {_main.Inspection.Goblins.Count} goblins are inspected, not {Goblins}.");
				Expect(_view.Hover == Heap, $"frame {frame}: the map outlines {_view.Hover}, not the Heap {Heap}.");
				if (frame < 2)
					await Capture($"1-settling-frame-{frame}");
			}
			var right = column.GetGlobalRect();
			Expect(right.End.Y <= size.Y + 0.5f && right.Position.Y >= -0.5f, $"The right panel {right} leaves the {size.X}x{size.Y} viewport.");
			report["right panel, seven goblins"] = RectOf(right);
			report["map origin, seven goblins"] = Pair(_view.GlobalPosition);
			report["last entry bottom, scrolled to top"] = panel.Entry(Goblins - 1).GetGlobalRect().End.Y;
			await Capture("1-seven-on-heap-top");

			// Every entry is the right goblin, in index order, with the values the sim holds.
			ExpectEntries(s, panel);

			// Without a bounded, scrolling Inspector nothing below here can be reached.
			if (scroll is null)
			{
				Expect(false, "The Inspector is not in a scroll area named InspectorScroll, so entries past the viewport are unreachable.");
				return;
			}
			ExpectBounded(scroll, column, size);
			// Mid-map, the pointer stays on the Heap and the wheel there scrolls the Inspector.
			await ExpectEveryEntryReachableByWheel(panel, scroll, report, "wheel over the tile", CellCentre(Heap.X, Heap.Y));
			Expect(_view.GlobalPosition == origin0 && column.GetGlobalRect() == column0, "Scrolling over the tile moved the map or the panel.");

			// The crowd on the map's east edge: the pointer can leave straight for the Inspector, which holds the tile.
			MoveCrowd(s, sim, EdgeHeap);
			await ExpectPointerRoute(panel, scroll, report);
			await ExpectEveryEntryReachableByWheel(panel, scroll, report, "wheel over the Inspector", scroll.GetGlobalRect().GetCenter());
			await ExpectScrollbarDrag(panel, scroll, report);
			await ExpectNoPaintThroughPanel(scroll);
			await ExpectReturnToMap(panel, scroll);
		}
		finally
		{
			_viewport.RemoveChild(_main);
			_main.Free();
			RemoveChild(_viewport);
			_viewport.Free();
		}
	}

	// The scroll area lies inside the right panel and inside the viewport, and the panel is no taller than it was with nothing inspected.
	void ExpectBounded(ScrollContainer scroll, PanelContainer column, Vector2I size)
	{
		var rect = scroll.GetGlobalRect();
		Expect(column.GetGlobalRect().Encloses(rect), $"The scroll area {rect} leaves the right panel {column.GetGlobalRect()}.");
		Expect(rect.Position.Y >= 0 && rect.End.Y <= size.Y + 0.5f, $"The scroll area {rect} leaves the viewport.");
		Expect(rect.Size.Y >= 120, $"The scroll area is only {rect.Size.Y} px tall.");
		Expect(scroll.GetVScrollBar().MaxValue > scroll.GetVScrollBar().Page, "Seven goblins fit the scroll area without scrolling; the check is vacuous.");
	}

	// Every goblin and a Heap under them go to one tile; the pointer is left where it was.
	void MoveCrowd(SimState s, Simulation sim, Vector2I to)
	{
		sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 0, to.X, to.Y, to.X, to.Y));
		for (var g = 0; g < Goblins; g++)
			(s.GoblinX[g], s.GoblinY[g]) = (to.X, to.Y);
		_tile = to;
	}

	void ExpectEntries(SimState s, InspectorPanel panel)
	{
		Expect(panel.EntryCount == Goblins, $"The Inspector draws {panel.EntryCount} entries, not {Goblins}.");
		for (var g = 0; g < Math.Min(panel.EntryCount, Goblins); g++)
		{
			var texts = Labels(panel.Entry(g));
			var expected = new[]
			{
				$"{GoblinNames.For(g)}, Grunt", Inspector.TaskWords(s, g),
				"Grub", s.GoblinGrub[g].ToString(), "Grog", s.GoblinGrog[g].ToString(), "Sleep", s.GoblinSleep[g].ToString(), "Chaos", s.GoblinChaos[g].ToString(),
			};
			Expect(texts.Take(expected.Length).SequenceEqual(expected), $"Entry {g} reads [{string.Join(" | ", texts)}], expected it to begin [{string.Join(" | ", expected)}].");
		}
		var tops = Enumerable.Range(0, Math.Min(panel.EntryCount, Goblins)).Select(g => panel.Entry(g).GetGlobalRect().Position.Y).ToList();
		Expect(tops.Zip(tops.Skip(1), (a, b) => a < b).All(ok => ok), "The entries are not laid out top to bottom in index order.");
	}

	// The mouse wheel over the Inspector brings each entry, whole, into the scroll area, and the last one at the bottom.
	async Task ExpectEveryEntryReachableByWheel(InspectorPanel panel, ScrollContainer scroll, Dictionary<string, object> report, string how, Vector2 over)
	{
		if (!scroll.GetGlobalRect().HasPoint(over))
			Motion(over);
		await Frames(2);
		Expect(_main.Inspection.Goblins.Count == Goblins && _view.Hover == _tile, $"The Inspector let go of the Heap at {_tile} with the pointer at {over} ({how}).");
		var bar = scroll.GetVScrollBar();
		report[$"{how}: scroll range"] = new { max = bar.MaxValue, page = bar.Page };
		var reached = new List<string>();
		for (var g = 0; g < Goblins; g++)
		{
			// Back to the top, then down a notch at a time until entry g sits whole in the visible part.
			for (var guard = 0; guard < 60 && scroll.ScrollVertical > 0; guard++)
				await Wheel(over, down: false);
			var notches = 0;
			while (!Visible(panel.Entry(g), scroll) && notches++ < 60)
				await Wheel(over, down: true);
			Expect(Visible(panel.Entry(g), scroll), $"({how}) Entry {g} never came wholly into the scroll area {VisibleRect(scroll)}; it sits at {panel.Entry(g).GetGlobalRect()}.");
			reached.Add($"entry {g} whole after {notches} wheel notches, at {RectOf(panel.Entry(g).GetGlobalRect())}");
			if (g == 0)
				await Capture($"2-first-entry-visible-{how.Replace(' ', '-')}");
			Expect(_main.Inspection.Goblins.Count == Goblins && _view.Hover == _tile, $"Scrolling to entry {g} changed the inspection.");
		}
		report[$"{how}: reach"] = reached;

		for (var guard = 0; guard < 80 && scroll.ScrollVertical < bar.MaxValue - bar.Page; guard++)
			await Wheel(over, down: true);
		Expect(Visible(panel.Entry(Goblins - 1), scroll), $"At the bottom the last entry {panel.Entry(Goblins - 1).GetGlobalRect()} is not whole in {VisibleRect(scroll)}.");
		Expect(panel.Entry(Goblins - 1).GetGlobalRect().Size.Y >= panel.Entry(Goblins - 1).GetCombinedMinimumSize().Y - 0.5f, "The last entry is drawn shorter than its content.");
		report[$"{how}: last entry at bottom"] = RectOf(panel.Entry(Goblins - 1).GetGlobalRect());
		report[$"{how}: scroll area at bottom"] = RectOf(VisibleRect(scroll));
		await Capture($"3-last-entry-at-bottom-{how.Replace(' ', '-')}");
		for (var guard = 0; guard < 80 && scroll.ScrollVertical > 0; guard++)
			await Wheel(over, down: false);
		Expect(Visible(panel.Entry(0), scroll), "Scrolling back up did not bring the first entry back.");
	}

	// The real route: the pointer comes along the Heap's row onto the Heap, leaves the map, crosses the gap between the map and the right
	// panel in many small motions, and enters the Inspector, which holds the Heap throughout.
	async Task ExpectPointerRoute(InspectorPanel panel, ScrollContainer scroll, Dictionary<string, object> report)
	{
		var held = await ReachInspectorFromTile(scroll);
		report["route steps held off the map"] = held;
		Expect(held > 0, "The route never left the map, so it tested nothing.");
		Expect(scroll.GetGlobalRect().HasPoint(_pointerAt), $"The route ended at {_pointerAt}, outside the Inspector {scroll.GetGlobalRect()}.");
		Expect(_main.Inspection.Goblins.Count == Goblins && _view.Hover == _tile, $"The pointer is in the Inspector, which shows \"{_main.Inspection.Title}\" and outlines {_view.Hover}, not the crowd on {_tile}.");
		await Capture("4-pointer-in-inspector");
	}

	// Onto the crowd's tile along its row, then off the map and on to the middle of the Inspector. Returns how many motions were off the map
	// and held the tile.
	async Task<int> ReachInspectorFromTile(ScrollContainer scroll)
	{
		var path = new List<Vector2> { CellCentre(_tile.X - 1, _tile.Y), CellCentre(_tile.X, _tile.Y) };
		var mapEnd = _view.GlobalPosition.X + _view.Sim!.State.Width * WarrenView.CellSize;
		path.Add(new Vector2(mapEnd + 4, CellCentre(_tile.X, _tile.Y).Y));
		path.Add(scroll.GetGlobalRect().GetCenter());
		var held = 0;
		for (var leg = 0; leg + 1 < path.Count; leg++)
		{
			const int steps = 16;
			for (var i = leg == 0 ? 0 : 1; i <= steps; i++)
			{
				var at = path[leg].Lerp(path[leg + 1], i / (float)steps);
				Motion(at);
				_pointerAt = at;
				if (i % 4 == 0)
					await Frames(1);
				var cell = new Vector2I(Mathf.FloorToInt((at.X - _view.GlobalPosition.X) / WarrenView.CellSize), Mathf.FloorToInt((at.Y - _view.GlobalPosition.Y) / WarrenView.CellSize));
				if (cell.X >= 0 && cell.Y >= 0 && cell.X < _view.Sim.State.Width && cell.Y < _view.Sim.State.Height)
					continue;
				Expect(_main.Inspection.Goblins.Count == Goblins && _view.Hover == _tile, $"Leg {leg}, step {i} at {at}: the Inspector lost {_tile} on the way to the panel (shows \"{_main.Inspection.Title}\").");
				held++;
			}
		}
		await Frames(2);
		return held;
	}

	Vector2 _pointerAt;

	// A real scrollbar drag: press on the grabber, drag to the bottom of the track, release.
	async Task ExpectScrollbarDrag(InspectorPanel panel, ScrollContainer scroll, Dictionary<string, object> report)
	{
		for (var guard = 0; guard < 80 && scroll.ScrollVertical > 0; guard++)
			await Wheel(scroll.GetGlobalRect().GetCenter(), down: false);
		var bar = scroll.GetVScrollBar();
		var track = bar.GetGlobalRect();
		var grab = new Vector2(track.GetCenter().X, track.Position.Y + 4);
		Motion(grab);
		await Frames(2);
		Expect(_main.Inspection.Goblins.Count == Goblins && _view.Hover == _tile, "The Inspector let go of the crowd when the pointer reached the scrollbar.");
		Press(grab, MouseButton.Left, true);
		Motion(new Vector2(grab.X, track.End.Y + 30), MouseButtonMask.Left);
		Press(new Vector2(grab.X, track.End.Y + 30), MouseButton.Left, false);
		await Frames(3);
		Expect(scroll.ScrollVertical >= bar.MaxValue - bar.Page - 1, $"Dragging the scrollbar to its end left the scroll at {scroll.ScrollVertical} of {bar.MaxValue - bar.Page}.");
		Expect(Visible(panel.Entry(Goblins - 1), scroll), "The scrollbar drag did not reach the last entry.");
		Expect(_main.Inspection.Goblins.Count == Goblins && _view.Hover == _tile, "Dragging the scrollbar changed the inspection.");
		report["scroll after scrollbar drag"] = scroll.ScrollVertical;
		await Capture("5-scrollbar-dragged-to-end");
	}

	// A press and a drag that begin in the Inspector neither paint nor designate on the map, with a Tool picked.
	async Task ExpectNoPaintThroughPanel(ScrollContainer scroll)
	{
		var s = _view.Sim!.State;
		PushKey(Godot.Key.D);
		var inside = scroll.GetGlobalRect().GetCenter();
		Press(inside, MouseButton.Left, true);
		Motion(CellCentre(3, 3), MouseButtonMask.Left);
		Press(CellCentre(8, 8), MouseButton.Left, false);
		await Frames(2);
		var marked = Enumerable.Range(0, s.Designations.Length).Count(t => s.Designations[t] != 0);
		Expect(marked == 0, $"A press that began in the Inspector marked {marked} tiles.");
		Expect(_view.Selection is null, "A press that began in the Inspector left a selection on the map.");
		PushKey(Godot.Key.Escape);
		await Frames(1);
	}

	// Back on the map the Inspector follows the pointer again: another tile, a Pile, the crowd afresh from the top; and off the map it empties.
	async Task ExpectReturnToMap(InspectorPanel panel, ScrollContainer scroll)
	{
		var sim = _view.Sim!;
		// Into the Inspector again and scrolled a notch, so the reset on a new tile has something to reset.
		await ReachInspectorFromTile(scroll);
		await Wheel(scroll.GetGlobalRect().GetCenter(), down: true);
		Expect(scroll.ScrollVertical > 0, "The wheel in the Inspector did not scroll it.");

		var floor = new Vector2I(_tile.X - 3, _tile.Y + 4);
		MoveAlong(scroll.GetGlobalRect().GetCenter(), CellCentre(floor.X, floor.Y));
		await Frames(2);
		Expect(_main.Inspection.Title == "Floor" && _main.Inspection.Goblins.Count == 0, $"Back on the map, a floor tile shows \"{_main.Inspection.Title}\" with {_main.Inspection.Goblins.Count} goblins.");
		Expect(_view.Hover == floor, $"Back on the map the outline is on {_view.Hover}, not {floor}.");

		sim.Apply(new PaintPile(0, floor.X, floor.Y, floor.X, floor.Y));
		Motion(CellCentre(floor.X, floor.Y));
		await Frames(1);
		Expect(_main.Inspection.Title == "Pile", $"A Pile tile shows \"{_main.Inspection.Title}\".");

		MoveAlong(CellCentre(floor.X, floor.Y), CellCentre(_tile.X, _tile.Y));
		await Frames(2);
		Expect(_main.Inspection.Goblins.Count == Goblins && scroll.ScrollVertical == 0, $"Returning to the crowd shows {_main.Inspection.Goblins.Count} goblins at scroll {scroll.ScrollVertical}, not seven from the top.");

		// Leaving the map by any other way than the Inspector clears the inspection.
		Motion(new Vector2(60, 300));
		await Frames(1);
		Expect(_main.Inspection.Title == "" && _view.Hover is null, $"Over the left panel the Inspector still shows \"{_main.Inspection.Title}\".");

		// Entering the Inspector without coming from a map tile inspects nothing.
		Motion(scroll.GetGlobalRect().GetCenter());
		await Frames(1);
		Expect(_main.Inspection.Title == "" && _view.Hover is null, $"Entering the Inspector cold shows \"{_main.Inspection.Title}\".");

		// And from the Heap off to the top bar, the Inspector lets go.
		Motion(CellCentre(_tile.X, _tile.Y));
		Motion(new Vector2(CellCentre(_tile.X, _tile.Y).X, 12));
		await Frames(1);
		Expect(_main.Inspection.Title == "" && _view.Hover is null, $"Over the top bar the Inspector still shows \"{_main.Inspection.Title}\".");
		await Capture("5-back-on-map");
	}

	void MoveAlong(Vector2 from, Vector2 to)
	{
		for (var i = 0; i <= 40; i++)
			Motion(from.Lerp(to, i / 40f));
	}

	// ── events: viewport-local, through the viewport's GUI and input routes, as the engine would deliver them ──

	void Motion(Vector2 at, MouseButtonMask mask = 0)
	{
		using var input = new InputEventMouseMotion { Position = at, GlobalPosition = at, ButtonMask = mask };
		_viewport.PushInput(input, inLocalCoords: true);
	}

	void Press(Vector2 at, MouseButton button, bool pressed)
	{
		using var input = new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = at, GlobalPosition = at, ButtonMask = pressed ? MouseButtonMask.Left : 0 };
		_viewport.PushInput(input, inLocalCoords: true);
	}

	async Task Wheel(Vector2 at, bool down)
	{
		var button = down ? MouseButton.WheelDown : MouseButton.WheelUp;
		using (var press = new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = at, GlobalPosition = at, Factor = 1 })
			_viewport.PushInput(press, inLocalCoords: true);
		using (var release = new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = at, GlobalPosition = at })
			_viewport.PushInput(release, inLocalCoords: true);
		await Frames(2);
	}

	void PushKey(Godot.Key key)
	{
		using var input = new InputEventKey { Keycode = key, Pressed = true };
		_viewport.PushInput(input, inLocalCoords: true);
	}

	Vector2 CellCentre(int x, int y) => _view.GlobalPosition + new Vector2((x + 0.5f) * WarrenView.CellSize, (y + 0.5f) * WarrenView.CellSize);

	async Task Frames(int count)
	{
		for (var i = 0; i < count; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	// ── geometry ──

	// The part of the scroll area that content shows in: its rectangle less the scrollbar.
	static Rect2 VisibleRect(ScrollContainer scroll)
	{
		var rect = scroll.GetGlobalRect();
		var bar = scroll.GetVScrollBar();
		return bar.Visible ? new Rect2(rect.Position, new Vector2(rect.Size.X - bar.Size.X, rect.Size.Y)) : rect;
	}

	static bool Visible(Control entry, ScrollContainer scroll) => VisibleRect(scroll).Grow(0.5f).Encloses(entry.GetGlobalRect());

	static List<string> Labels(Node node)
	{
		var texts = new List<string>();
		foreach (var child in node.GetChildren())
		{
			if (child is Label label)
				texts.Add(label.Text);
			texts.AddRange(Labels(child));
		}
		return texts;
	}

	static T? Find<T>(Node node) where T : class
	{
		foreach (var child in node.GetChildren())
			if (child is T found || (found = Find<T>(child)) is not null)
				return found;
		return null;
	}

	static T? Ancestor<T>(Node node) where T : class
	{
		for (var up = node.GetParent(); up is not null; up = up.GetParent())
			if (up is T found)
				return found;
		return null;
	}

	static float[] Pair(Vector2 v) => [v.X, v.Y];

	static float[] RectOf(Rect2 r) => [r.Position.X, r.Position.Y, r.Size.X, r.Size.Y];

	void Expect(bool condition, string message)
	{
		if (!condition)
			_failures.Add($"[{_label}] {message}");
	}

	// A screenshot of the viewport as rendered now, when asked for and there is a renderer to ask.
	async Task Capture(string name)
	{
		if (_captureDir is null || DisplayServer.GetName() == "headless")
			return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var image = _viewport.GetTexture().GetImage();
		image.SavePng(System.IO.Path.Combine(_captureDir, $"{_label}-{name}.png"));
	}
}
