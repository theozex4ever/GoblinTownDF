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
	SleepingHeap,
	FungusFarm,
	Brewery,
}

/// <summary>
/// What a goblin can crave. Grub and Grog are met with a Material of the same name; Sleep is met with no supply at all,
/// only a place to lie down. The order is the tie-break when meters are equal: Grub, then Grog, then Sleep.
/// </summary>
public enum Need : byte
{
	None,
	Grub,
	Grog,
	Sleep,
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
	SeekingHeap,
	Sleeping,
	Gathering,
	Taking,
	Bringing,
	Brewing,
	Shirking,
	Pursuing,
	Scuffling,
	Scuffled,
	Recovering,
	Sneaking,
	Pilfering,
	MakingOff,
	Scoffing,
	Stalled,
}
