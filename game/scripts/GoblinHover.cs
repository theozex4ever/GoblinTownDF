using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>What the hover line says about the goblin under the cursor. Reads sim state; never writes it.</summary>
public static class GoblinHover
{
	/// <summary>The lowest-numbered goblin standing on the tile, or -1 when none does.</summary>
	public static int GoblinAt(SimState s, int tile)
	{
		for (var g = 0; g < s.GoblinCount; g++)
			if (s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]) == tile)
				return g;
		return -1;
	}

	/// <summary>The hover line for a goblin: its Role and its Grub, Grog and Sleep meters, with a word on how it is bearing up.</summary>
	public static string HoverLine(SimState s, int goblin)
	{
		var grub = s.GoblinGrub[goblin];
		var grog = s.GoblinGrog[goblin];
		var sleep = s.GoblinSleep[goblin];
		var craving = NeedRules.LeadingCraving(grub, grog, sleep);
		var mood = (GoblinTask)s.GoblinTask[goblin] == GoblinTask.Sleeping ? "Fast asleep. Hunger and thirst may take a number."
			: NeedRules.IsStarving(grub) ? "Starving, and bearing it at half speed with great dignity."
			: craving == Need.Sleep ? "Thinking about nothing but a lie-down, on anything flat."
			: craving != Need.None ? $"Thinking about nothing but {craving}."
			: "At peace with the world, and with lunch.";
		return $"A {(Role)s.GoblinRole[goblin]}. Grub {grub}/{NeedRules.MeterMax}, Grog {grog}/{NeedRules.MeterMax}, Sleep {sleep}/{NeedRules.MeterMax}. {mood}";
	}
}
