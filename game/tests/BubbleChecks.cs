using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>
/// Checks the provisional chat bubbles: how an incident becomes a bubble, how long it lives, how many there can be, where it is placed, and that
/// showing, hiding and timing them never touches the sim. Board and layout checks are pure; the Main checks drive the real scene's
/// <c>_Process</c> with set frame times (the engine's own calls are switched off), so they run headless. What a bubble looks like on screen is
/// checked in <c>CarryDrawChecks</c>, which needs a renderer.
/// </summary>
public partial class BubbleChecks : Node
{
	int _scenarios;

	public override void _Ready()
	{
		try
		{
			IncidentsMakeBubbles();
			AuthoredCopy();
			LifetimeIsRealTime();
			OnePerGoblinThreeOverall();
			OffLevelIncidents();
			Placement();
			MainTimingAtOneAndFourTimesAndPaused();
			MainDropsOffLevelAndKeepsOnLevelChange();
			RealIncidentsMakeBubbles();
			PresentationLeavesTheSimAlone();
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
			board.Observe([incident], s, viewZ: 0);
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
			scuffle.Observe([new ScuffleStarted(0, 6)], s, 0);
			theft.Observe([new TheftPickedUp(0, Goblins.Sim.Material.Grub)], s, 0);
			Require(scuffle.Active.Count == 1 && scuffle.Active[0].Goblin == 0, "A scuffle made a bubble for someone but the instigator.");
		}

		// Events that are not one of the four make nothing.
		var quiet = new BubbleBoard();
		quiet.Observe([new PausedChanged(true), new SpeedChanged(4), new TileDug(1, 1, 0, Goblins.Sim.Material.Dirt)], s, 0);
		Require(quiet.Active.Count == 0, "A pause, a speed change and a dug tile made bubbles.");

		// A fact seen once is one bubble: nothing is drawn from the board's own refreshes.
		var once = new BubbleBoard();
		once.Observe([new ShirkStarted(0)], s, 0);
		for (var i = 0; i < 100; i++)
			once.Observe([], s, 0);
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
			board.Observe([new ShirkStarted(0)], s, 0);
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
		edge.Observe([new ShirkStarted(0)], s, 0);
		Require(!edge.Advance(2.999), "A bubble reported a change with a millisecond to go.");
		Require(edge.Active.Count == 1, "The bubble went with a millisecond to go.");
		Require(edge.Advance(0.001) && edge.Active.Count == 0, "The bubble outlived exactly three seconds, or the board did not report it going.");

		// Nothing owed: a zero or negative frame ages nothing.
		var still = new BubbleBoard();
		still.Observe([new ShirkStarted(0)], s, 0);
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
		board.Observe([new ShirkStarted(1)], s, 0);
		board.Advance(1.0);
		board.Observe([new HaulCancelled(1)], s, 0);
		Require(board.Active.Count == 1 && board.Active[0].Kind == BubbleKind.LostHaul, "A second incident did not replace the goblin's bubble.");
		Require(Math.Abs(board.Active[0].Remaining - BubbleBoard.LifetimeSeconds) < 1e-9, "The replacing bubble did not start its own three seconds.");

		board.Observe([new ShirkStarted(2), new ShirkStarted(3)], s, 0);
		Require(board.Active.Select(b => b.Goblin).SequenceEqual([1, 2, 3]), $"Three goblins: {string.Join(",", board.Active.Select(b => b.Goblin))}.");
		board.Observe([new ShirkStarted(4)], s, 0);
		Require(board.Active.Select(b => b.Goblin).SequenceEqual([2, 3, 4]), "A fourth bubble did not push out the oldest.");
		// Refreshing a goblin that already has one keeps the others.
		board.Observe([new ShirkStarted(3)], s, 0);
		Require(board.Active.Select(b => b.Goblin).SequenceEqual([2, 4, 3]), "Replacing goblin 3's bubble disturbed another's.");
		// Many in one frame: the last three win.
		board.Observe([new ShirkStarted(0), new ShirkStarted(1), new ShirkStarted(5), new ShirkStarted(6)], s, 0);
		Require(board.Active.Select(b => b.Goblin).SequenceEqual([1, 5, 6]), "Four at once did not leave the newest three.");
		// A goblin's own bubble expiring does not free another's.
		board.Advance(1.0);
		board.Observe([new ShirkStarted(2)], s, 0);
		Require(board.Active.Select(b => b.Goblin).SequenceEqual([5, 6, 2]), "A new bubble beside older ones kept more than three.");
	}

	// A goblin on another level gets no bubble on this one; a bubble follows its goblin's level.
	void OffLevelIncidents()
	{
		_scenarios++;
		var s = NewState();
		s.GoblinZ[2] = 1;
		var board = new BubbleBoard();
		board.Observe([new ShirkStarted(2), new HaulCancelled(2), new ScuffleStarted(2, 0), new TheftPickedUp(2, Goblins.Sim.Material.Grub)], s, viewZ: 0);
		Require(board.Active.Count == 0, "An incident on level 1 made a bubble while viewing level 0.");
		board.Observe([new ShirkStarted(2)], s, viewZ: 1);
		Require(board.Active.Count == 1, "An incident on the viewed level 1 made no bubble.");
		board.Observe([new ShirkStarted(99), new ShirkStarted(-1)], s, viewZ: 1);
		Require(board.Active.Count == 1, "An incident for a goblin that does not exist made a bubble.");

		Require(!board.KeepLevel(s, viewZ: 1), "KeepLevel reported a change with the goblin on the viewed level.");
		Require(board.KeepLevel(s, viewZ: 0) && board.Active.Count == 0, "A bubble stayed up on a level its goblin is not on.");
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
		var main = new Main();
		AddChild(main);
		main.SetProcess(false);
		var sim = main.View.Sim!;
		sim.Apply(new SetPaused(false));
		setUp(main, sim);
		return main;
	}

	void Dispose(Main main)
	{
		RemoveChild(main);
		main.Free();
	}

	static void Frames(Main main, int count, double delta)
	{
		for (var i = 0; i < count; i++)
			main._Process(delta);
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
				main.Bubbles.Observe([new ShirkStarted(0)], s, 0);
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
				main.Bubbles.Observe([new ShirkStarted(1)], s, 0);
				Frames(main, 1, 2.0);
				Require(main.Bubbles.Active.Count == 1 && Math.Abs(main.Bubbles.Active[0].Remaining - 1.0) < 1e-9, $"{speed}×: a 2 s frame left {main.Bubbles.Active.FirstOrDefault()?.Remaining}.");
			}
			finally { Dispose(main); }
		}
	}

	// Bubbles on a level go when the player leaves it, and are made only for goblins on the level the player is on.
	void MainDropsOffLevelAndKeepsOnLevelChange()
	{
		_scenarios++;
		var main = NewMain((_, _) => { });
		try
		{
			var s = main.View.Sim!.State;
			s.GoblinZ[1] = 1;
			main.Bubbles.Observe([new ShirkStarted(0), new ShirkStarted(1)], s, 0);
			Require(main.Bubbles.Active.Count == 1 && main.Bubbles.Active[0].Goblin == 0, "Only the goblin on the viewed level should have a bubble.");
			using (var up = new InputEventKey { Keycode = Key.Period, Pressed = true })
				main._UnhandledKeyInput(up);
			Require(main.Bubbles.Active.Count == 0, "A bubble stayed up after the player went down a level.");
			main.Bubbles.Observe([new ShirkStarted(1)], s, 1);
			Require(main.Bubbles.Active.Count == 1, "A goblin on the viewed level 1 had no bubble.");
			using (var down = new InputEventKey { Keycode = Key.Comma, Pressed = true })
				main._UnhandledKeyInput(down);
			Require(main.Bubbles.Active.Count == 0, "A bubble stayed up after the player went back up.");
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
				main._Process(0.05);
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

	// Two runs of one seed and one frame sequence, one through the real Main scene (bubbles made, aged, placed, drawn) and one bare: the saved
	// state, including the RNG, is identical at the end. The presentation reads the sim and draws; it writes nothing.
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
				main._Process(delta);
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
}
