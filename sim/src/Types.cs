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
}
