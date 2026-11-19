using System;
using System.Collections.Generic;
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
	static readonly Material[] Materials = Enum.GetValues<Material>()[1..];
	static Image? _gruntSprite;
	static readonly Dictionary<string, Image> TerrainImages = [];

	static Color TerrainPixel(string name, int x, int y, bool transpose = false)
	{
		if (!TerrainImages.TryGetValue(name, out var image))
			TerrainImages[name] = image = GD.Load<Texture2D>($"res://art/terrain/{name}.png").GetImage();
		var px = (int)((x + 0.5f) * image.GetWidth() / WarrenView.CellSize);
		var py = (int)((y + 0.5f) * image.GetHeight() / WarrenView.CellSize);
		return transpose ? image.GetPixel(py, px) : image.GetPixel(px, py);
	}

	// The authored PNG is the reference for the rendered sprite, including transparent pixels that leave the floor visible.
	static Color SpritePixel(int x, int y, bool starving = false)
	{
		var sprite = _gruntSprite ??= GD.Load<Texture2D>("res://art/goblins/grunt-idle.png").GetImage();
		var pixel = sprite.GetPixel((int)((x + 0.5f) * sprite.GetWidth() / WarrenView.CellSize),
			(int)((y + 0.5f) * sprite.GetHeight() / WarrenView.CellSize));
		if (pixel.A == 0)
			return TerrainPixel("surface", x, y);
		return starving ? pixel * WarrenView.StarvingGoblinColor : pixel;
	}

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
				GD.Print("Carry draw checks: layout checks passed; the 21 pixel scenarios were SKIPPED, not passed: no renderer under --headless.");
				GetTree().Quit();
				return;
			}

			await TerrainCheck();
			await GruntSpriteCheck();
			await ResourceSpriteCheck();
			await RoomSpriteCheck();
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
			await BubbleSymbolCheck();
			await BubblePileUpCheck();
			await BubbleKeepsCuesCheck();
			await BubbleOffLevelCheck();
			GD.Print("Carry draw checks passed (layout, colours, count order, 21 pixel scenarios).");
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

	// Terrain comes from tile kind, material and level; its visuals never write the simulation.
	async System.Threading.Tasks.Task TerrainCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.GoblinCount = 0;
		s.TileMaterials[s.TileIndex(6, 4, 0)] = (byte)Material.Stone;
		for (var z = 1; z <= 2; z++)
		{
			for (var x = 10; x <= 12; x++)
				s.TileKinds[s.TileIndex(x, 10, z)] = (byte)TileKind.Floor;
			s.TileKinds[s.TileIndex(11, 10, z)] = (byte)TileKind.Stairs;
			s.TileKinds[s.TileIndex(13, 10, z)] = (byte)TileKind.OpenAir;
		}
		var before = SimJson.Serialize(sim);
		var surface = await Render(sim);
		var dirt = await Render(sim, viewZ: 1);
		var stone = await Render(sim, viewZ: 2);
		Color At(Image image, int x, int y, int px = 9, int py = 9) => image.GetPixel(x * WarrenView.CellSize + px, y * WarrenView.CellSize + py);
		Require(IsNear(At(surface, 4, 4), TerrainPixel("surface", 9, 9)), "Surface Floor did not draw its earth-and-grass texture.");
		Require(IsNear(At(surface, 6, 4), TerrainPixel("stone-floor", 9, 9)), "A stone Floor on the Surface ignored its material.");
		Require(IsNear(At(dirt, 4, 4, 4, 8), TerrainPixel("dirt-solid", 4, 8, transpose: true)), "Solid dirt did not draw its coordinate-selected texture orientation.");
		Require(IsNear(At(dirt, 10, 10), TerrainPixel("dirt-floor", 9, 9)), "Dug dirt did not draw packed-earth Floor.");
		Require(IsNear(At(stone, 4, 4, 4, 8), TerrainPixel("stone-solid", 4, 8)), "Solid stone did not draw its rock texture.");
		Require(IsNear(At(stone, 10, 10), TerrainPixel("stone-floor", 9, 9)), "Dug stone did not draw stone Floor.");
		foreach (var image in new[] { dirt, stone })
		{
			Require(IsNear(At(image, 13, 10), WarrenView.OpenAirColor), "Open air looks like walkable Floor.");
			Require(IsNear(At(image, 11, 10, 9, 9), new Color(0.76f, 0.56f, 0.30f)), "Stairs lost their ladder rungs.");
			Require(IsNear(At(image, 11, 10, 7, 7), WarrenView.OpenAirColor), "Stairs lost their dark opening.");
		}
		Require(!IsNear(At(dirt, 10, 9, 9, 16), TerrainPixel("dirt-solid", 9, 16)), "The exposed Solid dirt edge has no visible lip.");
		Require(!IsNear(At(stone, 10, 9, 9, 16), TerrainPixel("stone-solid", 9, 16, transpose: true)), "The exposed Solid stone edge has no visible lip.");
		Require(SimJson.Serialize(sim) == before, "Drawing terrain changed the sim or its seeded RNG.");
		foreach (var image in TerrainImages.Values)
			Require(image.GetWidth() == 32 && image.GetHeight() == 32, "A terrain texture is not 32×32.");
		var captures = OS.GetEnvironment("GOBLINTOWN_CAPTURE_DIR");
		if (captures != "")
		{
			System.IO.Directory.CreateDirectory(captures);
			surface.SavePng(System.IO.Path.Combine(captures, "terrain-surface.png"));
			dirt.SavePng(System.IO.Path.Combine(captures, "terrain-dirt.png"));
			stone.SavePng(System.IO.Path.Combine(captures, "terrain-stone.png"));
		}
	}

	// The shared sprite keeps its palette and transparency and is tinted only for starvation.
	async System.Threading.Tasks.Task GruntSpriteCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		for (var g = 0; g < s.GoblinCount; g++)
			(s.GoblinGrub[g], s.GoblinGrog[g], s.GoblinSleep[g]) = (100, 100, 100);
		(s.GoblinX[0], s.GoblinY[0]) = (4, 4);
		(s.GoblinX[1], s.GoblinY[1]) = (6, 4);
		s.GoblinGrub[1] = 0;
		var image = await Render(sim);
		for (var g = 0; g < 2; g++)
		for (var y = 1; y < WarrenView.CellSize - 1; y++)
		for (var x = 1; x < WarrenView.CellSize - 1; x++)
		{
			// The starving Grunt also has its Grub pip, drawn above the sprite.
			if (g == 1 && WarrenView.PipRect(new Rect2(3, 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6)).HasPoint(new Vector2(x, y)))
				continue;
			ExpectColor(image, s.GoblinX[g] * WarrenView.CellSize + x, s.GoblinY[g] * WarrenView.CellSize + y,
				SpritePixel(x, y, starving: g == 1), $"Grunt {g}: the sprite's palette, transparency or nearest-neighbour sampling changed.");
		}
		Require(_gruntSprite!.GetWidth() == 32 && _gruntSprite.GetHeight() == 32, "The Grunt source sprite is not 32×32.");
	}

	async System.Threading.Tasks.Task ResourceSpriteCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.GoblinCount = 0;
		Array.Clear(s.TileItems);
		var background = await Render(sim);
		foreach (var material in Materials)
			s.AddItems(s.TileIndex(4, 4, 0), material, 1);
		sim.RecheckReachability();
		var before = SimJson.Serialize(sim);
		var waiting = await Render(sim);
		Require(SimJson.Serialize(sim) == before, "Drawing resources changed the sim or its seeded RNG.");
		var tile = new Rect2(4 * WarrenView.CellSize, 4 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize);
		foreach (var material in Materials)
		{
			var normal = MaterialSprites.Get(material).GetImage();
			var grey = MaterialSprites.Get(material, waiting: true).GetImage();
			Require(normal.GetWidth() == 16 && normal.GetHeight() == 16, $"{material} is not 16×16.");
			var transparent = 0;
			for (var y = 0; y < 16; y++)
			for (var x = 0; x < 16; x++)
			{
				var a = normal.GetPixel(x, y);
				var b = grey.GetPixel(x, y);
				Require(Math.Abs(a.A - b.A) < 0.01f, $"Waiting {material} changed its silhouette.");
				Require(Math.Abs(b.R - b.G) < 0.01f && Math.Abs(b.G - b.B) < 0.01f, $"Waiting {material} is not grey.");
				if (a.A == 0) transparent++;
			}
			Require(transparent > 0, $"{material} has no transparent background.");
			ExpectMaterial(waiting, WarrenView.LooseSquare(tile, (int)material - 1), material, "Waiting resource lost its shape.", waiting: true, background: background);
		}
		// The smallest carrier slots deliberately retain solid material colours.
		s.GoblinCount = SimState.MaxGoblins;
		for (var g = 0; g < s.GoblinCount; g++)
		{
			(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]) = (10, 10, 0);
			(s.GoblinGrub[g], s.GoblinGrog[g], s.GoblinSleep[g]) = (100, 100, 100);
			GoblinTasks.FinishPickUp(s, g, Materials[g % Materials.Length]);
		}
		var crowded = await Render(sim);
		var captures = OS.GetEnvironment("GOBLINTOWN_CAPTURE_DIR");
		if (captures != "")
		{
			System.IO.Directory.CreateDirectory(captures);
			waiting.SavePng(System.IO.Path.Combine(captures, "resources-waiting.png"));
			crowded.SavePng(System.IO.Path.Combine(captures, "resources-crowded.png"));
		}
		for (var g = 0; g < s.GoblinCount; g++)
			ExpectMaterial(crowded, CarryRect(s, g, g, s.GoblinCount), Materials[g % Materials.Length], "A crowded carrier lost its colour.");
	}

	// Fixtures use authored pixels, stay inside their painted tile, and disappear immediately when the Room is removed.
	async System.Threading.Tasks.Task RoomSpriteCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.GoblinCount = 1;
		(s.GoblinX[0], s.GoblinY[0], s.GoblinZ[0]) = (2, 7, 1);
		(s.GoblinGrub[0], s.GoblinGrog[0], s.GoblinSleep[0]) = (100, 100, 100);
		Array.Clear(s.TileItems);
		for (var y = 7; y <= 11; y++)
		for (var x = 2; x <= 22; x++)
			s.TileKinds[s.TileIndex(x, y, 1)] = (byte)TileKind.Floor;
		sim.RecheckReachability();
		var bare = await Render(sim, viewZ: 1);
		var rooms = new[] { RoomKind.Pile, RoomKind.GrubPit, RoomKind.FungusFarm, RoomKind.Brewery, RoomKind.SleepingHeap };
		for (var i = 0; i < rooms.Length; i++)
			sim.Apply(new PaintRoom(rooms[i], 1, 4 + i * 3, 9, 4 + i * 3, 9));
		var before = SimJson.Serialize(sim);
		var image = await Render(sim, viewZ: 1);
		Require(SimJson.Serialize(sim) == before, "Drawing Room fixtures changed simulation state or RNG.");
		for (var i = 0; i < rooms.Length; i++)
		{
			var room = rooms[i];
			var x = 4 + i * 3;
			ExpectRoomSprite(image, x, 9, room);
			var source = RoomSprites.Get(room).GetImage();
			Require(source.GetWidth() == 32 && source.GetHeight() == 32, $"{room} is not 32×32.");
			Require(source.GetPixel(0, 0).A == 0, $"{room} has no transparent margin.");
			// No neighbour receives a fixture, even beside a painted tile.
			for (var py = 0; py < WarrenView.CellSize; py++)
			for (var px = 0; px < WarrenView.CellSize; px++)
				ExpectColor(image, (x + 1) * WarrenView.CellSize + px, 9 * WarrenView.CellSize + py,
					bare.GetPixel((x + 1) * WarrenView.CellSize + px, 9 * WarrenView.CellSize + py), $"{room} spilled into a neighbouring tile.");
		}
		var captures = OS.GetEnvironment("GOBLINTOWN_CAPTURE_DIR");
		if (captures != "")
		{
			System.IO.Directory.CreateDirectory(captures);
			image.SavePng(System.IO.Path.Combine(captures, "room-fixtures.png"));
		}
		// A working goblin and its Need pip remain above the fixture, including its transparent edges.
		(s.GoblinX[0], s.GoblinY[0], s.GoblinGrub[0]) = (16, 9, 20);
		var occupied = await Render(sim, viewZ: 1);
		using var grunt = GD.Load<Texture2D>("res://art/goblins/grunt-idle.png").GetImage();
		var marker = new Rect2(3, 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		for (var y = 1; y < WarrenView.CellSize - 1; y++)
		for (var x = 1; x < WarrenView.CellSize - 1; x++)
		{
			if (WarrenView.PipRect(marker).HasPoint(new Vector2(x, y))) continue;
			var pixel = grunt.GetPixel((int)((x + 0.5f) * 32 / WarrenView.CellSize), (int)((y + 0.5f) * 32 / WarrenView.CellSize));
			var px = 16 * WarrenView.CellSize + x;
			var py = 9 * WarrenView.CellSize + y;
			ExpectColor(occupied, px, py, pixel.A == 0 ? image.GetPixel(px, py) : pixel, "A Room fixture covered its goblin or its transparent edge.");
		}
		var pip = WarrenView.PipRect(marker).GetCenter();
		ExpectColor(occupied, 16 * WarrenView.CellSize + (int)pip.X, 9 * WarrenView.CellSize + (int)pip.Y, WarrenView.NeedColor(Need.Grub), "A Room fixture covered a Need pip.");
		(s.GoblinX[0], s.GoblinY[0], s.GoblinGrub[0]) = (2, 7, 100);

		for (var i = 0; i < rooms.Length; i++)
			sim.Apply(new RemoveRoom(rooms[i], 1, 4 + i * 3, 9, 4 + i * 3, 9));
		var removed = await Render(sim, viewZ: 1);
		Require(removed.GetData().SequenceEqual(bare.GetData()), "Removing Rooms left stale artwork or tint.");
	}

	static void ExpectRoomSprite(Image actual, int tileX, int tileY, RoomKind room, bool waiting = false)
	{
		using var source = GD.Load<Texture2D>($"res://art/rooms/{RoomSprites.FileName(room)}.png").GetImage();
		if (waiting) source.AdjustBcs(0.8f, 1, 0);
		var opaque = 0;
		for (var y = 0; y < WarrenView.CellSize; y++)
		for (var x = 0; x < WarrenView.CellSize; x++)
		{
			// A Brewery's loose Grog in the waiting/ready scenario draws above its fixture.
			if (room == RoomKind.Brewery && WarrenView.LooseSquare(new Rect2(0, 0, WarrenView.CellSize, WarrenView.CellSize), (int)Material.Grog - 1).HasPoint(new Vector2(x, y)))
				continue;
			var pixel = source.GetPixel((int)((x + 0.5f) * 32 / WarrenView.CellSize), (int)((y + 0.5f) * 32 / WarrenView.CellSize));
			if (pixel.A < 0.99f) continue;
			opaque++;
			ExpectColor(actual, tileX * WarrenView.CellSize + x, tileY * WarrenView.CellSize + y, pixel, $"{room} has the wrong sprite or waiting palette.");
		}
		Require(opaque > 20, $"{room} has too few visible fixture pixels.");
	}

	static void ExpectRoomFloor(Image image, int tileX, int tileY, RoomKind room, string terrain, Color tint)
	{
		using var source = RoomSprites.Get(room).GetImage();
		// Read exposed Floor below the fixture and away from all five loose-item slots.
		for (var y = 12; y < WarrenView.CellSize - 1; y++)
		for (var x = 1; x < WarrenView.CellSize - 1; x++)
		{
			if (source.GetPixel((int)((x + 0.5f) * 32 / WarrenView.CellSize), (int)((y + 0.5f) * 32 / WarrenView.CellSize)).A != 0)
				continue;
			var floor = TerrainPixel(terrain, x, y);
			var tinted = floor * (1 - tint.A) + tint * tint.A;
			ExpectColor(image, tileX * WarrenView.CellSize + x, tileY * WarrenView.CellSize + y, new Color(tinted.R, tinted.G, tinted.B), $"{room} lost its Floor tint.");
			return;
		}
		throw new Exception($"{room} has no exposed Floor for the tint check.");
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
		ExpectMaterial(image, CarryRect(s, 0, 0, 1), Material.Dirt, "A carrier's item was covered by a body on its tile.");
		ExpectColor(image, square.X, square.Y - 5, SpritePixel(9, 4), "The goblin body was not drawn around the square.");
	}

	// Every goblin carries on one tile: each slot shows its goblin's material, inside the marker, and the tiles around stay clean.
	async System.Threading.Tasks.Task FullTileCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		var (tileX, tileY) = (s.GoblinX[0], s.GoblinY[0]);
		for (var g = 0; g < s.GoblinCount; g++)
			(s.GoblinX[g], s.GoblinY[g]) = (tileX, tileY);
		var background = await Render(sim);
		for (var g = 0; g < s.GoblinCount; g++)
			GoblinTasks.FinishPickUp(s, g, Materials[g % Materials.Length]);
		var image = await Render(sim);

		for (var g = 0; g < s.GoblinCount; g++)
		{
			ExpectMaterial(image, CarryRect(s, g, g, s.GoblinCount), Materials[g % Materials.Length], $"Carrier {g}'s slot does not show its material.", background: background);
		}

		var cell = WarrenView.CellSize;
		for (var y = (tileY - 1) * cell; y < (tileY + 2) * cell; y++)
		for (var x = (tileX - 1) * cell; x < (tileX + 2) * cell; x++)
		{
			var inMarker = x >= tileX * cell + 3 && x < (tileX + 1) * cell - 3 && y >= tileY * cell + 3 && y < (tileY + 1) * cell - 3;
			if (inMarker)
				continue;
			ExpectColor(image, x, y, background.GetPixel(x, y), $"A carried square changed the terrain or sprite outside its marker at ({x}, {y}).");
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
			ExpectMaterial(image, WarrenView.LooseSquare(rect, (int)material - 1), material, $"Loose {material} is missing.");
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

		var floor = TerrainPixel("surface", 9, 9);
		ExpectRoomFloor(image, 10, 10, RoomKind.GrubPit, "surface", WarrenView.GrubPitTint);
		ExpectColor(image, 12 * WarrenView.CellSize + 9, 10 * WarrenView.CellSize + 9, floor, "A tile beside the Grub Pit is tinted.");
		var square = WarrenView.LooseSquare(new Rect2(11 * WarrenView.CellSize, 10 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize), (int)Material.Grub - 1);
		ExpectMaterial(image, square, Material.Grub, "The Grub loose on a Grub Pit is hidden by its tint.");
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
		var floor = TerrainPixel("dirt-floor", 9, 9);
		ExpectRoomFloor(image, 10, 10, RoomKind.FungusFarm, "dirt-floor", WarrenView.FungusFarmTint);
		ExpectColor(image, 12 * WarrenView.CellSize + 9, 10 * WarrenView.CellSize + 9, floor, "A tile beside the Fungus Farm is tinted.");
		var square = WarrenView.LooseSquare(new Rect2(11 * WarrenView.CellSize, 10 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize), (int)Material.Fungus - 1);
		ExpectMaterial(image, square, Material.Fungus, "The Fungus loose on a Fungus Farm is hidden by its tint.");
	}

	// A Brewery tile is tinted over the floor in its own colour; it greys while no Pile in reach has room for Grog, and goes back to its
	// colour when one is painted. The Grog lying loose on it shows over either tint.
	async System.Threading.Tasks.Task BreweryTintCheck()
	{
		var cell = WarrenView.CellSize;
		var loose = WarrenView.LooseSquare(new Rect2(10 * cell, 10 * cell, cell, cell), (int)Material.Grog - 1);

		Require(!IsNear(WarrenView.BreweryTint, WarrenView.GrubPitTint) && !IsNear(WarrenView.BreweryTint, WarrenView.FungusFarmTint)
			&& !IsNear(WarrenView.BreweryTint, WarrenView.SleepingHeapTint) && !IsNear(WarrenView.BreweryTint, WarrenView.WaitingBreweryTint),
			"The Brewery tint is the same as another Room's, or as its waiting tint.");

		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		sim.Apply(new PaintRoom(RoomKind.Brewery, 0, 10, 10, 11, 10));
		s.AddItems(s.TileIndex(10, 10, 0), Material.Grog, 1);
		sim.RecheckReachability();
		var waiting = await Render(sim);
		ExpectRoomFloor(waiting, 10, 10, RoomKind.Brewery, "surface", WarrenView.WaitingBreweryTint);
		ExpectRoomSprite(waiting, 10, 10, RoomKind.Brewery, waiting: true);
		ExpectMaterial(waiting, loose, Material.Grog, "Waiting Grog lost its grey sprite.", waiting: true);

		sim.Apply(new PaintPile(0, 20, 20, 20, 20));
		var ready = await Render(sim);
		ExpectRoomFloor(ready, 10, 10, RoomKind.Brewery, "surface", WarrenView.BreweryTint);
		ExpectRoomSprite(ready, 10, 10, RoomKind.Brewery);
		ExpectColor(ready, 12 * cell + 9, 10 * cell + 9, TerrainPixel("surface", 9, 9), "A tile beside the Brewery is tinted.");
		ExpectMaterial(ready, loose, Material.Grog, "The Grog loose on a Brewery is hidden by its tint.");
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

		var floor = TerrainPixel("surface", 9, 9);
		ExpectRoomFloor(image, 10, 10, RoomKind.SleepingHeap, "surface", WarrenView.SleepingHeapTint);
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
		Require(IsNear(PipAt(0), SpritePixel(14, 4)), "A sleeping goblin with a comfortable meter has a pip.");
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
		Require(IsNear(Band(1), SpritePixel(9, 9)) && !IsNear(Ring(1), WarrenView.ShirkColor), "A goblin that is not shirking wears the shirk marks.");
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
		Require(IsNear(Centre(0), SpritePixel(9, 9)), "A pursuing goblin wears the scuffle cross.");
		Require(IsNear(Ring(2), WarrenView.FightColor) && IsNear(Ring(3), WarrenView.FightColor), "A scuffling goblin or its victim has no fight ring.");
		Require(IsNear(Centre(2), WarrenView.FightColor) && IsNear(Centre(3), WarrenView.FightColor), "A scuffling goblin or its victim has no cross over its body.");
		Require(IsNear(Ring(4), WarrenView.FightRestColor) && IsNear(Ring(5), WarrenView.FightRestColor), "A recovering goblin has no banked-down ring.");
		Require(IsNear(Centre(4), SpritePixel(9, 9)), "A recovering goblin wears the scuffle cross.");
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
		Require(IsNear(Centre(0), SpritePixel(9, 9)), "A thief with nothing yet in hand has something over its body.");
		ExpectMaterial(image, CarryRect(s, 1, 0, 1), Material.Grub, "A thief making off with Grub does not show the Grub.");
		ExpectMaterial(image, CarryRect(s, 2, 0, 1), Material.Grog, "A thief making off with Grog does not show the Grog.");
		var pip = WarrenView.PipRect(Marker(2)).GetCenter();
		Require(IsNear(image.GetPixel((int)pip.X, (int)pip.Y), WarrenView.NeedColor(Need.Grub)), "A thief that craves Grub has lost its pip.");
		Require(!IsNear(Ring(3), WarrenView.ThiefColor) && !IsNear(Mask(3), WarrenView.ThiefColor), "A goblin that is not stealing wears the thief's cue.");
	}


	static SimEvent IncidentFor(BubbleKind kind, int goblin) => kind switch
	{
		BubbleKind.Shirk => new ShirkStarted(goblin),
		BubbleKind.Scuffle => new ScuffleStarted(goblin, (goblin + 1) % 7),
		BubbleKind.Theft => new TheftPickedUp(goblin, Material.Grog),
		_ => new HaulCancelled(goblin),
	};

	static List<(Bubble Bubble, Rect2 Rect, Rect2 Anchor)> LayoutOf(Simulation sim, BubbleBoard board, int viewZ = 0)
	{
		var view = new WarrenView { Sim = sim, Bubbles = board, ViewZ = viewZ };
		var laid = view.LayoutBubbles();
		view.Free();
		return laid;
	}

	static Color Pixel(Image image, Vector2 at) => image.GetPixel((int)at.X, (int)at.Y);

	// Each kind of bubble draws its paper, rim, symbol in its colour and some ink for the name and the line, whole inside the map, for a goblin
	// in the open, in each corner and along each edge; and the goblin's own body is left showing.
	async System.Threading.Tasks.Task BubbleSymbolCheck()
	{
		var spots = new[] { (-1, -1), (0, 0), (31, 0), (0, 31), (31, 31), (15, 0), (15, 31), (0, 15), (31, 15) };
		foreach (var kind in Enum.GetValues<BubbleKind>())
		foreach (var (x, y) in spots)
		{
			var sim = Simulation.NewWorld(seed: 1);
			var s = sim.State;
			if (x >= 0)
				(s.GoblinX[0], s.GoblinY[0]) = (x, y);
			var board = new BubbleBoard();
			board.Observe([IncidentFor(kind, 0)], s);
			var image = await Render(sim, bubbles: board);
			var (bubble, rect, anchor) = LayoutOf(sim, board).Single();
			var where = $"{kind} at ({s.GoblinX[0]},{s.GoblinY[0]})";

			Require(new Rect2(0, 0, image.GetWidth(), image.GetHeight()).Encloses(rect), $"{where}: the bubble {rect} leaves the map.");
			Require(!rect.Intersects(anchor), $"{where}: the bubble covers its goblin.");
			ExpectColor(image, (int)rect.Position.X + 1, (int)rect.Position.Y + 1, WarrenView.BubbleFillColor, $"{where}: no paper.");
			ExpectColor(image, (int)rect.Position.X - 1, (int)rect.Position.Y + 3, WarrenView.BubbleRimColor, $"{where}: no rim.");
			var symbol = WarrenView.BubbleSymbolCentre(rect);
			ExpectColor(image, (int)symbol.X, (int)symbol.Y, WarrenView.BubbleColor(kind), $"{where}: the symbol is not in its colour.");
			var ink = 0;
			for (var py = (int)rect.Position.Y; py < (int)rect.End.Y; py++)
			for (var px = (int)rect.Position.X + BubbleLayout.Pad + BubbleLayout.SymbolSize + BubbleLayout.Gap; px < (int)rect.End.X; px++)
				if (IsNear(image.GetPixel(px, py), WarrenView.BubbleTextColor) || IsNear(image.GetPixel(px, py), WarrenView.BubbleNameColor))
					ink++;
			Require(ink >= 20, $"{where}: only {ink} pixels of text.");
			var marker = new Rect2(s.GoblinX[0] * WarrenView.CellSize + 3, s.GoblinY[0] * WarrenView.CellSize + 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
			ExpectColor(image, (int)marker.GetCenter().X, (int)marker.GetCenter().Y, SpritePixel(9, 9), $"{where}: the goblin's body is covered.");
		}
		Require(Enum.GetValues<BubbleKind>().Select(WarrenView.BubbleColor).Distinct().Count() == 4, "Two kinds of bubble share a symbol colour.");
	}

	// Three goblins on one tile all speaking at once: three bubbles, none over another or over the tile, each readable; a fourth makes three.
	async System.Threading.Tasks.Task BubblePileUpCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		for (var g = 0; g < 4; g++)
			(s.GoblinX[g], s.GoblinY[g]) = (s.GoblinX[0], s.GoblinY[0]);
		var board = new BubbleBoard();
		board.Observe([new ShirkStarted(0), new HaulCancelled(1), new TheftPickedUp(2, Material.Grub), new ScuffleStarted(3, 0)], s);
		Require(board.Active.Count == 3, "Four incidents on one tile left more than three bubbles.");
		var image = await Render(sim, bubbles: board);
		var laid = LayoutOf(sim, board);
		Require(laid.Count == 3, "Three bubbles did not all lay out.");
		for (var i = 0; i < laid.Count; i++)
		{
			ExpectColor(image, (int)WarrenView.BubbleSymbolCentre(laid[i].Rect).X, (int)WarrenView.BubbleSymbolCentre(laid[i].Rect).Y, WarrenView.BubbleColor(laid[i].Bubble.Kind), $"Bubble {i} lost its symbol.");
			Require(!laid[i].Rect.Intersects(laid[i].Anchor), $"Bubble {i} covers the tile.");
			for (var j = 0; j < i; j++)
				Require(!laid[i].Rect.Intersects(laid[j].Rect), $"Bubbles {i} and {j} overlap.");
		}
		// The oldest surviving bubble must be just as readable with the others present: their tails may cross its rectangle.
		// Advance the authoring count once so the solo reference has the exact same name and line as the crowd's first bubble.
		var solo = new BubbleBoard();
		solo.Observe([new ShirkStarted(0)], s);
		solo.Advance(BubbleBoard.LifetimeSeconds);
		solo.Observe([new HaulCancelled(1)], s);
		var reference = await Render(sim, bubbles: solo);
		Require(LayoutOf(sim, solo)[0].Rect == laid[0].Rect, "The solo reference moved the first bubble.");
		var first = laid[0].Rect;
		var changed = 0;
		for (var y = (int)Math.Ceiling(first.Position.Y); y < (int)first.End.Y; y++)
		for (var x = (int)Math.Ceiling(first.Position.X); x < (int)first.End.X; x++)
			if (!IsNear(image.GetPixel(x, y), reference.GetPixel(x, y)))
				changed++;
		var captures = System.Environment.GetEnvironmentVariable("GOBLINTOWN_CAPTURE_DIR");
		if (!string.IsNullOrEmpty(captures))
		{
			System.IO.Directory.CreateDirectory(captures);
			image.SavePng(System.IO.Path.Combine(captures, "crowd.png"));
			reference.SavePng(System.IO.Path.Combine(captures, "solo.png"));
		}
		Require(changed == 0, $"Later bubbles overwrite {changed} pixels of the first bubble's paper, name or line.");
	}

	// A bubble does not cover the cues of the goblin it hangs from (pip, carried square, shirk band) nor of its neighbour; once it expires the
	// same cues are there and its paper is gone.
	async System.Threading.Tasks.Task BubbleKeepsCuesCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		(s.GoblinGrub[0], s.GoblinGrog[0], s.GoblinSleep[0]) = (20, 100, 100);
		GoblinTasks.FinishPickUp(s, 0, Material.Dirt);
		(s.GoblinX[1], s.GoblinY[1]) = (s.GoblinX[0] + 1, s.GoblinY[0]);
		(s.GoblinGrub[1], s.GoblinGrog[1], s.GoblinSleep[1]) = (100, 10, 100);
		var board = new BubbleBoard();
		board.Observe([new HaulCancelled(0), new ShirkStarted(1)], s);

		Rect2 Marker(int g) => new(s.GoblinX[g] * WarrenView.CellSize + 3, s.GoblinY[g] * WarrenView.CellSize + 3, WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		void ExpectCues(Image image, string when)
		{
			ExpectMaterial(image, CarryRect(s, 0, 0, 1), Material.Dirt, $"{when}: the carried item is covered.");
			ExpectColor(image, (int)WarrenView.PipRect(Marker(0)).GetCenter().X, (int)WarrenView.PipRect(Marker(0)).GetCenter().Y, WarrenView.NeedColor(Need.Grub), $"{when}: the Grub pip is covered.");
			ExpectColor(image, (int)WarrenView.PipRect(Marker(1)).GetCenter().X, (int)WarrenView.PipRect(Marker(1)).GetCenter().Y, WarrenView.NeedColor(Need.Grog), $"{when}: the neighbour's Grog pip is covered.");
			ExpectColor(image, (int)Marker(0).GetCenter().X, (int)Marker(0).Position.Y + 1, SpritePixel(9, 4), $"{when}: a body is covered.");
		}

		ExpectCues(await Render(sim, bubbles: board), "With bubbles");
		var rects = LayoutOf(sim, board).Select(l => l.Rect).ToList();
		board.Advance(BubbleBoard.LifetimeSeconds);
		Require(board.Active.Count == 0, "The bubbles did not expire.");
		var after = await Render(sim, bubbles: board);
		ExpectCues(after, "After expiry");
		foreach (var rect in rects)
			Require(!IsNear(Pixel(after, rect.Position + new Vector2(1, 1)), WarrenView.BubbleFillColor), $"The paper of {rect} outlived its bubble.");
	}

	// A bubble for a goblin on another level is not drawn on this one, wherever that goblin's coordinates would put it.
	async System.Threading.Tasks.Task BubbleOffLevelCheck()
	{
		var sim = Simulation.NewWorld(seed: 1);
		var s = sim.State;
		s.GoblinZ[0] = 1;
		var board = new BubbleBoard();
		board.Observe([new ShirkStarted(0)], s);
		Require(board.Active.Count == 1, "The level-1 goblin had no bubble to hide.");
		Require(LayoutOf(sim, board, viewZ: 0).Count == 0, "A bubble for a level-1 goblin laid out on level 0.");
		var below = await Render(sim, viewZ: 0, bubbles: board);
		var above = await Render(sim, viewZ: 1, bubbles: board);
		var rect = LayoutOf(sim, board, viewZ: 1).Single().Rect;
		var at = rect.Position + new Vector2(1, 1);
		Require(!IsNear(Pixel(below, at), WarrenView.BubbleFillColor), "Level 0 shows a level-1 goblin's bubble.");
		Require(IsNear(Pixel(above, at), WarrenView.BubbleFillColor), "Level 1 does not show its own goblin's bubble.");
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

		Require(IsNear(Body(0), SpritePixel(5, 13)), "A craving goblin's sprite should keep its colours.");
		ExpectColor(image, Pip(0).X, Pip(0).Y, WarrenView.MaterialColor(Material.Grub), "A goblin craving Grub has no Grub pip.");
		ExpectColor(image, Pip(1).X, Pip(1).Y, WarrenView.MaterialColor(Material.Grog), "A goblin craving Grog has no Grog pip.");
		Require(IsNear(Body(1), SpritePixel(5, 13)), "A goblin craving Grog should keep its sprite colours.");
		ExpectColor(image, Pip(2).X, Pip(2).Y, WarrenView.MaterialColor(Material.Grog), "With the lower Grog meter, the pip should be Grog's.");
		ExpectColor(image, Pip(5).X, Pip(5).Y, WarrenView.MaterialColor(Material.Grub), "On a tie the pip should be Grub's.");
		Require(IsNear(Body(3), SpritePixel(5, 13, starving: true)), "A Starving goblin's body is not tinted.");
		ExpectColor(image, Pip(3).X, Pip(3).Y, WarrenView.MaterialColor(Material.Grub), "A Starving goblin has no Grub pip.");
		ExpectMaterial(image, CarryRect(s, 3, 0, 1), Material.Dirt, "The pip covered the Starving carrier's item.");
		Require(IsNear(Body(4), SpritePixel(5, 13)), "A fed goblin's sprite should keep its colours.");
		ExpectColor(image, Pip(4).X, Pip(4).Y, SpritePixel(14, 4), "A fed goblin has a pip.");
		Require(IsNear(Body(6), SpritePixel(5, 13)), "Grog at 0 should not tint the goblin.");
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
		var rect = new Rect2(tileX * WarrenView.CellSize, 4 * WarrenView.CellSize, WarrenView.CellSize, WarrenView.CellSize);
		ExpectMaterial(image, WarrenView.PileIconRect(rect, slot), material, "The Pile count has the wrong material icon.");
		if (chip)
			return;
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

	static Rect2 CarryRect(SimState s, int goblin, int slot, int carriers)
	{
		var marker = new Rect2(s.GoblinX[goblin] * WarrenView.CellSize + 3, s.GoblinY[goblin] * WarrenView.CellSize + 3,
			WarrenView.CellSize - 6, WarrenView.CellSize - 6);
		return WarrenView.CarriedRect(marker, slot, carriers);
	}

	static (int X, int Y) Square(SimState s, int goblin, int slot, int carriers)
	{
		var center = CarryRect(s, goblin, slot, carriers).GetCenter();
		return ((int)center.X, (int)center.Y);
	}

	// Compare every sampled opaque source pixel, rather than assuming the centre of every silhouette is filled.
	static void ExpectMaterial(Image actual, Rect2 rect, Material material, string message, bool waiting = false, Image? background = null)
	{
		var sprite = GD.Load<Texture2D>($"res://art/materials/{material.ToString().ToLowerInvariant()}.png").GetImage();
		if (waiting)
			sprite.AdjustBcs(0.8f, 1, 0);
		var opaque = 0;
		for (var y = 0; y < (int)rect.Size.Y; y++)
		for (var x = 0; x < (int)rect.Size.X; x++)
		{
			var px = (int)rect.Position.X + x;
			var py = (int)rect.Position.Y + y;
			if (rect.Size.X < 3 || rect.Size.Y < 3)
			{
				ExpectColor(actual, px, py, WarrenView.MaterialColor(material), message);
				opaque++;
				continue;
			}
			var sx = (x + 0.5f) * sprite.GetWidth() / rect.Size.X;
			var sy = (y + 0.5f) * sprite.GetHeight() / rect.Size.Y;
			// Downsampling 16 to 6 lands some samples exactly on texel boundaries. GPU rounding may select either neighbour.
			var minX = Math.Max(0, (int)(sx - 0.0001f));
			var minY = Math.Max(0, (int)(sy - 0.0001f));
			var matched = false;
			for (var iy = minY; iy <= (int)sy && !matched; iy++)
			for (var ix = minX; ix <= (int)sx && !matched; ix++)
			{
				var color = sprite.GetPixel(ix, iy);
				if (color.A > 0.99f && IsNear(actual.GetPixel(px, py), color))
				{
					matched = true;
					opaque++;
				}
				else if (color.A == 0 && (background == null || IsNear(actual.GetPixel(px, py), background.GetPixel(px, py))))
					matched = true;
			}
			Require(matched, $"{message} {material} pixel ({px}, {py}) differs from the sprite: {actual.GetPixel(px, py)}.");
		}
		Require(opaque > 0, $"{material} has no visible opaque pixels at {rect.Size}.");
	}

	async System.Threading.Tasks.Task<Image> Render(Simulation sim, int viewZ = 0, BubbleBoard? bubbles = null)
	{
		var size = new Vector2I(sim.State.Width * WarrenView.CellSize, sim.State.Height * WarrenView.CellSize);
		var viewport = new SubViewport { Size = size, RenderTargetUpdateMode = SubViewport.UpdateMode.Once };
		viewport.AddChild(new WarrenView { Sim = sim, ViewZ = viewZ, Bubbles = bubbles });
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
