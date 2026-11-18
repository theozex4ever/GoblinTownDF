using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>
/// Checks the provisional chat bubbles: how an incident becomes a bubble, how long it lives, how many there can be, where it is placed, and that
/// showing, hiding and timing them never touches the sim. Board and layout checks are pure; the Main checks drive the real scene's
/// <c>_Process</c> with a controlled monotonic clock; a separate real engine-stall check uses normal callbacks. Both run headless. Appearance is
/// checked in <c>CarryDrawChecks</c>, which needs a renderer.
/// </summary>
public partial class BubbleChecks : Node
{
	int _scenarios;
	sealed class FrameClock { public ulong Usec; }
	readonly Dictionary<Main, FrameClock> _clocks = [];

	public override async void _Ready()
	{
		try
		{
			IncidentsMakeBubbles();
			AuthoredCopy();
			LifetimeIsRealTime();
			OnePerGoblinThreeOverall();
			OffLevelIncidents();
			HiddenBubblesCountTowardTheCap();
			Placement();
			MainTimingAtOneAndFourTimesAndPaused();
			PauseInputBetweenFrames();
			LevelRoundTripKeepsBubbles();
			ExpiryOffLevel();
			HiddenBubblesPauseAndSpeed();
			BubbleFollowsGoblinOnStairs();
			RealIncidentsMakeBubbles();
			PresentationLeavesTheSimAlone();
			await MainExpiresAfterEngineStall();
			GD.Print($"Bubble checks passed ({_scenarios} scenarios).");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	static SimState NewState() => Simulation.NewWorld(seed: 1).State;

	// Each of the four incidents puts one bubble over the goblin it happened to, named as the goblin is named everywhere else.
	void IncidentsMakeBubbles()
	{
		_scenarios++;
		var s = NewState();
		var cases = new (SimEvent Incident, int Goblin, BubbleKind Kind)[]
		{
			(new ShirkStarted(2), 2, BubbleKind.Shirk),
			(new ScuffleStarted(Instigator: 4, Victim: 1), 4, BubbleKind.Scuffle),
			(new TheftPickedUp(5, Goblins.Sim.Material.Grog), 5, BubbleKind.Theft),
			(new HaulCancelled(3), 3, BubbleKind.LostHaul),
		};
		foreach (var (incident, goblin, kind) in cases)
		{
			var board = new BubbleBoard();
			board.Observe([incident], s);
			Require(board.Active.Count == 1, $"{incident} made {board.Active.Count} bubbles.");
			var bubble = board.Active[0];
			Require(bubble.Goblin == goblin && bubble.Kind == kind, $"{incident} made a {bubble.Kind} bubble over goblin {bubble.Goblin}.");
			Require(bubble.Name == GoblinNames.For(goblin), $"{incident}: the bubble names {bubble.Name}, not {GoblinNames.For(goblin)}.");
			Require(bubble.Line.Length > 0 && !bubble.Line.Contains('{'), $"{incident}: the line \"{bubble.Line}\" is empty or has an unfilled blank.");
			Require(Math.Abs(bubble.Remaining - BubbleBoard.LifetimeSeconds) < 1e-9, $"{incident}: a new bubble has {bubble.Remaining}s, not {BubbleBoard.LifetimeSeconds}.");
		}

		// A scuffle puts one bubble on the instigator, and its line may name the victim; a theft's may name the supply.
		var scuffle = new BubbleBoard();
		var theft = new BubbleBoard();
		for (var i = 0; i < 12; i++)
		{
			scuffle.Observe([new ScuffleStarted(0, 6)], s);
			theft.Observe([new TheftPickedUp(0, Goblins.Sim.Material.Grub)], s);
			Require(scuffle.Active.Count == 1 && scuffle.Active[0].Goblin == 0, "A scuffle made a bubble for someone but the instigator.");
		}

		// Events that are not one of the four make nothing.
		var quiet = new BubbleBoard();
		quiet.Observe([new PausedChanged(true), new SpeedChanged(4), new TileDug(1, 1, 0, Goblins.Sim.Material.Dirt)], s);
		Require(quiet.Active.Count == 0, "A pause, a speed change and a dug tile made bubbles.");

		// A fact seen once is one bubble: nothing is drawn from the board's own refreshes.
		var once = new BubbleBoard();
		once.Observe([new ShirkStarted(0)], s);
		for (var i = 0; i < 100; i++)
			once.Observe([], s);
		Require(once.Active.Count == 1 && once.Made == 1, "Observing no events changed the board.");
	}

	// The prewritten lines and symbol labels: some of each, none too long for a bubble, all four kinds distinct, and they cycle without any randomness.
	void AuthoredCopy()
	{
		_scenarios++;
		var kinds = Enum.GetValues<BubbleKind>();
		var labels = new HashSet<string>();
		foreach (var kind in kinds)
		{
			Require(BubbleCopy.Label(kind).Length is > 0 and <= 24, $"{kind}: the symbol label \"{BubbleCopy.Label(kind)}\" is empty or long.");
			Require(labels.Add(BubbleCopy.Label(kind)), $"{kind}: its label repeats another's.");
			Require(BubbleCopy.LineCount(kind) >= 3, $"{kind} has fewer than three lines.");
			var seen = new HashSet<string>();
			for (var sequence = 0; sequence < BubbleCopy.LineCount(kind); sequence++)
			{
				var line = BubbleCopy.Line(kind, sequence, victim: "Snaggle Wartnose", supply: "Grog");
				Require(!line.Contains('{') && line.Length <= 48, $"{kind}: \"{line}\" has an unfilled blank or is over 48 characters.");
				seen.Add(line);
			}
			Require(seen.Count == BubbleCopy.LineCount(kind), $"{kind}: lines repeat within one cycle.");
		}
		Require(BubbleCopy.Line(BubbleKind.Theft, 0, "Kevin", "Grub") == BubbleCopy.Line(BubbleKind.Theft, BubbleCopy.LineCount(BubbleKind.Theft), "Kevin", "Grub"), "Lines do not cycle.");
		Require(Enumerable.Range(0, BubbleCopy.LineCount(BubbleKind.Scuffle)).Any(i => BubbleCopy.Line(BubbleKind.Scuffle, i, "Kevin", "Grub").Contains("Kevin")), "No scuffle line names the victim.");
		Require(Enumerable.Range(0, BubbleCopy.LineCount(BubbleKind.Theft)).Any(i => BubbleCopy.Line(BubbleKind.Theft, i, "Kevin", "Grub").Contains("Grub")), "No theft line names the supply.");
	}

	// Three seconds of real time, however the frames cut it, and not a tick sooner or later.
	void LifetimeIsRealTime()
	{
		_scenarios++;
		var s = NewState();
		foreach (var frames in new[] { 30, 60, 3, 1 })
		{
			var board = new BubbleBoard();
			board.Observe([new ShirkStarted(0)], s);
			var step = BubbleBoard.LifetimeSeconds / frames;
			for (var i = 1; i < frames; i++)
			{
				board.Advance(step);
				Require(board.Active.Count == 1, $"{frames} frames: the bubble went at frame {i}, before three seconds.");
			}
			board.Advance(step);
			Require(board.Active.Count == 0, $"{frames} frames: the bubble outlived three seconds.");
		}

		var edge = new BubbleBoard();
		edge.Observe([new ShirkStarted(0)], s);
		Require(!edge.Advance(2.999), "A bubble reported a change with a millisecond to go.");
		Require(edge.Active.Count == 1, "The bubble went with a millisecond to go.");
		Require(edge.Advance(0.001) && edge.Active.Count == 0, "The bubble outlived exactly three seconds, or the board did not report it going.");

		// Nothing owed: a zero or negative frame ages nothing.
		var still = new BubbleBoard();
		still.Observe([new ShirkStarted(0)], s);
		still.Advance(0);
		still.Advance(-5);
		Require(Math.Abs(still.Active[0].Remaining - BubbleBoard.LifetimeSeconds) < 1e-9, "A zero or negative frame aged the bubble.");
	}

	// One bubble per goblin, three overall, the newest in and the oldest out.
	void OnePerGoblinThreeOverall()
	{
		_scenarios++;
		var s = NewState();
		var board = new BubbleBoard();
		board.Observe([new ShirkStarted(1)], s);
		board.Advance(1.0);
		board.Observe([new HaulCancelled(1)], s);
		Require(board.Active.Count == 1 && board.Active[0].Kind == BubbleKind.LostHaul, "A second incident did not replace the goblin's bubble.");
		Require(Math.Abs(board.Active[0].Remaining - BubbleBoard.LifetimeSeconds) < 1e-9, "The replacing bubble did not start its own three seconds.");

		board.Observe([new ShirkStarted(2), new ShirkStarted(3)], s);
		Require(board.Active.Select(b => b.Goblin).SequenceEqual([1, 2, 3]), $"Three goblins: {string.Join(",", board.Active.Select(b => b.Goblin))}.");
		board.Observe([new ShirkStarted(4)], s);
		Require(board.Active.Select(b => b.Goblin).SequenceEqual([2, 3, 4]), "A fourth bubble did not push out the oldest.");
		// Refreshing a goblin that already has one keeps the others.
		board.Observe([new ShirkStarted(3)], s);
		Require(board.Active.Select(b => b.Goblin).SequenceEqual([2, 4, 3]), "Replacing goblin 3's bubble disturbed another's.");
		// Many in one frame: the last three win.
		board.Observe([new ShirkStarted(0), new ShirkStarted(1), new ShirkStarted(5), new ShirkStarted(6)], s);
		Require(board.Active.Select(b => b.Goblin).SequenceEqual([1, 5, 6]), "Four at once did not leave the newest three.");
		// A goblin's own bubble expiring does not free another's.
		board.Advance(1.0);
		board.Observe([new ShirkStarted(2)], s);
		Require(board.Active.Select(b => b.Goblin).SequenceEqual([5, 6, 2]), "A new bubble beside older ones kept more than three.");
	}

	// An incident makes a bubble whichever level the goblin is on and whichever level is viewed; only the view decides what is drawn.
	void OffLevelIncidents()
	{
		_scenarios++;
		var s = NewState();
		s.GoblinZ[2] = 1;
		var board = new BubbleBoard();
		board.Observe([new ShirkStarted(2)], s);
		board.Observe([new HaulCancelled(2)], s);
		board.Observe([new ScuffleStarted(2, 0)], s);
		board.Observe([new TheftPickedUp(2, Goblins.Sim.Material.Grub)], s);
		Require(board.Active.Count == 1 && board.Active[0].Kind == BubbleKind.Theft, "Incidents on level 1 did not each make (and replace) a bubble.");
		foreach (var incident in new SimEvent[] { new ShirkStarted(2), new HaulCancelled(2), new ScuffleStarted(2, 0), new TheftPickedUp(2, Goblins.Sim.Material.Grub) })
		{
			var each = new BubbleBoard();
			each.Observe([incident], s);
			Require(each.Active.Count == 1 && each.Active[0].Goblin == 2, $"{incident} born off the viewed level made no bubble.");
		}
		board.Observe([new ShirkStarted(99), new ShirkStarted(-1)], s);
		Require(board.Active.Count == 1, "An incident for a goblin that does not exist made a bubble.");
	}

	// Hidden bubbles count toward the three, and an off-level incident pushes out the oldest, visible or not.
	void HiddenBubblesCountTowardTheCap()
	{
		_scenarios++;
		var main = NewMain((_, _) => { });
		try
		{
			var s = main.View.Sim!.State;
			s.GoblinZ[1] = 1;
			s.GoblinZ[2] = 1;
			s.GoblinZ[3] = 1;
			main.Bubbles.Observe([new ShirkStarted(0), new ShirkStarted(1), new ShirkStarted(2)], s);
			Require(main.View.LayoutBubbles().Select(l => l.Bubble.Goblin).SequenceEqual([0]), "Only goblin 0's bubble belongs on the viewed level 0.");
			main.Bubbles.Observe([new ShirkStarted(3)], s);
			Require(main.Bubbles.Active.Select(b => b.Goblin).SequenceEqual([1, 2, 3]), "A fourth, off-level bubble did not push out the visible oldest.");
			Require(main.View.LayoutBubbles().Count == 0, "The replaced bubble was still drawn on level 0.");
			// A replaced incident never comes back.
			Frames(main, 1, 0.1);
			Require(!main.Bubbles.Active.Any(b => b.Goblin == 0), "A replaced bubble reappeared.");
		}
		finally { Dispose(main); }
	}

	// Bubbles sit clear of their own goblin, inside the map, and clear of each other, even for goblins in the corners or sharing a tile.
	void Placement()
	{
		_scenarios++;
		var s = NewState();
		var map = new Vector2(s.Width, s.Height) * WarrenView.CellSize;
		var sizes = new[] { new Vector2(120, 34), new Vector2(150, 34), new Vector2(110, 22) };

		foreach (var (x, y) in new[] { (0, 0), (s.Width - 1, 0), (0, s.Height - 1), (s.Width - 1, s.Height - 1), (16, 0), (16, s.Height - 1), (0, 16), (s.Width - 1, 16), (16, 16) })
		foreach (var size in sizes)
		{
			var cell = new Rect2(x * WarrenView.CellSize, y * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize);
			var rect = BubbleLayout.Place(cell, size, map, [], []);
			Require(new Rect2(Vector2.Zero, map).Encloses(rect), $"Cell ({x},{y}) {size}: the bubble {rect} leaves the map.");
			Require(!rect.Intersects(cell), $"Cell ({x},{y}) {size}: the bubble {rect} covers its own goblin's tile.");
			Require(Math.Abs(rect.Size.X - size.X) < 0.01f && Math.Abs(rect.Size.Y - size.Y) < 0.01f, $"Cell ({x},{y}): the bubble was resized.");
		}

		// Three goblins on one tile, and three on neighbouring tiles: no two bubbles overlap, and none covers a goblin's tile.
		foreach (var tiles in new[] { new[] { (10, 10), (10, 10), (10, 10) }, new[] { (10, 10), (11, 10), (10, 11) }, new[] { (0, 0), (0, 0), (1, 0) }, new[] { (31, 31), (30, 31), (31, 31) } })
		{
			var placed = new List<Rect2>();
			var cells = tiles.Select(t => new Rect2(t.Item1 * WarrenView.CellSize, t.Item2 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize)).ToArray();
			foreach (var cell in cells)
			{
				var rect = BubbleLayout.Place(cell, new Vector2(130, 34), map, placed, cells);
				Require(new Rect2(Vector2.Zero, map).Encloses(rect), $"{string.Join(" ", tiles)}: {rect} leaves the map.");
				foreach (var other in placed)
					Require(!rect.Intersects(other), $"{string.Join(" ", tiles)}: bubbles {rect} and {other} overlap.");
				foreach (var any in cells)
					Require(!rect.Intersects(any), $"{string.Join(" ", tiles)}: bubble {rect} covers a goblin's tile {any}.");
				placed.Add(rect);
			}
		}
	}

	// A fresh Main with its engine callbacks off, so the checks set how much time passes in a frame.
	Main NewMain(Action<Main, Simulation> setUp)
	{
		var clock = new FrameClock();
		var main = new Main { BubbleClock = () => clock.Usec };
		_clocks.Add(main, clock);
		AddChild(main);
		main.SetProcess(false);
		var sim = main.View.Sim!;
		sim.Apply(new SetPaused(false));
		setUp(main, sim);
		return main;
	}

	void Dispose(Main main)
	{
		_clocks.Remove(main);
		RemoveChild(main);
		main.Free();
	}

	void Frames(Main main, int count, double delta)
	{
		for (var i = 0; i < count; i++)
		{
			_clocks[main].Usec += (ulong)Math.Round(delta * 1_000_000);
			main._Process(delta);
		}
	}

	// The same three seconds at 1× and 4× (the sim runs four times the ticks in them), frozen by a pause, and no bubble made of an idle frame.
	void MainTimingAtOneAndFourTimesAndPaused()
	{
		foreach (var speed in new[] { 1, 4 })
		{
			_scenarios++;
			var main = NewMain((_, sim) => sim.Apply(new SetSpeed(speed)));
			try
			{
				var sim = main.View.Sim!;
				var s = sim.State;
				main.Bubbles.Observe([new ShirkStarted(0)], s);
				var ticks0 = s.Tick;
				Frames(main, 29, 0.1);
				Require(main.Bubbles.Active.Count == 1, $"{speed}×: the bubble was gone after 2.9 s.");
				var ticksPerSecond = (s.Tick - ticks0) / 2.9;
				Require(Math.Abs(ticksPerSecond - 10 * speed) < 1.0, $"{speed}×: the sim ran {ticksPerSecond} ticks per second.");

				// Pausing freezes what is left, for as long as the pause lasts.
				var remaining = main.Bubbles.Active[0].Remaining;
				sim.Apply(new SetPaused(true));
				Frames(main, 100, 0.1);
				Require(main.Bubbles.Active.Count == 1 && Math.Abs(main.Bubbles.Active[0].Remaining - remaining) < 1e-9, $"{speed}×: a pause did not freeze the bubble.");
				sim.Apply(new SetPaused(false));
				Frames(main, 0, 0.1);
				Require(Math.Abs(main.Bubbles.Active[0].Remaining - remaining) < 1e-9, $"{speed}×: lifting the pause aged the bubble by itself.");

				// Unpaused again: the same 0.1 s that was left.
				Frames(main, 1, 0.1);
				Require(main.Bubbles.Active.Count == 0, $"{speed}×: the bubble outlived its three unpaused seconds.");

				// A long frame (a hitch) ages the bubble by real time but runs the sim only as far as it ever does.
				main.Bubbles.Observe([new ShirkStarted(1)], s);
				Frames(main, 1, 2.0);
				Require(main.Bubbles.Active.Count == 1 && Math.Abs(main.Bubbles.Active[0].Remaining - 1.0) < 1e-9, $"{speed}×: a 2 s frame left {main.Bubbles.Active.FirstOrDefault()?.Remaining}.");
			}
			finally { Dispose(main); }
		}
	}

	// Pause input can arrive between frames: settle the unpaused interval before freezing, and exclude time spent paused.
	void PauseInputBetweenFrames()
	{
		_scenarios++;
		var main = NewMain((_, _) => { });
		try
		{
			main.Bubbles.Observe([new ShirkStarted(0)], main.View.Sim!.State);
			_clocks[main].Usec += 1_000_000;
			using var pause = new InputEventKey { Keycode = Key.Space, Pressed = true };
			main._UnhandledKeyInput(pause);
			Require(Math.Abs(main.Bubbles.Active[0].Remaining - 2) < 1e-9, "Pausing between frames lost the unpaused second.");
			_clocks[main].Usec += 5_000_000;
			main._UnhandledKeyInput(pause);
			Require(Math.Abs(main.Bubbles.Active[0].Remaining - 2) < 1e-9, "Unpausing between frames charged the paused interval.");
			Frames(main, 19, 0.1);
			Require(main.Bubbles.Active.Count == 1, "A bubble expired before its remaining two unpaused seconds.");
			Frames(main, 1, 0.1);
			Require(main.Bubbles.Active.Count == 0, "A bubble outlived its remaining two unpaused seconds.");
		}
		finally { Dispose(main); }
	}

	void Level(Main main, Key key)
	{
		using var press = new InputEventKey { Keycode = key, Pressed = true };
		main._UnhandledKeyInput(press);
	}

	static List<int> Drawn(Main main) => main.View.LayoutBubbles().Select(l => l.Bubble.Goblin).ToList();

	// Switching levels hides a bubble without deleting it; coming back before it expires shows it again with what is left.
	void LevelRoundTripKeepsBubbles()
	{
		_scenarios++;
		var main = NewMain((_, _) => { });
		try
		{
			var s = main.View.Sim!.State;
			s.GoblinZ[1] = 1;
			main.Bubbles.Observe([new ShirkStarted(0), new ShirkStarted(1)], s);
			Require(Drawn(main).SequenceEqual([0]), "Level 0 should draw goblin 0's bubble only.");
			Frames(main, 10, 0.1);
			Level(main, Key.Period);
			Require(main.Bubbles.Active.Count == 2, "Going down a level deleted a bubble.");
			Require(Drawn(main).SequenceEqual([1]), $"Level 1 drew {string.Join(",", Drawn(main))}, not goblin 1's bubble alone.");
			Frames(main, 5, 0.1);
			Level(main, Key.Comma);
			Require(main.Bubbles.Active.Count == 2, "Coming back deleted a bubble.");
			Require(Drawn(main).SequenceEqual([0]), "Level 0 did not show goblin 0's bubble again, alone.");
			Require(Math.Abs(main.Bubbles.Active.Single(b => b.Goblin == 0).Remaining - 1.5) < 1e-9, "The bubble did not keep the lifetime it had left.");
		}
		finally { Dispose(main); }
	}

	// A bubble ages while hidden, expires off-level, and does not come back on return.
	void ExpiryOffLevel()
	{
		_scenarios++;
		var main = NewMain((_, _) => { });
		try
		{
			var s = main.View.Sim!.State;
			main.Bubbles.Observe([new ShirkStarted(0)], s);
			Level(main, Key.Period);
			Require(Drawn(main).Count == 0 && main.Bubbles.Active.Count == 1, "Level 1 drew goblin 0's bubble, or it was lost on leaving.");
			Frames(main, 29, 0.1);
			Require(main.Bubbles.Active.Count == 1, "A hidden bubble expired early.");
			Frames(main, 1, 0.1);
			Require(main.Bubbles.Active.Count == 0, "A hidden bubble outlived its three seconds.");
			Level(main, Key.Comma);
			Require(Drawn(main).Count == 0 && main.Bubbles.Active.Count == 0, "An expired bubble reappeared on return.");
		}
		finally { Dispose(main); }
	}

	// Pause freezes bubbles on other levels too, and the hidden lifetime is the same at 1× and 4×.
	void HiddenBubblesPauseAndSpeed()
	{
		foreach (var speed in new[] { 1, 4 })
		{
			_scenarios++;
			var main = NewMain((_, sim) => sim.Apply(new SetSpeed(speed)));
			try
			{
				var sim = main.View.Sim!;
				main.Bubbles.Observe([new ShirkStarted(0)], sim.State);
				Level(main, Key.Period);
				Frames(main, 10, 0.1);
				sim.Apply(new SetPaused(true));
				var left = main.Bubbles.Active[0].Remaining;
				Require(Math.Abs(left - 2.0) < 1e-9, $"{speed}×: a hidden bubble had {left}s left after 1 s, not 2.");
				Frames(main, 100, 0.1);
				Require(main.Bubbles.Active.Count == 1 && Math.Abs(main.Bubbles.Active[0].Remaining - left) < 1e-9, $"{speed}×: a pause did not freeze a hidden bubble.");
				sim.Apply(new SetPaused(false));
				Frames(main, 19, 0.1);
				Require(main.Bubbles.Active.Count == 1, $"{speed}×: a hidden bubble expired early.");
				Frames(main, 1, 0.1);
				Require(main.Bubbles.Active.Count == 0, $"{speed}×: a hidden bubble outlived its three unpaused seconds.");
			}
			finally { Dispose(main); }
		}
	}

	// A bubble follows its goblin up and down the stairs: hidden when it leaves the viewed level, drawn over the goblin's own tile when it arrives,
	// and never over another goblin.
	void BubbleFollowsGoblinOnStairs()
	{
		_scenarios++;
		var main = NewMain((_, _) => { });
		try
		{
			var s = main.View.Sim!.State;
			(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]) = (5, 6, 0);
			(s.GoblinX[1], s.GoblinY[1], s.GoblinZ[1]) = (20, 20, 1);
			main.Bubbles.Observe([new ShirkStarted(0)], s);
			var anchor = main.View.LayoutBubbles().Single().Anchor;
			Require(anchor.Position.X >= 5 * WarrenView.CellSize && anchor.Position.X < 6 * WarrenView.CellSize, "The bubble did not hang from goblin 0's tile.");
			Frames(main, 5, 0.1);

			// Goblin 0 takes the stairs down to level 1, arriving at (20,20,1) where goblin 1 stands.
			(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]) = (20, 20, 1);
			Frames(main, 1, 0.1);
			Require(Drawn(main).Count == 0 && main.Bubbles.Active.Count == 1, "The bubble stayed over level 0 or was deleted when its goblin took the stairs.");
			Level(main, Key.Period);
			var laid = main.View.LayoutBubbles();
			Require(laid.Count == 1 && laid[0].Bubble.Goblin == 0, "Level 1 did not show goblin 0's bubble.");
			Require(laid[0].Anchor.Position.X >= 20 * WarrenView.CellSize && laid[0].Anchor.Position.X < 21 * WarrenView.CellSize, "The bubble hangs from a tile other than where goblin 0 now is.");
			Require(!laid.Any(l => l.Bubble.Goblin == 1), "Goblin 1, who had no incident, got a bubble.");

			// And back up: it hangs where goblin 0 now stands, not where it began.
			(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]) = (7, 7, 0);
			Level(main, Key.Comma);
			var back = main.View.LayoutBubbles().Single();
			Require(back.Anchor.Position.X >= 7 * WarrenView.CellSize && back.Anchor.Position.X < 8 * WarrenView.CellSize, "The bubble did not follow its goblin back up.");
		}
		finally { Dispose(main); }
	}

	// Run the real sim until something really happens: the bubble is for the goblin it happened to, drawn from one event, and a still frame adds none.
	void RealIncidentsMakeBubbles()
	{
		_scenarios++;
		var main = NewMain((_, sim) =>
		{
			sim.Apply(new SetSpeed(4));
			var s = sim.State;
			for (var g = 0; g < s.GoblinCount; g++)
				s.GoblinChaos[g] = 80;
		});
		try
		{
			var s = main.View.Sim!.State;
			var seen = new HashSet<BubbleKind>();
			var made = 0;
			for (var frame = 0; frame < 4000 && made == 0; frame++)
			{
				Frames(main, 1, 0.05);
				made = main.Bubbles.Made;
			}
			Require(made > 0, "A warren of goblins at 80 Chaos produced no bubble in 4000 frames.");
			foreach (var bubble in main.Bubbles.Active)
			{
				seen.Add(bubble.Kind);
				var task = (GoblinTask)s.GoblinTask[bubble.Goblin];
				var fits = bubble.Kind switch
				{
					BubbleKind.Shirk => task is GoblinTask.Shirking,
					BubbleKind.Scuffle => task is GoblinTask.Scuffling,
					BubbleKind.Theft => GoblinTasks.IsTheftTask(task),
					_ => true,
				};
				Require(fits, $"A {bubble.Kind} bubble over {bubble.Name}, who is {task}.");
			}

			// Frames that pass no time run no ticks and so make no bubble.
			var count = main.Bubbles.Made;
			var tick = s.Tick;
			main.View.Sim!.Apply(new SetPaused(true));
			Frames(main, 200, 0.05);
			Require(s.Tick == tick && main.Bubbles.Made == count, "Paused frames ran ticks or made bubbles.");
		}
		finally { Dispose(main); }
	}

	// Two runs of one seed and one frame sequence, one through Main's callbacks (bubbles made and aged) and one bare: the saved
	// state, including the RNG, is identical. This synchronous comparison queues redraws but does not render them; CarryDrawChecks covers pixels.
	void PresentationLeavesTheSimAlone()
	{
		_scenarios++;
		var frames = new List<(double Delta, int Speed, bool Paused)>();
		var rng = new Random(7);
		var speed = 1;
		var paused = false;
		for (var i = 0; i < 2500; i++)
		{
			if (i % 400 == 0) speed = new[] { 1, 4, 2, 4 }[(i / 400) % 4];
			if (i % 300 == 150) paused = !paused;
			frames.Add((0.02 + rng.NextDouble() * 0.2, speed, paused));
		}

		var main = NewMain((_, sim) =>
		{
			var s = sim.State;
			for (var g = 0; g < s.GoblinCount; g++)
				s.GoblinChaos[g] = (byte)(60 + 5 * g);
		});
		try
		{
			var viaMain = main.View.Sim!;
			var seed = viaMain.State.Seed;
			var bare = Simulation.NewWorld(seed);
			bare.Apply(new SetPaused(false));
			for (var g = 0; g < bare.State.GoblinCount; g++)
				bare.State.GoblinChaos[g] = (byte)(60 + 5 * g);

			var viewZ = 0;
			foreach (var (delta, frameSpeed, framePaused) in frames)
			{
				foreach (var sim in new[] { viaMain, bare })
				{
					sim.Apply(new SetSpeed(frameSpeed));
					sim.Apply(new SetPaused(framePaused));
				}
				bare.Advance(delta);
				bare.DrainEvents();
				Frames(main, 1, delta);
				if (main.Bubbles.Active.Count > 0 && viewZ == 0)
					main.View.QueueRedraw();
			}

			Require(main.Bubbles.Made > 0, "The run made no bubbles, so it proves nothing.");
			Require(viaMain.State.Tick == bare.State.Tick && viaMain.State.Tick > 1000, $"The runs ended at ticks {viaMain.State.Tick} and {bare.State.Tick}.");
			Require(viaMain.State.Rng.State == bare.State.Rng.State, "The RNG state differs between the run with bubbles and the bare run.");
			Require(SimJson.Serialize(viaMain) == SimJson.Serialize(bare), "The saved state differs between the run with bubbles and the bare run.");
		}
		finally { Dispose(main); }
	}

	static void Require(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}

	// Let the engine supply its capped delta after a real main-thread stall; a direct _Process(3.2) call cannot catch this.
	async System.Threading.Tasks.Task MainExpiresAfterEngineStall()
	{
		foreach (var speed in new[] { 1, 4 })
		{
			_scenarios++;
			var main = new Main();
			AddChild(main);
			try
			{
				main.View.Sim!.Apply(new SetSpeed(speed));
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				main.Bubbles.Observe([new ShirkStarted(0)], main.View.Sim.State);
				var start = Time.GetTicksUsec();
				OS.DelayMsec(3200);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				var elapsed = (Time.GetTicksUsec() - start) / 1_000_000.0;
				GD.Print($"Engine stall {speed}×: {elapsed:F3}s elapsed, {main.Bubbles.Active.Count} bubbles remain.");
				Require(main.Bubbles.Active.Count == 0, $"{speed}×: a bubble survived a real {elapsed:F3}s engine stall.");
			}
			finally { Dispose(main); }
		}
	}
}
