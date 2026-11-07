using System;
using System.Linq;
using Godot;
using Goblins.Sim;
using Material = Goblins.Sim.Material;

namespace AThousandGoblinsDeep;

/// <summary>
/// Checks where carried-material squares, loose squares and Pile counts are drawn. The layout is pure geometry and always runs. The pixel checks
/// render the warren into an image and read pixels, so they need a real renderer: with --headless they print a skip
/// and report the pixel scenarios as skipped, which means a run with no display does not check drawing.
/// </summary>
public partial class CarryDrawChecks : Node
{
	static readonly Color DirtSquare = new(0.55f, 0.36f, 0.18f);
	static readonly Color StoneSquare = new(0.36f, 0.40f, 0.48f);
	static readonly Material[] Materials = Enum.GetValues<Material>()[1..];
	static readonly Color GoblinBody = new(0.35f, 0.75f, 0.30f);

	public override async void _Ready()
	{
		try
		{
			LayoutChecks();
			ColourChecks();
			LooseLayoutChecks();
			PipLayoutCheck();
			ZzzLayoutCheck();
			ShirkLayoutCheck();
			FightLayoutCheck();
			ThiefLayoutCheck();

			if (DisplayServer.GetName() == "headless")
			{
				GD.Print("Carry draw checks: layout checks passed; the 13 pixel scenarios were SKIPPED, not passed: no renderer under --headless.");
				GetTree().Quit();
				return;
			}

			await CoveredByBodyCheck();
			await FullTileCheck();
			await FullCountCheck();
			await TwoCountsCheck();
			await LooseSquaresCheck();
			await NeedMarksCheck();
			await RoomTintCheck();
			await SleepMarksCheck();
			await FungusFarmTintCheck();
			await BreweryTintCheck();
			await ShirkMarkCheck();
			await FightMarkCheck();
			await ThiefMarkCheck();
			GD.Print("Carry draw checks passed (layout, colours, count order, 13 pixel scenarios).");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	// For every number of carriers on one tile up to the goblin cap, each square lies inside the marker and no two overlap.
	static void LayoutChecks()
	{
		var marker = new Rect2(3, 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		for (var carriers = 1; carriers <= SimState.MaxGoblins; carriers++)
		{
			var rects = new Rect2[carriers];
			for (var slot = 0; slot < carriers; slot++)
			{
				rects[slot] = WarrenView.CarriedRect(marker, slot, carriers);
				Require(rects[slot].Size.X >= 1 && rects[slot].Size.Y >= 1, $"{carriers} carriers: slot {slot} is empty.");
				Require(marker.Encloses(rects[slot]), $"{carriers} carriers: slot {slot} {rects[slot]} leaves the marker {marker}.");
				for (var other = 0; other < slot; other++)
					Require(!rects[slot].Intersects(rects[other]), $"{carriers} carriers: slots {other} and {slot} overlap.");
			}
		}
	}

	// Goblin 0 carries dirt and goblin 2 stands on its tile, drawn after it in goblin order.
	async System.Threading.Tasks.Task CoveredByBodyCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		(s.GoblinX[2], s.GoblinY[2]) = (s.GoblinX[0], s.GoblinY[0]);
		GoblinTasks.FinishPickUp(s, 0, Goblins.Sim.Material.Dirt);
		var image = await Render(sim);
		var square = Square(s, 0, slot: 0, carriers: 1);
		ExpectColor(image, square.X, square.Y, DirtSquare, "A carrier's square was covered by a body on its tile.");
		ExpectColor(image, square.X, square.Y - 5, GoblinBody, "The goblin body was not drawn around the square.");
	}

	// Every goblin carries on one tile: each slot shows its goblin's material, inside the marker, and the tiles around stay clean.
	async System.Threading.Tasks.Task FullTileCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var (tileX, tileY) = (s.GoblinX[0], s.GoblinY[0]);
		for (var g = 0; g < s.GoblinCount; g++)
		{
			(s.GoblinX[g], s.GoblinY[g]) = (tileX, tileY);
			GoblinTasks.FinishPickUp(s, g, Materials[g % Materials.Length]);
		}
		var image = await Render(sim);

		for (var g = 0; g < s.GoblinCount; g++)
		{
			var square = Square(s, g, slot: g, carriers: s.GoblinCount);
			ExpectColor(image, square.X, square.Y, WarrenView.MaterialColor(Materials[g % Materials.Length]), $"Carrier {g}'s slot does not show its material.");
		}

		var cell = WarrenView.CellSize;
		for (var y = (tileY - 1) * cell; y < (tileY + 2) * cell; y++)
		for (var x = (tileX - 1) * cell; x < (tileX + 2) * cell; x++)
		{
			var inMarker = x >= tileX * cell + 3 && x < (tileX + 1) * cell - 3 && y >= tileY * cell + 3 && y < (tileY + 1) * cell - 3;
			if (inMarker)
				continue;
			var pixel = image.GetPixel(x, y);
			Require(!Materials.Any(m => IsNear(pixel, WarrenView.MaterialColor(m))), $"A carried colour was drawn at ({x}, {y}), outside the carrier's marker.");
		}
	}

	// A Pile count is highlighted at 30 and only for its own material. A full count comes first, so the chip is always
	// in the top slot: 30 dirt with 5 stone, and 5 dirt with 30 stone, where stone moves ahead of the smaller dirt.
	async System.Threading.Tasks.Task FullCountCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 4, 4, 5, 4));
		s.AddItems(s.TileIndex(4, 4, 0), Material.Dirt, SimState.PileCapacity);
		s.AddItems(s.TileIndex(4, 4, 0), Material.Stone, 5);
		s.AddItems(s.TileIndex(5, 4, 0), Material.Dirt, 5);
		s.AddItems(s.TileIndex(5, 4, 0), Material.Stone, SimState.PileCapacity);
		var image = await Render(sim);

		foreach (var tileX in new[] { 4, 5 })
		{
			Require(HasChip(image, tileX, 4, slot: 0), $"Tile {tileX}: the full count is not highlighted in the top slot.");
			Require(!HasChip(image, tileX, 4, slot: 1), $"Tile {tileX}: the count below the full one is highlighted.");
		}
	}

	// Five materials stored on one tile: only the two best are drawn, each in its own colour. Tile 4 has two full counts
	// ahead of a larger count that is not full; tile 5 has no full count, so the two largest show, in Material order on a tie.
	async System.Threading.Tasks.Task TwoCountsCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new PaintPile(0, 4, 4, 5, 4));
		var full = s.TileIndex(4, 4, 0);
		(Material Material, int Count)[] fullTile = [(Material.Dirt, 29), (Material.Stone, 28), (Material.Grub, 30), (Material.Fungus, 1), (Material.Grog, 30)];
		foreach (var (material, count) in fullTile)
			s.AddItems(full, material, count);
		var plain = s.TileIndex(5, 4, 0);
		(Material Material, int Count)[] plainTile = [(Material.Dirt, 3), (Material.Stone, 20), (Material.Grub, 7), (Material.Fungus, 20), (Material.Grog, 2)];
		foreach (var (material, count) in plainTile)
			s.AddItems(plain, material, count);
		var image = await Render(sim);

		ExpectCount(image, 4, slot: 0, Material.Grub, chip: true);
		ExpectCount(image, 4, slot: 1, Material.Grog, chip: true);
		ExpectCount(image, 5, slot: 0, Material.Stone, chip: false);
		ExpectCount(image, 5, slot: 1, Material.Fungus, chip: false);
	}

	// Loose units of every material on one tile each get their own square, in their own colour. Each has a Pile with room
	// to go to, so none is greyed out as waiting.
	async System.Threading.Tasks.Task LooseSquaresCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var tile = s.TileIndex(4, 4, 0);
		sim.Apply(new PaintPile(0, 10, 10, 10, 10));
		foreach (var material in Materials)
			s.AddItems(tile, material, 1);
		sim.RecheckReachability();
		var image = await Render(sim);

		var rect = new Rect2(4 * WarrenView.CellSize, 4 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize);
		foreach (var material in Materials)
		{
			var center = WarrenView.LooseSquare(rect, (int)material - 1).GetCenter();
			ExpectColor(image, (int)center.X, (int)center.Y, WarrenView.MaterialColor(material), $"The loose {material} square is missing or the wrong colour.");
		}
	}

	// A goblin craving Grub wears a Grub-coloured pip and one craving Grog a Grog-coloured one; with both, the lower meter's
	// colour shows (Grub on a tie). A Starving goblin also has a tinted body, a fed one neither, and Grog at 0 tints nothing.
	// The pip is drawn over a carried square without hiding it.
	// A Grub Pit tile is tinted over the floor, and the Grub lying loose on it still shows over the tint.
	async System.Threading.Tasks.Task RoomTintCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 10, 10, 11, 10));
		sim.Apply(new PaintPile(0, 20, 20, 20, 20));
		s.AddItems(s.TileIndex(11, 10, 0), Material.Grub, 1);
		sim.RecheckReachability();
		var image = await Render(sim);

		var floor = new Color(0.62f, 0.62f, 0.64f);
		var tint = WarrenView.GrubPitTint;
		var tinted = floor * (1 - tint.A) + tint * tint.A;
		var rect = new Rect2(10 * WarrenView.CellSize, 10 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize);
		var centre = rect.GetCenter();
		ExpectColor(image, (int)centre.X, (int)centre.Y, new Color(tinted.R, tinted.G, tinted.B), "A Grub Pit tile is not tinted.");
		ExpectColor(image, 12 * WarrenView.CellSize + 9, 10 * WarrenView.CellSize + 9, floor, "A tile beside the Grub Pit is tinted.");
		var square = WarrenView.LooseSquare(new Rect2(11 * WarrenView.CellSize, 10 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize), (int)Material.Grub - 1).GetCenter();
		ExpectColor(image, (int)square.X, (int)square.Y, WarrenView.MaterialColor(Material.Grub), "The Grub loose on a Grub Pit is hidden by its tint.");
	}

	// A Fungus Farm tile on z1 is tinted over the floor, its tint differs from the Grub Pit's, and the Fungus lying loose on it still shows over the tint.
	async System.Threading.Tasks.Task FungusFarmTintCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		for (var x = 10; x <= 12; x++)
			s.TileKinds[s.TileIndex(x, 10, 1)] = (byte)TileKind.Floor;
		// Stairs down from the Surface and a Pile up there, so the Fungus has a Haul to wait for rather than greying out.
		s.TileKinds[s.TileIndex(13, 10, 0)] = (byte)TileKind.Stairs;
		s.TileKinds[s.TileIndex(13, 10, 1)] = (byte)TileKind.Stairs;
		sim.Apply(new PaintPile(0, 20, 20, 20, 20));
		sim.Apply(new PaintRoom(RoomKind.FungusFarm, 1, 10, 10, 11, 10));
		s.AddItems(s.TileIndex(11, 10, 1), Material.Fungus, 1);
		sim.RecheckReachability();
		var image = await Render(sim, viewZ: 1);

		Require(!IsNear(WarrenView.FungusFarmTint, WarrenView.GrubPitTint), "The Fungus Farm and Grub Pit tints are the same.");
		var floor = new Color(0.62f, 0.62f, 0.64f);
		var tint = WarrenView.FungusFarmTint;
		var tinted = floor * (1 - tint.A) + tint * tint.A;
		var centre = new Rect2(10 * WarrenView.CellSize, 10 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize).GetCenter();
		ExpectColor(image, (int)centre.X, (int)centre.Y, new Color(tinted.R, tinted.G, tinted.B), "A Fungus Farm tile is not tinted.");
		ExpectColor(image, 12 * WarrenView.CellSize + 9, 10 * WarrenView.CellSize + 9, floor, "A tile beside the Fungus Farm is tinted.");
		var square = WarrenView.LooseSquare(new Rect2(11 * WarrenView.CellSize, 10 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize), (int)Material.Fungus - 1).GetCenter();
		ExpectColor(image, (int)square.X, (int)square.Y, WarrenView.MaterialColor(Material.Fungus), "The Fungus loose on a Fungus Farm is hidden by its tint.");
	}

	// A Brewery tile is tinted over the floor in its own colour; it greys while no Pile in reach has room for Grog, and goes back to its
	// colour when one is painted. The Grog lying loose on it shows over either tint.
	async System.Threading.Tasks.Task BreweryTintCheck()
	{
		var floor = new Color(0.62f, 0.62f, 0.64f);
		Color Over(Color tint) { var c = floor * (1 - tint.A) + tint * tint.A; return new Color(c.R, c.G, c.B); }
		var cell = WarrenView.CellSize;
		// The bottom right corner of the tile: the loose squares fill a three-by-three grid and the ninth slot is no material's.
		var centre = new Vector2(10 * cell + cell - 3, 10 * cell + cell - 3);
		var loose = WarrenView.LooseSquare(new Rect2(10 * cell, 10 * cell, cell, cell), (int)Material.Grog - 1).GetCenter();

		Require(!IsNear(WarrenView.BreweryTint, WarrenView.GrubPitTint) && !IsNear(WarrenView.BreweryTint, WarrenView.FungusFarmTint)
			&& !IsNear(WarrenView.BreweryTint, WarrenView.SleepingHeapTint) && !IsNear(WarrenView.BreweryTint, WarrenView.WaitingBreweryTint),
			"The Brewery tint is the same as another Room's, or as its waiting tint.");

		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new PaintRoom(RoomKind.Brewery, 0, 10, 10, 11, 10));
		s.AddItems(s.TileIndex(10, 10, 0), Material.Grog, 1);
		sim.RecheckReachability();
		var waiting = await Render(sim);
		ExpectColor(waiting, (int)centre.X, (int)centre.Y, Over(WarrenView.WaitingBreweryTint), "A Brewery with no Pile for its Grog is not greyed out.");
		ExpectColor(waiting, (int)loose.X, (int)loose.Y, WarrenView.WaitingHaulColor, "The Grog waiting on a Brewery with no Pile is not greyed.");

		sim.Apply(new PaintPile(0, 20, 20, 20, 20));
		var ready = await Render(sim);
		ExpectColor(ready, (int)centre.X, (int)centre.Y, Over(WarrenView.BreweryTint), "A Brewery with a Pile for its Grog is not tinted.");
		ExpectColor(ready, 12 * cell + 9, 10 * cell + 9, floor, "A tile beside the Brewery is tinted.");
		ExpectColor(ready, (int)loose.X, (int)loose.Y, WarrenView.MaterialColor(Material.Grog), "The Grog loose on a Brewery is hidden by its tint.");
	}

	// A Sleeping Heap tile is tinted over the floor, a sleeping goblin wears "zzz" and an awake one does not, and a goblin craving Sleep
	// wears a Sleep-coloured pip (Grub's on a three-way tie, Sleep's when it is lowest), over its "zzz" when it craves in its sleep.
	async System.Threading.Tasks.Task SleepMarksCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new PaintRoom(RoomKind.SleepingHeap, 0, 10, 10, 10, 10));
		for (var g = 0; g < s.GoblinCount; g++)
			(s.GoblinGrub[g], s.GoblinGrog[g], s.GoblinSleep[g]) = (100, 100, 100);
		(s.GoblinSleep[0], s.GoblinSleep[1], s.GoblinSleep[3]) = (50, 15, 15);
		GoblinTasks.BeginSleep(s, 0, dropTile: -1, asleep: true);
		GoblinTasks.BeginSleep(s, 1, dropTile: -1, asleep: true);
		(s.GoblinGrub[2], s.GoblinGrog[2], s.GoblinSleep[2]) = (20, 20, 20);
		(s.GoblinGrub[4], s.GoblinGrog[4], s.GoblinSleep[4]) = (25, 25, 10);
		var image = await Render(sim);

		var floor = new Color(0.62f, 0.62f, 0.64f);
		var tint = WarrenView.SleepingHeapTint;
		var tinted = floor * (1 - tint.A) + tint * tint.A;
		var centre = new Rect2(10 * WarrenView.CellSize, 10 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize).GetCenter();
		ExpectColor(image, (int)centre.X, (int)centre.Y, new Color(tinted.R, tinted.G, tinted.B), "A Sleeping Heap tile is not tinted.");
		ExpectColor(image, 11 * WarrenView.CellSize + 9, 10 * WarrenView.CellSize + 9, floor, "A tile beside the Heap is tinted.");

		Rect2 Marker(int g) => new(s.GoblinX[g] * WarrenView.CellSize + 3, s.GoblinY[g] * WarrenView.CellSize + 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		bool HasZzz(int g)
		{
			var marker = Marker(g);
			var baseline = WarrenView.ZzzBaseline(marker);
			for (var y = (int)baseline.Y - (WarrenView.ZzzFontSize - 2); y <= (int)baseline.Y; y++)
			for (var x = (int)baseline.X; x < (int)baseline.X + 10; x++)
				if (IsNear(image.GetPixel(x, y), WarrenView.ZzzColor))
					return true;
			return false;
		}
		Color PipAt(int g) => image.GetPixel((int)WarrenView.PipRect(Marker(g)).GetCenter().X, (int)WarrenView.PipRect(Marker(g)).GetCenter().Y);

		Require(HasZzz(0), "A sleeping goblin has no \"zzz\".");
		Require(HasZzz(1), "A goblin asleep and craving Sleep has no \"zzz\".");
		Require(!HasZzz(2) && !HasZzz(3), "An awake goblin wears \"zzz\".");
		Require(IsNear(PipAt(0), GoblinBody), "A sleeping goblin with a comfortable meter has a pip.");
		Require(IsNear(PipAt(1), WarrenView.NeedColor(Need.Sleep)), "A goblin craving Sleep in its sleep has no Sleep pip over its \"zzz\".");
		Require(IsNear(PipAt(3), WarrenView.NeedColor(Need.Sleep)), "A goblin craving Sleep has no Sleep pip.");
		Require(IsNear(PipAt(2), WarrenView.NeedColor(Need.Grub)), "On a three-way tie the pip should be Grub's.");
		Require(IsNear(PipAt(4), WarrenView.NeedColor(Need.Sleep)), "With the lowest meter Sleep's pip should show over Grub's and Grog's.");
		Require(!IsNear(WarrenView.NeedColor(Need.Sleep), WarrenView.NeedColor(Need.Grub)) && !IsNear(WarrenView.NeedColor(Need.Sleep), WarrenView.NeedColor(Need.Grog)),
			"The Sleep pip is too close to the others in colour.");
	}

	// The shirk band lies inside the goblin marker and clear of the pip in its top right corner.
	static void ShirkLayoutCheck()
	{
		var marker = new Rect2(3, 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		var band = WarrenView.ShirkBandRect(marker);
		Require(marker.Encloses(band), $"The shirk band {band} leaves its marker.");
		Require(!band.Intersects(WarrenView.PipRect(marker)), "The shirk band overlaps the pip.");
	}

	// A shirking goblin wears a lilac band across its body and a lilac ring round it; one that works does not; and a shirker that craves
	// still wears its Need pip, over the band's reach.
	async System.Threading.Tasks.Task ShirkMarkCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		for (var g = 0; g < s.GoblinCount; g++)
			(s.GoblinGrub[g], s.GoblinGrog[g], s.GoblinSleep[g]) = (100, 100, 100);
		GoblinTasks.BeginShirk(s, 0, dropTile: -1);
		GoblinTasks.BeginShirk(s, 2, dropTile: -1);
		s.GoblinGrub[2] = 20;
		var image = await Render(sim);

		Rect2 Marker(int g) => new(s.GoblinX[g] * WarrenView.CellSize + 3, s.GoblinY[g] * WarrenView.CellSize + 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		Color Band(int g) { var c = WarrenView.ShirkBandRect(Marker(g)).GetCenter(); return image.GetPixel((int)c.X, (int)c.Y); }
		Color Ring(int g) => image.GetPixel((int)Marker(g).Position.X - 2, (int)Marker(g).GetCenter().Y);

		Require(IsNear(Band(0), WarrenView.ShirkColor), "A shirking goblin has no band across its body.");
		Require(IsNear(Ring(0), WarrenView.ShirkColor), "A shirking goblin has no ring round it.");
		Require(IsNear(Band(1), GoblinBody) && !IsNear(Ring(1), WarrenView.ShirkColor), "A goblin that is not shirking wears the shirk marks.");
		var pip = WarrenView.PipRect(Marker(2)).GetCenter();
		Require(IsNear(image.GetPixel((int)pip.X, (int)pip.Y), WarrenView.NeedColor(Need.Grub)), "A shirking goblin that craves Grub has lost its pip.");
		Require(IsNear(Band(2), WarrenView.ShirkColor), "A shirking goblin that craves has lost its band.");
	}

	// The fight ring and cross lie inside their tile, and the fight colours are told apart from a shirk's.
	static void FightLayoutCheck()
	{
		var tile = new Rect2(0, 0, WarrenView.CellSize, WarrenView.CellSize);
		var marker = new Rect2(3, 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		Require(tile.Encloses(marker.Grow(2)), "The fight ring leaves its tile.");
		Require(WarrenView.FightRingColor(GoblinTask.Pursuing) == WarrenView.FightColor && WarrenView.FightRingColor(GoblinTask.Scuffling) == WarrenView.FightColor
			&& WarrenView.FightRingColor(GoblinTask.Scuffled) == WarrenView.FightColor && WarrenView.FightRingColor(GoblinTask.Recovering) == WarrenView.FightRestColor,
			"A stage of a fight wears the wrong ring.");
		Require(WarrenView.FightRingColor(GoblinTask.Idle) is null && WarrenView.FightRingColor(GoblinTask.Shirking) is null, "A goblin in no fight wears a fight ring.");
		foreach (var fight in new[] { WarrenView.FightColor, WarrenView.FightRestColor })
			Require(Math.Abs(fight.R - WarrenView.ShirkColor.R) + Math.Abs(fight.G - WarrenView.ShirkColor.G) + Math.Abs(fight.B - WarrenView.ShirkColor.B) > 0.3f,
				"A fight's ring is too close to a shirk's in colour.");
	}

	// A goblin pursuing, scuffling or being scuffled wears an orange ring, and a scuffler an orange cross over its body; a recovering one wears the banked-down ring;
	// one in no fight wears none; and a fighter that craves still wears its Need pip.
	async System.Threading.Tasks.Task FightMarkCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var places = new[] { (3, 3), (3, 12), (10, 10), (11, 10), (6, 20), (6, 21), (20, 6) };
		for (var g = 0; g < s.GoblinCount; g++)
		{
			(s.GoblinGrub[g], s.GoblinGrog[g], s.GoblinSleep[g]) = (100, 100, 100);
			(s.GoblinX[g], s.GoblinY[g]) = places[g];
		}
		GoblinTasks.BeginPursuit(s, 0, 1, dropTile: -1);
		GoblinTasks.BeginPursuit(s, 2, 3, dropTile: -1);
		GoblinTasks.BeginScuffle(s, 2, 3, victimDropTile: -1);
		GoblinTasks.BeginPursuit(s, 4, 5, dropTile: -1);
		GoblinTasks.BeginScuffle(s, 4, 5, victimDropTile: -1);
		GoblinTasks.FinishScuffle(s, 4);
		s.GoblinGrub[2] = 20;
		var image = await Render(sim);

		Rect2 Marker(int g) => new(s.GoblinX[g] * WarrenView.CellSize + 3, s.GoblinY[g] * WarrenView.CellSize + 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		Color Ring(int g) => image.GetPixel((int)Marker(g).Position.X - 2, (int)Marker(g).GetCenter().Y);
		Color Centre(int g) => image.GetPixel((int)Marker(g).GetCenter().X, (int)Marker(g).GetCenter().Y);

		Require(IsNear(Ring(0), WarrenView.FightColor), "A pursuing goblin has no fight ring.");
		Require(IsNear(Centre(0), GoblinBody), "A pursuing goblin wears the scuffle cross.");
		Require(IsNear(Ring(2), WarrenView.FightColor) && IsNear(Ring(3), WarrenView.FightColor), "A scuffling goblin or its victim has no fight ring.");
		Require(IsNear(Centre(2), WarrenView.FightColor) && IsNear(Centre(3), WarrenView.FightColor), "A scuffling goblin or its victim has no cross over its body.");
		Require(IsNear(Ring(4), WarrenView.FightRestColor) && IsNear(Ring(5), WarrenView.FightRestColor), "A recovering goblin has no banked-down ring.");
		Require(IsNear(Centre(4), GoblinBody), "A recovering goblin wears the scuffle cross.");
		Require(!IsNear(Ring(1), WarrenView.FightColor), "The target of a pursuit wears a fight ring before contact.");
		Require(!IsNear(Ring(6), WarrenView.FightColor) && !IsNear(Ring(6), WarrenView.FightRestColor), "A goblin in no fight wears a fight ring.");
		var pip = WarrenView.PipRect(Marker(2)).GetCenter();
		Require(IsNear(image.GetPixel((int)pip.X, (int)pip.Y), WarrenView.NeedColor(Need.Grub)), "A scuffling goblin that craves Grub has lost its pip under the cross.");
	}

	// The thief's ring and mask lie inside their tile, the mask clears the square a thief carries and the pip, and the thief's colour is told apart from a shirk's and a fight's.
	static void ThiefLayoutCheck()
	{
		var tile = new Rect2(0, 0, WarrenView.CellSize, WarrenView.CellSize);
		var marker = new Rect2(3, 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		Require(tile.Encloses(marker.Grow(2)), "The thief's ring leaves its tile.");
		var mask = WarrenView.ThiefMaskRect(marker);
		Require(marker.Encloses(mask), $"The thief's mask {mask} leaves its marker.");
		Require(!mask.Intersects(WarrenView.CarriedRect(marker, 0, 1)), "The thief's mask covers the unit it carries.");
		Require(!mask.Intersects(WarrenView.ShirkBandRect(marker)), "The thief's mask overlaps a shirk's band.");
		foreach (var other in new[] { WarrenView.ShirkColor, WarrenView.FightColor, WarrenView.FightRestColor, WarrenView.GoblinColor, WarrenView.MaterialColor(Material.Grub), WarrenView.MaterialColor(Material.Grog) })
			Require(Math.Abs(WarrenView.ThiefColor.R - other.R) + Math.Abs(WarrenView.ThiefColor.G - other.G) + Math.Abs(WarrenView.ThiefColor.B - other.B) > 0.3f,
				"The thief's colour is too close to another cue.");
	}

	// Every stage of a theft wears a ring and a mask in the thief's colour; the unit a thief carries still shows in its own colour over the body and the Need pip still shows;
	// a goblin that is not stealing wears neither.
	async System.Threading.Tasks.Task ThiefMarkCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var places = new[] { (3, 3), (3, 12), (10, 10), (11, 10), (6, 20), (6, 21), (20, 6) };
		for (var g = 0; g < s.GoblinCount; g++)
		{
			(s.GoblinGrub[g], s.GoblinGrog[g], s.GoblinSleep[g]) = (100, 100, 100);
			(s.GoblinX[g], s.GoblinY[g]) = places[g];
		}
		Array.Clear(s.TileItems);
		var far = s.TileIndex(25, 25, 0);
		// Goblin 0 is on its way to a Pile, goblin 1 is making off with Grub, goblin 2 is making off with Grog and craves Grub.
		GoblinTasks.BeginTheft(s, 0, far, Material.Grub, far + 1, dropTile: -1, atSource: false);
		GoblinTasks.BeginTheft(s, 1, far, Material.Grub, far + 1, dropTile: -1, atSource: true);
		GoblinTasks.FinishPilfer(s, 1);
		GoblinTasks.BeginTheft(s, 2, far, Material.Grog, far + 1, dropTile: -1, atSource: true);
		GoblinTasks.FinishPilfer(s, 2);
		s.GoblinGrub[2] = 20;
		var image = await Render(sim);

		Rect2 Marker(int g) => new(s.GoblinX[g] * WarrenView.CellSize + 3, s.GoblinY[g] * WarrenView.CellSize + 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		Color Ring(int g) => image.GetPixel((int)Marker(g).Position.X - 2, (int)Marker(g).GetCenter().Y);
		Color Mask(int g) { var c = WarrenView.ThiefMaskRect(Marker(g)).GetCenter(); return image.GetPixel((int)c.X, (int)c.Y); }
		Color Centre(int g) => image.GetPixel((int)Marker(g).GetCenter().X, (int)Marker(g).GetCenter().Y);

		foreach (var thief in new[] { 0, 1, 2 })
			Require(IsNear(Ring(thief), WarrenView.ThiefColor) && IsNear(Mask(thief), WarrenView.ThiefColor), $"Thief {thief} has lost its ring or mask.");
		Require(IsNear(Centre(0), GoblinBody), "A thief with nothing yet in hand has something over its body.");
		Require(IsNear(Centre(1), WarrenView.MaterialColor(Material.Grub)), "A thief making off with Grub does not show the Grub.");
		Require(IsNear(Centre(2), WarrenView.MaterialColor(Material.Grog)), "A thief making off with Grog does not show the Grog.");
		var pip = WarrenView.PipRect(Marker(2)).GetCenter();
		Require(IsNear(image.GetPixel((int)pip.X, (int)pip.Y), WarrenView.NeedColor(Need.Grub)), "A thief that craves Grub has lost its pip.");
		Require(!IsNear(Ring(3), WarrenView.ThiefColor) && !IsNear(Mask(3), WarrenView.ThiefColor), "A goblin that is not stealing wears the thief's cue.");
	}

	// The "zzz" lies inside its tile, from the left edge of the goblin marker.
	static void ZzzLayoutCheck()
	{
		var tile = new Rect2(0, 0, WarrenView.CellSize, WarrenView.CellSize);
		var marker = new Rect2(3, 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		var baseline = WarrenView.ZzzBaseline(marker);
		var width = ThemeDB.FallbackFont.GetStringSize("zzz", HorizontalAlignment.Left, -1, WarrenView.ZzzFontSize).X;
		var text = new Rect2(baseline.X, baseline.Y - (WarrenView.ZzzFontSize - 2), width, WarrenView.ZzzFontSize - 2);
		Require(tile.Encloses(text), $"The \"zzz\" {text} leaves its tile.");
	}

	async System.Threading.Tasks.Task NeedMarksCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		for (var g = 0; g < s.GoblinCount; g++)
			(s.GoblinGrub[g], s.GoblinGrog[g]) = (100, 100);
		s.GoblinGrub[0] = 29;
		s.GoblinGrub[3] = 0;
		s.GoblinGrog[1] = 29;
		(s.GoblinGrub[2], s.GoblinGrog[2]) = (25, 10);
		(s.GoblinGrub[5], s.GoblinGrog[5]) = (20, 20);
		s.GoblinGrog[6] = 0;
		GoblinTasks.FinishPickUp(s, 3, Material.Dirt);
		var image = await Render(sim);

		Color Body(int g) => image.GetPixel(s.GoblinX[g] * WarrenView.CellSize + 5, s.GoblinY[g] * WarrenView.CellSize + 13);
		Vector2I Pip(int g)
		{
			var marker = new Rect2(s.GoblinX[g] * WarrenView.CellSize + 3, s.GoblinY[g] * WarrenView.CellSize + 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
			return (Vector2I)WarrenView.PipRect(marker).GetCenter();
		}

		Require(IsNear(Body(0), GoblinBody), "A craving goblin's body should stay green.");
		ExpectColor(image, Pip(0).X, Pip(0).Y, WarrenView.MaterialColor(Material.Grub), "A goblin craving Grub has no Grub pip.");
		ExpectColor(image, Pip(1).X, Pip(1).Y, WarrenView.MaterialColor(Material.Grog), "A goblin craving Grog has no Grog pip.");
		Require(IsNear(Body(1), GoblinBody), "A goblin craving Grog should stay green.");
		ExpectColor(image, Pip(2).X, Pip(2).Y, WarrenView.MaterialColor(Material.Grog), "With the lower Grog meter, the pip should be Grog's.");
		ExpectColor(image, Pip(5).X, Pip(5).Y, WarrenView.MaterialColor(Material.Grub), "On a tie the pip should be Grub's.");
		Require(IsNear(Body(3), WarrenView.StarvingGoblinColor), "A Starving goblin's body is not tinted.");
		ExpectColor(image, Pip(3).X, Pip(3).Y, WarrenView.MaterialColor(Material.Grub), "A Starving goblin has no Grub pip.");
		var square = Square(s, 3, slot: 0, carriers: 1);
		ExpectColor(image, square.X, square.Y, DirtSquare, "The pip covered the Starving carrier's square.");
		Require(IsNear(Body(4), GoblinBody), "A fed goblin's body should be green.");
		ExpectColor(image, Pip(4).X, Pip(4).Y, GoblinBody, "A fed goblin has a pip.");
		Require(IsNear(Body(6), GoblinBody), "Grog at 0 should not tint the goblin.");
		ExpectColor(image, Pip(6).X, Pip(6).Y, WarrenView.MaterialColor(Material.Grog), "A goblin with Grog at 0 has no Grog pip.");
	}

	// The pip lies inside its tile, and clear of the carried square at the middle of the marker.
	static void PipLayoutCheck()
	{
		var tile = new Rect2(0, 0, WarrenView.CellSize, WarrenView.CellSize);
		var marker = new Rect2(3, 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		var pip = WarrenView.PipRect(marker);
		Require(tile.Encloses(pip), $"The pip {pip} leaves its tile.");
		Require(!pip.Intersects(WarrenView.CarriedRect(marker, 0, 1)), "The pip overlaps the carried square.");
	}

	// The first lit pixel of a count's chip, or its text colour, is read at the count's baseline.
	static bool HasChip(Image image, int tileX, int tileY, int slot)
	{
		var rect = new Rect2(tileX * WarrenView.CellSize, tileY * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize);
		var chip = WarrenView.CountChipRect(WarrenView.CountBaseline(rect, slot), 0);
		return IsNear(image.GetPixel((int)chip.Position.X, (int)chip.Position.Y), WarrenView.FullCountChipColor);
	}

	// A count in the right slot is drawn in its material's colour, or on the chip when full: some pixel of the slot's band has it.
	static void ExpectCount(Image image, int tileX, int slot, Material material, bool chip)
	{
		Require(HasChip(image, tileX, 4, slot) == chip, $"Tile {tileX}, slot {slot}: the chip is {(chip ? "missing" : "present")}.");
		if (chip)
			return;
		var rect = new Rect2(tileX * WarrenView.CellSize, 4 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize);
		var baseline = WarrenView.CountBaseline(rect, slot);
		var found = false;
		for (var y = (int)baseline.Y - (WarrenView.CountFontSize - 2); y <= (int)baseline.Y && !found; y++)
		for (var x = (int)baseline.X; x < (int)baseline.X + 8 && !found; x++)
			found = IsNear(image.GetPixel(x, y), WarrenView.MaterialColor(material));
		Require(found, $"Tile {tileX}, slot {slot}: no {material}-coloured count was drawn.");
	}

	// Every material has a colour of its own, so a loose square or a carried square says what it is.
	static void ColourChecks()
	{
		foreach (var a in Materials)
		foreach (var b in Materials)
		{
			if (a >= b)
				continue;
			var (ca, cb) = (WarrenView.MaterialColor(a), WarrenView.MaterialColor(b));
			Require(Math.Abs(ca.R - cb.R) + Math.Abs(ca.G - cb.G) + Math.Abs(ca.B - cb.B) > 0.3f, $"{a} and {b} have colours too close to tell apart.");
		}
	}

	// The loose squares of every material fit one tile without overlap.
	static void LooseLayoutChecks()
	{
		var rect = new Rect2(0, 0, WarrenView.CellSize, WarrenView.CellSize);
		for (var i = 0; i < Materials.Length; i++)
		{
			var square = WarrenView.LooseSquare(rect, i);
			Require(rect.Encloses(square), $"Loose square {i} leaves the tile.");
			for (var j = 0; j < i; j++)
				Require(!square.Intersects(WarrenView.LooseSquare(rect, j)), $"Loose squares {i} and {j} overlap.");
		}
	}

	static (int X, int Y) Square(SimState s, int goblin, int slot, int carriers)
	{
		var marker = new Rect2(s.GoblinX[goblin] * WarrenView.CellSize + 3, s.GoblinY[goblin] * WarrenView.CellSize + 3,
			WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		var center = WarrenView.CarriedRect(marker, slot, carriers).GetCenter();
		return ((int)center.X, (int)center.Y);
	}

	async System.Threading.Tasks.Task<Image> Render(Simulation sim, int viewZ = 0)
	{
		var size = new Vector2I(sim.State.Width * WarrenView.CellSize, sim.State.Height * WarrenView.CellSize);
		var viewport = new SubViewport { Size = size, RenderTargetUpdateMode = SubViewport.UpdateMode.Once };
		viewport.AddChild(new WarrenView { Sim = sim, ViewZ = viewZ });
		AddChild(viewport);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		var image = viewport.GetTexture().GetImage();
		RemoveChild(viewport);
		viewport.Free();
		return image;
	}

	static bool IsNear(Color a, Color b) => Math.Abs(a.R - b.R) < 0.03f && Math.Abs(a.G - b.G) < 0.03f && Math.Abs(a.B - b.B) < 0.03f;

	static void ExpectColor(Image image, int x, int y, Color expected, string message)
	{
		var actual = image.GetPixel(x, y);
		Require(IsNear(actual, expected), $"{message} Pixel ({x}, {y}) was {actual}, expected {expected}.");
	}

	static void Require(bool condition, string message)
	{
		if (!condition)
			throw new Exception(message);
	}
}
