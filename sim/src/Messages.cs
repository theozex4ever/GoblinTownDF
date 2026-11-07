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
/// Paints a Room on every Floor tile in the rectangle from (X0, Y0) to (X1, Y1) on level Z, corners in any order,
/// clipped to the map. A single click is a 1×1 rectangle. Stairs, Solid and Open air tiles are skipped, and so is a
/// tile that already holds a different Room. <c>PaintRoom(RoomKind.Pile, …)</c> is <see cref="PaintPile"/>.
/// </summary>
public sealed record PaintRoom(RoomKind Room, int Z, int X0, int Y0, int X1, int Y1) : Command;

/// <summary>
/// Removes the Room from every tile in the rectangle from (X0, Y0) to (X1, Y1) on level Z, corners in any order,
/// clipped to the map. A single click is a 1×1 rectangle. Tiles that hold no Room, or a different one, are skipped.
/// <c>RemoveRoom(RoomKind.Pile, …)</c> is <see cref="RemovePile"/>.
/// </summary>
public sealed record RemoveRoom(RoomKind Room, int Z, int X0, int Y0, int X1, int Y1) : Command;

/// <summary>
/// Paints a Pile on every Floor tile in the rectangle from (X0, Y0) to (X1, Y1) on level Z, corners in any order,
/// clipped to the map. A single click is a 1×1 rectangle. Stairs, Solid and Open air tiles are skipped, and so is
/// a tile holding more than <see cref="SimState.PileCapacity"/> of any material, whose items stay loose. Loose
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

/// <summary>A goblin began to shirk: it abandoned its Job to loaf on the spot. A transient fact for the view; nothing is logged.</summary>
public sealed record ShirkStarted(int Goblin) : SimEvent;

/// <summary>Another goblin consumed supplies and so cancelled this goblin's claimed Haul, which annoyed it. A transient fact for the view; nothing is logged.</summary>
public sealed record HaulCancelled(int Goblin) : SimEvent;

/// <summary>A goblin made contact with its target and a scuffle began: the instigator and the victim, by goblin index. A transient fact for the view; nothing is logged.</summary>
public sealed record ScuffleStarted(int Instigator, int Victim) : SimEvent;

/// <summary>A thief took its unit of Grub or Grog off a Pile: the goblin and the supply it now carries. A transient fact for the view; nothing is logged.</summary>
public sealed record TheftPickedUp(int Goblin, Material Supply) : SimEvent;
