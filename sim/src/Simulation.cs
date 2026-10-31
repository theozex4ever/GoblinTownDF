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

	// Not saved: Godot drains events every frame, so a save holds none worth keeping.
	readonly List<SimEvent> _events = [];

	readonly JobSystem _jobs;

	public Simulation(SimState state, IPathfinder? pathfinder = null)
	{
		State = state;
		_jobs = new JobSystem(pathfinder ?? new GridPathfinder());
		// Reachability is derived, never saved, so every sim builds it from its tiles.
		RecheckReachability();
	}

	public SimState State { get; }

	/// <summary>
	/// The one hook for reachability: call it after opening paths or adding Designations outside digging.
	/// Digging a tile and the Designate command already call it. Cancellation needs no recheck because
	/// IsReachable also checks that the Designation still exists.
	/// </summary>
	public void RecheckReachability() => _jobs.RecheckReachability(State);

	public static bool IsValidSpeed(int speed) => speed is 1 or 2 or 4;

	// The starting cluster on z0: a plus around the map centre, as (dx, dy) from the centre tile.
	static readonly (int Dx, int Dy)[] StartingCluster = [(0, 0), (-1, 0), (1, 0), (0, -1), (0, 1)];

	/// <summary>The P2 warren: flat and identical for every seed. The seed only seeds <see cref="SimRandom"/>.</summary>
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
		s.Designations = new byte[layer * MapDepth];
		s.DigProgress = new byte[layer * MapDepth];
		s.TileItems = new int[layer * MapDepth * SimState.CountedMaterials];
		s.Piles = new byte[layer * MapDepth];
		// The surface and z1 are dirt, so digging into them yields dirt; z2 is stone.
		for (var z = 0; z < MapDepth; z++)
		{
			var kind = z == 0 ? TileKind.Floor : TileKind.Solid;
			var material = z == MapDepth - 1 ? Material.Stone : Material.Dirt;
			Array.Fill(s.TileKinds, (byte)kind, z * layer, layer);
			Array.Fill(s.TileMaterials, (byte)material, z * layer, layer);
		}

		s.GoblinCount = StartingCluster.Length;
		for (var g = 0; g < s.GoblinCount; g++)
		{
			s.GoblinRole[g] = (byte)Role.Grunt;
			s.GoblinX[g] = MapWidth / 2 + StartingCluster[g].Dx;
			s.GoblinY[g] = MapHeight / 2 + StartingCluster[g].Dy;
			s.GoblinZ[g] = 0;
		}

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
				if (!IsValidSpeed(c.Speed))
					throw new ArgumentOutOfRangeException(nameof(command), c.Speed, "Speed must be 1, 2 or 4.");
				if (State.Speed != c.Speed)
				{
					State.Speed = c.Speed;
					_events.Add(new SpeedChanged(c.Speed));
				}
				break;
			case Designate c:
				_jobs.Designate(State, c);
				break;
			case CancelDesignation c:
				_jobs.Cancel(State, c);
				break;
			case PaintPile c:
				PileRules.Paint(State, c);
				break;
			case RemovePile c:
				PileRules.Remove(State, c);
				break;
			default:
				throw new ArgumentException($"Unknown command {command.GetType().Name}.", nameof(command));
		}
	}

	/// <summary>
	/// Feeds one frame of wall time in and runs whole ticks: 10 per second at 1×, 2× and 4× as many
	/// at higher speeds. Returns the ticks run. Paused time is not owed afterwards. A frame time that
	/// is not a positive number runs nothing.
	/// </summary>
	public int Advance(double frameSeconds)
	{
		if (State.Paused)
		{
			State.TickDebt = 0;
			return 0;
		}
		if (!(frameSeconds > 0))
			return 0;

		var debt = State.TickDebt + Math.Min(frameSeconds, MaxFrameSeconds) * TicksPerSecond * State.Speed;

		var ticks = 0;
		while (debt >= 1.0 - Epsilon)
		{
			debt -= 1.0;
			Step();
			ticks++;
		}
		State.TickDebt = Math.Max(debt, 0);
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
		_jobs.Step(State, _events);
	}
}
