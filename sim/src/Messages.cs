namespace Goblins.Sim;

/// <summary>An input from Godot to the sim.</summary>
public abstract record Command;

public sealed record SetPaused(bool Paused) : Command;

/// <summary>Speed is 1, 2 or 4. It changes ticks per second, never the tick length.</summary>
public sealed record SetSpeed(int Speed) : Command;

/// <summary>
/// Designates every tile in the rectangle from (X0, Y0) to (X1, Y1) on level Z, corners in any order,
/// clipped to the map. A single click is a 1×1 rectangle. Tiles that cannot take the designation are skipped.
/// </summary>
public sealed record Designate(DesignationKind Kind, int Z, int X0, int Y0, int X1, int Y1) : Command;

/// <summary>
/// Cancels every Designation in the rectangle from (X0, Y0) to (X1, Y1) on level Z, corners in any order,
/// clipped to the map. A single click is a 1×1 rectangle. Cancelling a tile mid-dig throws its progress away.
/// Tiles with no Designation are skipped.
/// </summary>
public sealed record CancelDesignation(int Z, int X0, int Y0, int X1, int Y1) : Command;

/// <summary>
/// Paints a Pile on every Floor tile in the rectangle from (X0, Y0) to (X1, Y1) on level Z, corners in any order,
/// clipped to the map. A single click is a 1×1 rectangle. Stairs, Solid and Open air tiles are skipped. Loose
/// items on a painted tile are stored on the spot.
/// </summary>
public sealed record PaintPile(int Z, int X0, int Y0, int X1, int Y1) : Command;

/// <summary>
/// Removes the Pile from every Pile tile in the rectangle from (X0, Y0) to (X1, Y1) on level Z, corners in any
/// order, clipped to the map. A single click is a 1×1 rectangle. The stock stays on the tile as loose items.
/// Tiles with no Pile are skipped.
/// </summary>
public sealed record RemovePile(int Z, int X0, int Y0, int X1, int Y1) : Command;

/// <summary>An output from the sim to Godot.</summary>
public abstract record SimEvent;

public sealed record PausedChanged(bool Paused) : SimEvent;

public sealed record SpeedChanged(int Speed) : SimEvent;

/// <summary>A tile was dug out. It carries the material the tile is made of; P2 turns that into hauling.</summary>
public sealed record TileDug(int X, int Y, int Z, Material Material) : SimEvent;
