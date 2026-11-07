using System.Collections.Generic;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>One Need meter of an inspected goblin.</summary>
public readonly record struct MeterReading(Need Need, int Value);

/// <summary>
/// One material's count on an inspected tile. <see cref="Full"/> marks a Pile count at capacity; <see cref="Waiting"/> marks
/// a loose material whose Haul waits for a Pile with room it can be carried to.
/// </summary>
public readonly record struct StockLine(Material Material, int Count, bool Full, bool Waiting);

/// <summary>
/// What the Inspector shows: a <see cref="Title"/> (a goblin's Role or what the tile is), what it is <see cref="Doing"/> (a Task in
/// plain words, or what is marked on the tile), the goblin's meters, the tile's stock, and a <see cref="Note"/> in the goblin voice.
/// An empty Title means nothing is under the cursor.
/// </summary>
public sealed record Inspection(string Title, string Doing, IReadOnlyList<MeterReading> Meters, IReadOnlyList<StockLine> Stock, string Note)
{
	public static readonly Inspection Nothing = new("", "", [], [], "");
}

/// <summary>Reads what is on one tile for the Inspector. Reads sim state; never writes it.</summary>
public static class Inspector
{
	/// <summary>The goblin on the tile, the lowest-numbered if several, ahead of anything else there; else the tile itself.</summary>
	public static Inspection Inspect(SimState s, int tile)
	{
		var goblin = GoblinHover.GoblinAt(s, tile);
		return goblin >= 0 ? InspectGoblin(s, goblin) : InspectTile(s, tile);
	}

	static Inspection InspectGoblin(SimState s, int goblin)
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
		MeterReading[] meters = [new(Need.Grub, grub), new(Need.Grog, grog), new(Need.Sleep, sleep)];
		return new Inspection(((Role)s.GoblinRole[goblin]).ToString(), TaskWords(s, goblin), meters, [], mood);
	}

	/// <summary>A goblin's Task in plain words, naming what it carries or where it is headed.</summary>
	public static string TaskWords(SimState s, int goblin)
	{
		var carried = (Material)s.GoblinCarry[goblin];
		var jobTile = s.GoblinJobTile[goblin];
		var position = s.TileIndex(s.GoblinX[goblin], s.GoblinY[goblin], s.GoblinZ[goblin]);
		return (GoblinTask)s.GoblinTask[goblin] switch
		{
			GoblinTask.Idle => "Standing about, between jobs",
			GoblinTask.Wandering => "Wandering about, looking busy",
			GoblinTask.Walking => "Off to dig",
			GoblinTask.Digging => "Digging",
			GoblinTask.Fetching => "Off to fetch something loose",
			GoblinTask.PickingUp => "Picking something up",
			GoblinTask.Carrying when NeedsSystem.IsWaitingCarrier(s, goblin) => $"Holding {Word(carried)}, with nowhere to put it",
			GoblinTask.Carrying => $"Hauling {Word(carried)} to a Pile",
			GoblinTask.Dropping => $"Putting {Word(carried)} on a Pile",
			GoblinTask.Seeking => "Off to find Grub",
			GoblinTask.Eating => "Eating",
			GoblinTask.SeekingGrog => "Off for a drink",
			GoblinTask.Drinking => "Drinking",
			GoblinTask.Approaching => jobTile >= 0 ? $"Off to tend the {RoomName(s.RoomAt(jobTile))}" : "Off to tend something",
			GoblinTask.Tending => jobTile >= 0 ? $"Tending the {RoomName(s.RoomAt(jobTile))}" : "Tending",
			GoblinTask.SeekingHeap => "Looking for a Heap to sleep on",
			GoblinTask.Sleeping => s.RoomAt(position) == RoomKind.SleepingHeap ? "Asleep on a Heap" : "Asleep on the bare floor",
			GoblinTask.Gathering => "Off to fetch Fungus for brewing",
			GoblinTask.Taking => "Taking Fungus for brewing",
			GoblinTask.Bringing => "Taking Fungus to the Brewery",
			GoblinTask.Brewing => "Brewing",
			var task => task.ToString(),
		};
	}

	static Inspection InspectTile(SimState s, int tile)
	{
		var stock = new List<StockLine>();
		for (var material = Material.Dirt; (int)material <= SimState.CountedMaterials; material++)
		{
			var count = s.ItemCount(tile, material);
			if (count <= 0)
				continue;
			var stored = s.IsPile(tile);
			stock.Add(new StockLine(material, count, stored && count >= SimState.PileCapacity, !stored && !s.IsHaulReachable(tile, material)));
		}

		var room = s.RoomAt(tile);
		var kind = s.KindAt(tile);
		var title = kind switch
		{
			TileKind.Solid => $"Solid {Word(s.MaterialAt(tile))}",
			TileKind.Stairs => "Stairs",
			TileKind.OpenAir => "Open air",
			_ when room != RoomKind.None => RoomName(room),
			_ => "Floor",
		};

		var doing = s.DesignationAt(tile) switch
		{
			DesignationKind.Dig => "Marked for digging",
			DesignationKind.DigStairs => "Marked for stairs",
			_ => RoomDoing(s, tile, room),
		};
		var progress = s.DigProgress[tile];
		if (s.DesignationAt(tile) != DesignationKind.None && progress > 0)
			doing += $", {progress * 100 / MaterialRules.DigTicks(s.MaterialAt(tile))}% dug";

		var note = s.DesignationAt(tile) != DesignationKind.None && !s.IsReachable(tile) ? "No goblin can reach it yet."
			: room == RoomKind.Pile ? stock.Count == 0 ? "An empty Pile, waiting with great dignity." : "Counted solemnly."
			: stock.Exists(line => line.Waiting) ? "Greyed things are waiting for a Pile with room. Patiently, for now."
			: "";
		return new Inspection(title, doing, [], stock, note);
	}

	static string RoomDoing(SimState s, int tile, RoomKind room) => room switch
	{
		RoomKind.Brewery when BrewSystem.IsWaiting(s, tile) => "Waiting for a Pile with room for Grog",
		RoomKind.GrubPit or RoomKind.FungusFarm when s.FarmUnspawned[tile] > 0 =>
			$"{s.FarmUnspawned[tile]} {FarmSystem.CropOf(room)} still to come",
		RoomKind.GrubPit or RoomKind.FungusFarm when FarmSystem.IsTendOffered(s, tile) => "Wants tending",
		_ => "",
	};

	/// <summary>A Room's name as the Tools and the glossary write it.</summary>
	public static string RoomName(RoomKind room) => room switch
	{
		RoomKind.GrubPit => "Grub Pit",
		RoomKind.FungusFarm => "Fungus Farm",
		RoomKind.SleepingHeap => "Sleeping Heap",
		_ => room.ToString(),
	};

	// Dirt and stone are common nouns; Grub, Fungus and Grog are names.
	static string Word(Material material) => material is Material.Dirt or Material.Stone ? material.ToString().ToLowerInvariant() : material.ToString();
}
