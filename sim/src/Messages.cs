namespace Goblins.Sim;

/// <summary>An input from Godot to the sim.</summary>
public abstract record Command;

public sealed record SetPaused(bool Paused) : Command;

/// <summary>Speed is 1, 2 or 4. It changes ticks per second, never the tick length.</summary>
public sealed record SetSpeed(int Speed) : Command;

/// <summary>An output from the sim to Godot.</summary>
public abstract record SimEvent;

public sealed record PausedChanged(bool Paused) : SimEvent;

public sealed record SpeedChanged(int Speed) : SimEvent;
