namespace Goblins.Sim;

public enum TileKind : byte
{
	Solid,
	Floor,
	OpenAir,
	Stairs,
}

public enum Material : byte
{
	None,
	Dirt,
	Stone,
	Grub,
	Fungus,
	Grog,
}

/// <summary>A painted zone. A tile holds at most one Room.</summary>
public enum RoomKind : byte
{
	None,
	Pile,
	GrubPit,
}

public enum Role : byte
{
	Grunt,
}

public enum DesignationKind : byte
{
	None,
	DigStairs,
	Dig,
}

public enum GoblinTask : byte
{
	Idle,
	Walking,
	Digging,
	Wandering,
	Fetching,
	PickingUp,
	Carrying,
	Dropping,
	Seeking,
	Eating,
	SeekingGrog,
	Drinking,
	Approaching,
	Tending,
}
