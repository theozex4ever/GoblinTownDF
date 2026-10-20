namespace Goblins.Sim;

/// <summary>
/// The headless sim. Commands go in through <see cref="Apply"/>, events come out through
/// <see cref="DrainEvents"/>, and <see cref="State"/> is read-only to the renderer.
/// </summary>
public sealed class Simulation
{
	public const int TicksPerSecond = 10;
	public const int MapWidth = 32;
	public const int MapHeight = 32;
	public const int MapDepth = 3;

	// A stalled frame may owe at most this much wall time, so it cannot trigger a burst of ticks.
	const double MaxFrameSeconds = 0.25;
	// Absorbs floating-point drift when frame times add up to a whole tick.
	const double Epsilon = 1e-9;

	readonly List<SimEvent> _events = [];
	double _tickDebt;

	public Simulation(SimState state)
	{
		State = state;
	}

	public SimState State { get; }

	/// <summary>The P1 warren: flat and identical for every seed. The seed only seeds <see cref="SimRandom"/>.</summary>
	public static Simulation NewWorld(int seed)
	{
		var s = new SimState
		{
			Seed = seed,
			Rng = new SimRandom(seed),
			Width = MapWidth,
			Height = MapHeight,
			Depth = MapDepth,
		};

		var layer = MapWidth * MapHeight;
		s.TileKinds = new byte[layer * MapDepth];
		s.TileMaterials = new byte[layer * MapDepth];
		for (var z = 0; z < MapDepth; z++)
		{
			var kind = z == 0 ? TileKind.Floor : TileKind.Solid;
			var material = z == 0 ? Material.None : Material.Dirt;
			Array.Fill(s.TileKinds, (byte)kind, z * layer, layer);
			Array.Fill(s.TileMaterials, (byte)material, z * layer, layer);
		}

		s.GoblinCount = 1;
		s.GoblinRole[0] = (byte)Role.Grunt;
		s.GoblinX[0] = 16;
		s.GoblinY[0] = 16;
		s.GoblinZ[0] = 0;

		return new Simulation(s);
	}

	public void Apply(Command command)
	{
		switch (command)
		{
			case SetPaused c:
				if (State.Paused != c.Paused)
				{
					State.Paused = c.Paused;
					_events.Add(new PausedChanged(c.Paused));
				}
				break;
			case SetSpeed c:
				if (c.Speed is not (1 or 2 or 4))
					throw new ArgumentOutOfRangeException(nameof(command), c.Speed, "Speed must be 1, 2 or 4.");
				if (State.Speed != c.Speed)
				{
					State.Speed = c.Speed;
					_events.Add(new SpeedChanged(c.Speed));
				}
				break;
			default:
				throw new ArgumentException($"Unknown command {command.GetType().Name}.", nameof(command));
		}
	}

	/// <summary>
	/// Feeds one frame of wall time in and runs whole ticks: 10 per second at 1×, 2× and 4× as many
	/// at higher speeds. Returns the ticks run. Paused time is not owed afterwards.
	/// </summary>
	public int Advance(double frameSeconds)
	{
		if (State.Paused)
		{
			_tickDebt = 0;
			return 0;
		}

		_tickDebt += Math.Min(frameSeconds, MaxFrameSeconds) * TicksPerSecond * State.Speed;

		var ticks = 0;
		while (_tickDebt >= 1.0 - Epsilon)
		{
			_tickDebt -= 1.0;
			Step();
			ticks++;
		}
		return ticks;
	}

	public IReadOnlyList<SimEvent> DrainEvents()
	{
		var drained = _events.ToArray();
		_events.Clear();
		return drained;
	}

	void Step()
	{
		State.Tick++;
	}
}
