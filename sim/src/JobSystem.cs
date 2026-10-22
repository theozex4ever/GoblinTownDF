namespace Goblins.Sim;

/// <summary>
/// The global job list and the goblins that work it. A designated tile is a Job; an idle goblin claims
/// the nearest one by path length (ties go to the lowest tile index), walks to a tile it can dig from,
/// and digs. Loops over the state arrays; the only memory it owns is reused scratch lists.
/// </summary>
public sealed class JobSystem
{
	public const int WalkTicksPerTile = 3;

	readonly IPathfinder _pathfinder;
	readonly List<int> _path = [];
	readonly List<int> _goals = [];
	readonly List<int> _goalJobs = [];
	readonly List<int> _starts = [];
	byte[] _reached = [];

	public JobSystem(IPathfinder pathfinder)
	{
		_pathfinder = pathfinder;
	}

	/// <summary>Designates the rectangle, skipping tiles that cannot take it or are already designated.</summary>
	public void Designate(SimState s, Designate command)
	{
		if (command.Kind != DesignationKind.DigStairs)
			throw new ArgumentOutOfRangeException(nameof(command), command.Kind, "Only Dig stairs can be designated.");
		if (command.Z < 0 || command.Z >= s.Depth)
			throw new ArgumentOutOfRangeException(nameof(command), command.Z, "Level is off the map.");

		// Clip the rectangle to the map. One wholly off the map designates nothing, rather than the edge row.
		var x0 = Math.Max(Math.Min(command.X0, command.X1), 0);
		var x1 = Math.Min(Math.Max(command.X0, command.X1), s.Width - 1);
		var y0 = Math.Max(Math.Min(command.Y0, command.Y1), 0);
		var y1 = Math.Min(Math.Max(command.Y0, command.Y1), s.Height - 1);
		var designated = false;
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
		{
			var tile = s.TileIndex(x, y, command.Z);
			if (s.DesignationAt(tile) == DesignationKind.None
				&& TileRules.CanDigStairs(s.KindAt(tile))
				&& MaterialRules.DigTicks(s.MaterialAt(tile)) > 0)
			{
				s.Designations[tile] = (byte)command.Kind;
				designated = true;
			}
		}

		if (designated)
			RecheckReachability(s);
	}

	/// <summary>
	/// Rebuilds <see cref="SimState.Reachable"/> from the tiles, designations and goblin positions. Runs once
	/// when a tile is dug, when a Designation is made, and when a sim is built, and never on a tick that did
	/// none of those. Anything else that changes a tile or a Designation (cancelling, new tools) calls this
	/// too, through <see cref="Simulation.RecheckReachability"/>.
	/// </summary>
	public void RecheckReachability(SimState s)
	{
		var tiles = s.TileKinds.Length;
		if (s.Reachable.Length != tiles)
			s.Reachable = new byte[tiles];
		if (_reached.Length != tiles)
			_reached = new byte[tiles];

		// Goblins never leave the region they stand in, so what they can reach only changes when a tile does.
		_starts.Clear();
		for (var g = 0; g < s.GoblinCount; g++)
			_starts.Add(s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]));
		_pathfinder.FloodReachable(s, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_starts), _reached);

		for (var tile = 0; tile < tiles; tile++)
		{
			s.Reachable[tile] = 0;
			if (s.DesignationAt(tile) == DesignationKind.None)
				continue;

			_goals.Clear();
			_goalJobs.Clear();
			AddStandingTiles(s, tile);
			foreach (var stand in _goals)
				if (_reached[stand] != 0)
				{
					s.Reachable[tile] = 1;
					break;
				}
		}
	}

	public void Step(SimState s, List<SimEvent> events)
	{
		for (var g = 0; g < s.GoblinCount; g++)
		{
			if ((GoblinTask)s.GoblinTask[g] == GoblinTask.Idle && !TryClaim(s, g))
				continue;

			var job = s.GoblinJobTile[g];
			if (s.DesignationAt(job) == DesignationKind.None)
			{
				Release(s, g);
				continue;
			}

			// One action per tick: arriving spends the tick, so digging starts on the next one.
			switch ((GoblinTask)s.GoblinTask[g])
			{
				case GoblinTask.Walking:
					Walk(s, g, job);
					break;
				case GoblinTask.Digging:
					Dig(s, g, job, events);
					break;
			}
		}
	}

	bool TryClaim(SimState s, int goblin)
	{
		_goals.Clear();
		_goalJobs.Clear();
		for (var tile = 0; tile < s.Designations.Length; tile++)
		{
			if (!s.IsReachable(tile) || IsClaimed(s, tile))
				continue;
			AddStandingTiles(s, tile);
		}

		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (!_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out var goalIndex))
			return false;

		s.GoblinJobTile[goblin] = _goalJobs[goalIndex];
		s.GoblinStepTicks[goblin] = 0;
		s.GoblinTask[goblin] = (byte)(_path.Count == 0 ? GoblinTask.Digging : GoblinTask.Walking);
		return true;
	}

	void Walk(SimState s, int goblin, int job)
	{
		if (++s.GoblinStepTicks[goblin] < WalkTicksPerTile)
			return;
		s.GoblinStepTicks[goblin] = 0;

		_goals.Clear();
		_goalJobs.Clear();
		AddStandingTiles(s, job);
		var start = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		if (!_pathfinder.TryFindPath(s, start, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_goals), _path, out _)
			|| _path.Count == 0)
		{
			Release(s, goblin);
			return;
		}

		var next = _path[0];
		var layer = s.Width * s.Height;
		s.GoblinX[goblin] = next % s.Width;
		s.GoblinY[goblin] = next / s.Width % s.Height;
		s.GoblinZ[goblin] = next / layer;
		if (_path.Count == 1)
			s.GoblinTask[goblin] = (byte)GoblinTask.Digging;
	}

	void Dig(SimState s, int goblin, int job, List<SimEvent> events)
	{
		var material = s.MaterialAt(job);
		var progress = ++s.DigProgress[job];
		if (progress < MaterialRules.DigTicks(material))
			return;

		s.TileKinds[job] = (byte)TileKind.Stairs;
		s.Designations[job] = (byte)DesignationKind.None;
		s.DigProgress[job] = 0;
		var layer = s.Width * s.Height;
		events.Add(new TileDug(job % s.Width, job / s.Width % s.Height, job / layer, material));
		Release(s, goblin);
		RecheckReachability(s);
	}

	static void Release(SimState s, int goblin)
	{
		s.GoblinTask[goblin] = (byte)GoblinTask.Idle;
		s.GoblinJobTile[goblin] = -1;
		s.GoblinStepTicks[goblin] = 0;
	}

	static bool IsClaimed(SimState s, int tile)
	{
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.GoblinJobTile[g] == tile)
				return true;
		return false;
	}

	/// <summary>
	/// Whether a goblin on <paramref name="stand"/> can dig <paramref name="tile"/>: from a walkable
	/// orthogonal neighbour on its level, or from Stairs directly above or below it.
	/// </summary>
	public static bool CanDigFrom(SimState s, int stand, int tile)
	{
		var layer = s.Width * s.Height;
		var dx = Math.Abs(stand % s.Width - tile % s.Width);
		var dy = Math.Abs(stand / s.Width % s.Height - tile / s.Width % s.Height);
		var dz = Math.Abs(stand / layer - tile / layer);
		var kind = s.KindAt(stand);
		return dz == 0
			? dx + dy == 1 && TileRules.IsWalkable(kind)
			: dz == 1 && dx == 0 && dy == 0 && kind == TileKind.Stairs;
	}

	/// <summary>Appends the tiles a goblin can dig <paramref name="tile"/> from (see <see cref="CanDigFrom"/>).</summary>
	void AddStandingTiles(SimState s, int tile)
	{
		var width = s.Width;
		var layer = s.Width * s.Height;
		var x = tile % width;
		var y = tile / width % s.Height;
		var z = tile / layer;
		if (x > 0) AddIfStanding(s, tile - 1, tile);
		if (x < width - 1) AddIfStanding(s, tile + 1, tile);
		if (y > 0) AddIfStanding(s, tile - width, tile);
		if (y < s.Height - 1) AddIfStanding(s, tile + width, tile);
		if (z > 0) AddIfStanding(s, tile - layer, tile);
		if (z < s.Depth - 1) AddIfStanding(s, tile + layer, tile);
	}

	void AddIfStanding(SimState s, int stand, int job)
	{
		if (CanDigFrom(s, stand, job))
		{
			_goals.Add(stand);
			_goalJobs.Add(job);
		}
	}
}
