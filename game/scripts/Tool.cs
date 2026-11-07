using Godot;

namespace AThousandGoblinsDeep;

/// <summary>The action a Selection applies. It stays picked until Escape, or its own key or button, puts it away.</summary>
public enum Tool
{
	None,
	Dig,
	DigStairs,
	Pile,
	GrubPit,
	FungusFarm,
	Brewery,
	Heap,
}

/// <summary>How each Tool presents itself: its name, its key, its colour on the map, and what it says about itself.</summary>
public static class ToolInfo
{
	public static readonly Tool[] DigTools = [Tool.Dig, Tool.DigStairs];
	public static readonly Tool[] RoomTools = [Tool.Pile, Tool.GrubPit, Tool.FungusFarm, Tool.Brewery, Tool.Heap];

	public static string Label(Tool tool) => tool switch
	{
		Tool.Dig => "Dig",
		Tool.DigStairs => "Stairs",
		Tool.Pile => "Pile",
		Tool.GrubPit => "Grub Pit",
		Tool.FungusFarm => "Fungus Farm",
		Tool.Brewery => "Brewery",
		Tool.Heap => "Heap",
		_ => "",
	};

	public static Key KeyOf(Tool tool) => tool switch
	{
		Tool.Dig => Key.D,
		Tool.DigStairs => Key.S,
		Tool.Pile => Key.P,
		Tool.GrubPit => Key.G,
		Tool.FungusFarm => Key.F,
		Tool.Brewery => Key.B,
		Tool.Heap => Key.H,
		_ => Key.None,
	};

	/// <summary>The colour the Tool leaves on the map: the Designation outline, the Stairs, or the Room's tint over Floor.</summary>
	public static Color Swatch(Tool tool) => tool switch
	{
		Tool.Dig => WarrenView.DesignationColor,
		Tool.DigStairs => WarrenView.StairsColor,
		Tool.Pile => OverFloor(WarrenView.PileTint),
		Tool.GrubPit => OverFloor(WarrenView.GrubPitTint),
		Tool.FungusFarm => OverFloor(WarrenView.FungusFarmTint),
		Tool.Brewery => OverFloor(WarrenView.BreweryTint),
		Tool.Heap => OverFloor(WarrenView.SleepingHeapTint),
		_ => Colors.Transparent,
	};

	static Color OverFloor(Color tint) => WarrenView.FloorColor.Lerp(new Color(tint.R, tint.G, tint.B), tint.A);

	public static string Tooltip(Tool tool) => tool switch
	{
		Tool.Dig => "Digs out whatever is in the way. The ground is not consulted.",
		Tool.DigStairs => "Digs a hole with ambitions. Stairs join this level to the one below.",
		Tool.Pile => "Heaps things in a pile. Thirty of each thing to a tile, counted very carefully.",
		Tool.GrubPit => "Sets a pit aside for Grub, which needs minding.",
		Tool.FungusFarm => "Plants a Fungus Farm, but only down the stairs, since Fungus will not grow where anyone can see it.",
		Tool.Brewery => "Sets up a Brewery, where a goblin stands over the Fungus until it gives in and becomes Grog.",
		Tool.Heap => "Lays out a heap to sleep on, which is a bed with the pretensions removed.",
		_ => "",
	};

	/// <summary>The short how-to under the palette for the picked Tool.</summary>
	public static string Hint(Tool tool) => tool switch
	{
		Tool.Dig => "Drag a box over solid ground to dig it out. Right-drag unsays it, and nobody mentions it again.",
		Tool.DigStairs => "Drag a box to dig stairs down a level. Down is where the good stuff is. Right-drag unsays it.",
		Tool.Pile => "Paint a Pile on dug floor. Loose things are hauled here and counted. Right-drag removes it.",
		Tool.GrubPit => "Paint a Grub Pit on floor. A goblin tends it, and Grub turns up, as Grub does. Right-drag removes it.",
		Tool.FungusFarm => "Paint a Fungus Farm below the Surface. A goblin tends it, and Fungus grows, sulkily. Right-drag removes it.",
		Tool.Brewery => "Paint a Brewery on floor. Fungus goes in, Grog comes out, and nobody asks how. Right-drag removes it.",
		Tool.Heap => "Paint a Sleeping Heap on floor. Goblins sleep twice as fast on it, and snore with pride. Right-drag removes it.",
		_ => "Pick a Tool, then drag a box on the map. Right-drag unsays a dig, Tool or no Tool. The Grunt takes it personally.",
	};
}
