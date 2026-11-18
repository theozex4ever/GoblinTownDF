using System;
using System.Collections.Generic;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>One chat bubble: a kind, the goblin it hangs over, what it says, and how long it has left.</summary>
public sealed class Bubble
{
	public required int Goblin { get; init; }
	public required BubbleKind Kind { get; init; }
	public required string Name { get; init; }
	public required string Line { get; init; }
	public double Remaining { get; set; }
}

/// <summary>
/// The chat bubbles on show, provisional presentation of the sim's incident events. A bubble is born from a sim event (<see cref="Observe"/>),
/// ages by unpaused real time (<see cref="Advance"/>) and is gone after <see cref="LifetimeSeconds"/>; there is at most one per goblin and
/// <see cref="MaxBubbles"/> overall, hidden ones included, the newest replacing the oldest. It holds no history, is not saved, reads the sim but
/// never writes it, and draws nothing from the sim's RNG. A bubble is made whichever level the viewer is on and ages the same off-level; it follows
/// its goblin's current tile and level, and the view draws it only while that level is viewed.
/// </summary>
public sealed class BubbleBoard
{
	public const double LifetimeSeconds = 3.0;
	public const int MaxBubbles = 3;
	// Frame times are sums of doubles, so a bubble whose time is up to rounding is up.
	const double Epsilon = 1e-9;

	readonly List<Bubble> _active = [];

	/// <summary>The bubbles on show, oldest first.</summary>
	public IReadOnlyList<Bubble> Active => _active;

	/// <summary>How many bubbles have been made, ever: also the count that picks each bubble's line.</summary>
	public int Made { get; private set; }

	/// <summary>
	/// Makes a bubble for each of the four incidents in <paramref name="events"/> in order, so a
	/// later fact replaces an earlier one. The scuffle's bubble is the instigator's. Returns whether the bubbles on show changed.
	/// </summary>
	public bool Observe(IReadOnlyList<SimEvent> events, SimState s)
	{
		var changed = false;
		foreach (var incident in events)
		{
			var made = incident switch
			{
				ShirkStarted e => Add(s, e.Goblin, BubbleKind.Shirk, victim: "", supply: ""),
				ScuffleStarted e => Add(s, e.Instigator, BubbleKind.Scuffle, victim: e.Victim >= 0 && e.Victim < s.GoblinCount ? GoblinNames.For(e.Victim) : "someone", supply: ""),
				TheftPickedUp e => Add(s, e.Goblin, BubbleKind.Theft, victim: "", supply: e.Supply.ToString()),
				HaulCancelled e => Add(s, e.Goblin, BubbleKind.LostHaul, victim: "", supply: ""),
				_ => false,
			};
			changed |= made;
		}
		return changed;
	}

	bool Add(SimState s, int goblin, BubbleKind kind, string victim, string supply)
	{
		if (goblin < 0 || goblin >= s.GoblinCount)
			return false;

		_active.RemoveAll(b => b.Goblin == goblin);
		if (_active.Count >= MaxBubbles)
			_active.RemoveAt(0);
		_active.Add(new Bubble
		{
			Goblin = goblin,
			Kind = kind,
			Name = GoblinNames.For(goblin),
			Line = BubbleCopy.Line(kind, goblin + Made, victim, supply),
			Remaining = LifetimeSeconds,
		});
		Made++;
		return true;
	}

	/// <summary>Ages every bubble by <paramref name="seconds"/> of unpaused real time and drops those whose three seconds are up. Returns whether any went.</summary>
	public bool Advance(double seconds)
	{
		if (!(seconds > 0) || _active.Count == 0)
			return false;
		foreach (var bubble in _active)
			bubble.Remaining -= seconds;
		return _active.RemoveAll(b => b.Remaining <= Epsilon) > 0;
	}
}
