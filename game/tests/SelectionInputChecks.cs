using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>Headless input checks through Main's engine callbacks and the rendered sim state.</summary>
public partial class SelectionInputChecks : Node
{
	public override async void _Ready()
	{
		try
		{
			WithMain((main, view, sim) =>
			{
				sim.Apply(new Designate(DesignationKind.DigStairs, 0, 4, 4, 7, 7));
				sim.Apply(new Designate(DesignationKind.DigStairs, 0, 9, 9, 9, 9));
				// Right-drag works before any tool has been selected, with reversed corners.
				Drag(main, view, MouseButton.Right, 7, 7, 4, 4);
				ExpectRectangle(sim.State, 0, DesignationKind.None, 4, 4, 7, 7);
				ExpectRectangle(sim.State, 0, DesignationKind.DigStairs, 9, 9, 9, 9);
				Drag(main, view, MouseButton.Right, 9, 9, 9, 9);
				ExpectRectangle(sim.State, 0, DesignationKind.None, 9, 9, 9, 9);
				Require(view.Selection is null, "Completed cancellation kept its preview.");
			});

			WithMain((main, view, sim) =>
			{
				Drag(main, view, MouseButton.Left, 4, 4, 5, 5);
				ExpectRectangle(sim.State, 0, DesignationKind.None, 4, 4, 5, 5);
				Key(main, Godot.Key.S);
				Drag(main, view, MouseButton.Left, 4, 4, 5, 5);
				ExpectRectangle(sim.State, 0, DesignationKind.DigStairs, 4, 4, 5, 5);
				Key(main, Godot.Key.Period);
				Key(main, Godot.Key.D);
				Drag(main, view, MouseButton.Left, 4, 4, 5, 5);
				ExpectRectangle(sim.State, 1, DesignationKind.Dig, 4, 4, 5, 5);
				Drag(main, view, MouseButton.Right, 4, 4, 5, 5);
				ExpectRectangle(sim.State, 1, DesignationKind.None, 4, 4, 5, 5);
				ExpectRectangle(sim.State, 0, DesignationKind.DigStairs, 4, 4, 5, 5);
			});

			WithMain((main, view, sim) =>
			{
				sim.Apply(new Designate(DesignationKind.DigStairs, 0, 4, 4, 7, 7));
				// The release position counts even if no motion event preceded it.
				Button(main, view, MouseButton.Right, true, 4, 4);
				Button(main, view, MouseButton.Right, false, 7, 7);
				ExpectRectangle(sim.State, 0, DesignationKind.None, 4, 4, 7, 7);
			});

			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				Key(main, Godot.Key.P);
				Drag(main, view, MouseButton.Left, 10, 10, 12, 11);
				ExpectPiles(s, 0, true, 10, 10, 12, 11);
				// Stairs are skipped; the tile in the box that is not Floor stays unflagged.
				s.TileKinds[s.TileIndex(20, 20, 0)] = (byte)TileKind.Stairs;
				Drag(main, view, MouseButton.Left, 20, 20, 21, 20);
				Require(!s.IsPile(s.TileIndex(20, 20, 0)) && s.IsPile(s.TileIndex(21, 20, 0)), "Pile paint did not skip Stairs.");

				// Right-drag with the Pile Tool removes Piles and leaves Designations alone.
				sim.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 12, 12, 12));
				s.AddItems(s.TileIndex(10, 10, 0), Goblins.Sim.Material.Stone, 2);
				Drag(main, view, MouseButton.Right, 10, 10, 12, 12);
				ExpectPiles(s, 0, false, 10, 10, 12, 11);
				ExpectRectangle(s, 0, DesignationKind.DigStairs, 10, 12, 12, 12);
				Require(s.LooseCount(s.TileIndex(10, 10, 0), Goblins.Sim.Material.Stone) == 2, "Removed Pile lost its stock.");

				// Right-drag with Dig stairs cancels Designations and leaves Piles alone.
				Drag(main, view, MouseButton.Left, 10, 10, 12, 12);
				Key(main, Godot.Key.P);
				Key(main, Godot.Key.S);
				Drag(main, view, MouseButton.Right, 10, 10, 12, 12);
				ExpectRectangle(s, 0, DesignationKind.None, 10, 12, 12, 12);
				ExpectPiles(s, 0, true, 10, 10, 12, 11);
			});

			// The Grub Pit Tool follows the Pile's painting rules: Floor only, a tile holds one Room, right-drag removes only pits, Dig stairs clears.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				sim.Apply(new PaintPile(0, 12, 10, 12, 10));
				s.TileKinds[s.TileIndex(13, 10, 0)] = (byte)TileKind.Stairs;
				Key(main, Godot.Key.G);
				Drag(main, view, MouseButton.Left, 14, 10, 10, 11);
				ExpectRooms(s, RoomKind.GrubPit, 10, 10, 11, 10);
				ExpectRooms(s, RoomKind.GrubPit, 14, 10, 14, 10);
				ExpectRooms(s, RoomKind.GrubPit, 10, 11, 14, 11);
				Require(s.RoomAt(s.TileIndex(12, 10, 0)) == RoomKind.Pile, "Painting a Grub Pit replaced a Pile.");
				Require(s.RoomAt(s.TileIndex(13, 10, 0)) == RoomKind.None, "Painting a Grub Pit covered Stairs.");

				// Right-drag with the Grub Pit Tool takes the pits and leaves the Pile and Designations alone.
				sim.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 12, 11, 12));
				Drag(main, view, MouseButton.Right, 9, 10, 15, 12);
				Require(Enumerable.Range(10, 5).All(x => s.RoomAt(s.TileIndex(x, 11, 0)) == RoomKind.None), "Right-drag left Grub Pits behind.");
				Require(s.RoomAt(s.TileIndex(12, 10, 0)) == RoomKind.Pile, "Right-drag with the Grub Pit Tool removed a Pile.");
				ExpectRectangle(s, 0, DesignationKind.DigStairs, 10, 12, 11, 12);

				// Right-drag with the Pile Tool leaves pits alone, and Dig stairs through a pit clears it.
				Drag(main, view, MouseButton.Left, 10, 11, 10, 11);
				Key(main, Godot.Key.P);
				Drag(main, view, MouseButton.Right, 9, 10, 15, 12);
				Require(s.RoomAt(s.TileIndex(10, 11, 0)) == RoomKind.GrubPit, "Right-drag with the Pile Tool removed a Grub Pit.");
				Key(main, Godot.Key.S);
				Drag(main, view, MouseButton.Left, 10, 11, 10, 11);
				sim.Apply(new SetPaused(false));
				var frames = 0;
				while (s.KindAt(s.TileIndex(10, 11, 0)) != TileKind.Stairs && frames++ < 400)
					main._Process(0.1);
				Require(frames < 400, "The stairs through the Grub Pit were never dug.");
				Require(s.RoomAt(s.TileIndex(10, 11, 0)) == RoomKind.None, "Dig stairs left a Grub Pit on a Stairs tile.");
			});

			// The Sleeping Heap Tool follows the Pile's painting rules too: Floor only, a tile holds one Room, right-drag removes only Heaps, Dig stairs clears.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				sim.Apply(new PaintPile(0, 12, 10, 12, 10));
				sim.Apply(new PaintRoom(RoomKind.GrubPit, 0, 11, 10, 11, 10));
				s.TileKinds[s.TileIndex(13, 10, 0)] = (byte)TileKind.Stairs;
				Key(main, Godot.Key.H);
				Drag(main, view, MouseButton.Left, 14, 10, 10, 11);
				ExpectRooms(s, RoomKind.SleepingHeap, 10, 10, 10, 10);
				ExpectRooms(s, RoomKind.SleepingHeap, 14, 10, 14, 10);
				ExpectRooms(s, RoomKind.SleepingHeap, 10, 11, 14, 11);
				Require(s.RoomAt(s.TileIndex(12, 10, 0)) == RoomKind.Pile, "Painting a Heap replaced a Pile.");
				Require(s.RoomAt(s.TileIndex(11, 10, 0)) == RoomKind.GrubPit, "Painting a Heap replaced a Grub Pit.");
				Require(s.RoomAt(s.TileIndex(13, 10, 0)) == RoomKind.None, "Painting a Heap covered Stairs.");

				// Right-drag with the Heap Tool takes the Heaps and leaves the Pile, the pit and Designations alone.
				sim.Apply(new Designate(DesignationKind.DigStairs, 0, 10, 12, 11, 12));
				Drag(main, view, MouseButton.Right, 9, 10, 15, 12);
				Require(Enumerable.Range(10, 5).All(x => s.RoomAt(s.TileIndex(x, 11, 0)) == RoomKind.None), "Right-drag left Heaps behind.");
				Require(s.RoomAt(s.TileIndex(12, 10, 0)) == RoomKind.Pile && s.RoomAt(s.TileIndex(11, 10, 0)) == RoomKind.GrubPit,
					"Right-drag with the Heap Tool removed another Room.");
				ExpectRectangle(s, 0, DesignationKind.DigStairs, 10, 12, 11, 12);

				// Right-drag with the Pile Tool leaves Heaps alone, and Dig stairs through a Heap clears it.
				Drag(main, view, MouseButton.Left, 10, 11, 10, 11);
				Key(main, Godot.Key.P);
				Drag(main, view, MouseButton.Right, 9, 10, 15, 12);
				Require(s.RoomAt(s.TileIndex(10, 11, 0)) == RoomKind.SleepingHeap, "Right-drag with the Pile Tool removed a Heap.");
				Key(main, Godot.Key.S);
				Drag(main, view, MouseButton.Left, 10, 11, 10, 11);
				sim.Apply(new SetPaused(false));
				var frames = 0;
				while (s.KindAt(s.TileIndex(10, 11, 0)) != TileKind.Stairs && frames++ < 400)
					main._Process(0.1);
				Require(frames < 400, "The stairs through the Heap were never dug.");
				Require(s.RoomAt(s.TileIndex(10, 11, 0)) == RoomKind.None, "Dig stairs left a Heap on a Stairs tile.");
			});

			// The real game: Grunts craving Sleep walk to a painted Heap, share its one tile, and sleep there to a full meter.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				Array.Clear(s.TileItems);
				Array.Fill(s.GoblinGrub, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinGrog, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinSleep, (byte)NeedRules.MeterMax);
				Key(main, Godot.Key.H);
				Drag(main, view, MouseButton.Left, 12, 12, 12, 12);
				var heap = s.TileIndex(12, 12, 0);
				Require(s.RoomAt(heap) == RoomKind.SleepingHeap, "The Heap was not painted.");
				(s.GoblinSleep[0], s.GoblinSleep[1], s.GoblinSleep[2]) = (25, 20, 29);
				sim.Apply(new SetPaused(false));
				sim.Apply(new SetSpeed(4));
				var shared = 0;
				var frames = 0;
				while (frames++ < 200 && !(shared == 3 && Enumerable.Range(0, 3).All(g => (GoblinTask)s.GoblinTask[g] != GoblinTask.Sleeping)))
				{
					main._Process(0.25);
					Require(GoblinTasks.FindViolation(s) is null, "A Task rule broke while the Grunts went to bed.");
					shared = Math.Max(shared, Enumerable.Range(0, 3).Count(g => (GoblinTask)s.GoblinTask[g] == GoblinTask.Sleeping
						&& s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]) == heap));
					Require(Enumerable.Range(0, 3).All(g => (GoblinTask)s.GoblinTask[g] != GoblinTask.Sleeping
						|| s.TileIndex(s.GoblinX[g], s.GoblinY[g], s.GoblinZ[g]) == heap), "A Grunt slept off the Heap.");
				}
				Require(shared == 3, $"{shared} Grunts slept on the Heap at once, not 3.");
				Require(frames < 200, "The Grunts never woke from the Heap.");
				sim.Apply(new SetPaused(true));
				// Waking means a full meter, which then drains again at the usual rate.
				Require(Enumerable.Range(0, 3).All(g => s.GoblinSleep[g] >= 90), "A Grunt woke without a full Sleep meter.");
			});

			// The real game: with no Heap a Grunt lies down where it stands, and a digging Grunt whose Sleep reaches 0 collapses on the spot and sleeps uninterrupted.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				Array.Clear(s.TileItems);
				Array.Fill(s.GoblinGrub, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinGrog, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinSleep, (byte)NeedRules.MeterMax);
				Key(main, Godot.Key.S);
				Drag(main, view, MouseButton.Left, 6, 6, 9, 7);
				sim.Apply(new SetPaused(false));
				var frames = 0;
				while (!Enumerable.Range(0, s.GoblinCount).Any(g => (GoblinTask)s.GoblinTask[g] == GoblinTask.Digging) && frames++ < 200)
					main._Process(0.1);
				Require(frames < 200, "No Grunt started digging.");
				var digger = Enumerable.Range(0, s.GoblinCount).First(g => (GoblinTask)s.GoblinTask[g] == GoblinTask.Digging);
				Move(main, view, s.GoblinX[digger], s.GoblinY[digger]);
				Require(GoblinHover.GoblinAt(s, s.TileIndex(s.GoblinX[digger], s.GoblinY[digger], 0)) != digger
					|| main.Inspection.Doing == "Digging", $"A digging Grunt is \"{main.Inspection.Doing}\".");
				// A few ticks into the dig, so it has progress to keep.
				for (var tick = 0; tick < 5; tick++)
					main._Process(0.1);
				var dug = s.GoblinJobTile[digger];
				var progress = s.DigProgress[dug];
				Require(progress > 0 && (GoblinTask)s.GoblinTask[digger] == GoblinTask.Digging, "The digger is not mid-dig.");
				var (x, y) = (s.GoblinX[digger], s.GoblinY[digger]);
				var spare = Enumerable.Range(0, s.GoblinCount).First(g => g != digger);
				var (spareX, spareY) = (s.GoblinX[spare], s.GoblinY[spare]);
				(s.GoblinSleep[digger], s.GoblinSleep[spare]) = (0, 29);
				main._Process(0.1);
				Require((GoblinTask)s.GoblinTask[digger] == GoblinTask.Sleeping, $"The digger went on as {(GoblinTask)s.GoblinTask[digger]} at Sleep 0.");
				Require((GoblinTask)s.GoblinTask[spare] == GoblinTask.Sleeping, $"The Grunt at Sleep 29 went on as {(GoblinTask)s.GoblinTask[spare]}.");
				Require(s.DigProgress[dug] == progress && s.DesignationAt(dug) != DesignationKind.None, "The collapse threw the dig away.");

				sim.Apply(new SetSpeed(4));
				// Hunger and thirst arrive mid-sleep and wait their turn: the sleepers stay put until their meters are full.
				for (frames = 0; frames < 140 && ((GoblinTask)s.GoblinTask[digger] == GoblinTask.Sleeping || (GoblinTask)s.GoblinTask[spare] == GoblinTask.Sleeping); frames++)
				{
					main._Process(0.25);
					Require(GoblinTasks.FindViolation(s) is null, "A Task rule broke while the Grunts slept on the floor.");
					if ((GoblinTask)s.GoblinTask[digger] == GoblinTask.Sleeping)
						Require((s.GoblinX[digger], s.GoblinY[digger]) == (x, y), "The collapsed Grunt moved in its sleep.");
					if ((GoblinTask)s.GoblinTask[spare] == GoblinTask.Sleeping)
						Require((s.GoblinX[spare], s.GoblinY[spare]) == (spareX, spareY), "The Grunt with no Heap moved in its sleep.");
				}
				sim.Apply(new SetPaused(true));
				Require(frames < 140, "The Grunts never woke.");
				// Waking means a full meter, which then drains again at the usual rate.
				Require(s.GoblinSleep[digger] >= 90 && s.GoblinSleep[spare] >= 90, $"The sleepers woke at {s.GoblinSleep[digger]} and {s.GoblinSleep[spare]}, not full.");
			});

			// The real game: Grunts tend a painted Grub Pit, haul the three Grub it yields to a Pile, and a hungry Grunt eats one.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				Array.Clear(s.TileItems);
				Array.Fill(s.GoblinGrub, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinGrog, (byte)NeedRules.MeterMax);
				Key(main, Godot.Key.P);
				Drag(main, view, MouseButton.Left, 10, 10, 10, 10);
				Key(main, Godot.Key.P);
				Key(main, Godot.Key.G);
				Drag(main, view, MouseButton.Left, 14, 14, 14, 14);
				var pit = s.TileIndex(14, 14, 0);
				var pile = s.TileIndex(10, 10, 0);
				sim.Apply(new SetPaused(false));
				sim.Apply(new SetSpeed(4));
				var tended = false;
				for (var frame = 0; frame < 150 && s.StoredCount(pile, Goblins.Sim.Material.Grub) < ProductionRules.GrubPerTend; frame++)
				{
					main._Process(0.25);
					tended |= Enumerable.Range(0, s.GoblinCount).Any(g => (GoblinTask)s.GoblinTask[g] == GoblinTask.Tending);
					Require(GoblinTasks.FindViolation(s) is null, "A Task rule broke while the Grunts tended the pit.");
				}
				Require(tended, "No Grunt tended the Grub Pit.");
				Require(s.StoredCount(pile, Goblins.Sim.Material.Grub) == ProductionRules.GrubPerTend,
					$"The Pile holds {s.StoredCount(pile, Goblins.Sim.Material.Grub)} Grub from the pit, not {ProductionRules.GrubPerTend}.");
				Require(s.LooseCount(pit, Goblins.Sim.Material.Grub) == 0, "Grub was left lying in the pit.");

				sim.Apply(new SetSpeed(1));
				s.GoblinGrub[0] = 29;
				for (var frame = 0; frame < 200 && s.StoredCount(pile, Goblins.Sim.Material.Grub) == ProductionRules.GrubPerTend; frame++)
					main._Process(0.1);
				sim.Apply(new SetPaused(true));
				Require(s.StoredCount(pile, Goblins.Sim.Material.Grub) == ProductionRules.GrubPerTend - 1, "A hungry Grunt did not eat a Grub from the Pile.");
			});

			// The Fungus Farm Tool follows the Pile's painting rules, and skips the Surface: Floor only, z1 or deeper, one Room per tile, right-drag removes only farms.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				for (var x = 9; x <= 14; x++)
					s.TileKinds[s.TileIndex(x, 10, 1)] = (byte)TileKind.Floor;
				s.TileKinds[s.TileIndex(13, 10, 1)] = (byte)TileKind.Stairs;
				sim.Apply(new PaintPile(1, 10, 10, 10, 10));
				sim.RecheckReachability();
				Key(main, Godot.Key.F);
				Drag(main, view, MouseButton.Left, 9, 10, 14, 10);
				Require(CountRooms(s, RoomKind.FungusFarm) == 0, "A Fungus Farm was painted on the Surface.");
				Key(main, Godot.Key.Period);
				Drag(main, view, MouseButton.Left, 14, 10, 9, 10);
				Require(CountRooms(s, RoomKind.FungusFarm) == 4, $"{CountRooms(s, RoomKind.FungusFarm)} Fungus Farm tiles were painted on z1, not 4.");
				Require(s.RoomAt(s.TileIndex(10, 10, 1)) == RoomKind.Pile, "Painting a Fungus Farm replaced a Pile.");
				Require(s.RoomAt(s.TileIndex(13, 10, 1)) == RoomKind.None, "Painting a Fungus Farm covered Stairs.");

				// Right-drag with the Pile Tool leaves farms alone; with the farm Tool it takes them and leaves the Pile.
				Key(main, Godot.Key.P);
				Drag(main, view, MouseButton.Right, 9, 10, 14, 10);
				Require(CountRooms(s, RoomKind.FungusFarm) == 4 && s.RoomAt(s.TileIndex(10, 10, 1)) == RoomKind.None, "Right-drag with the Pile Tool did not take only the Pile.");
				Key(main, Godot.Key.F);
				Drag(main, view, MouseButton.Right, 9, 10, 14, 10);
				Require(CountRooms(s, RoomKind.FungusFarm) == 0, "Right-drag with the Fungus Farm Tool left farms behind.");
			});

			// The real game: Grunts take stairs they dug down to z1, dig a room, tend a Fungus Farm painted there, and haul its Fungus up to a Pile on the Surface.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				Array.Clear(s.TileItems);
				Array.Fill(s.GoblinGrub, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinGrog, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinSleep, (byte)NeedRules.MeterMax);
				var pile = s.TileIndex(10, 10, 0);
				Key(main, Godot.Key.P);
				Drag(main, view, MouseButton.Left, 10, 10, 10, 10);
				Key(main, Godot.Key.S);
				Drag(main, view, MouseButton.Left, 20, 16, 20, 16);
				sim.Apply(new SetPaused(false));
				sim.Apply(new SetSpeed(4));
				var sawZ1 = false;
				var frames = 0;
				void Run(Func<bool> done, string failure)
				{
					for (var frame = 0; !done(); frame++)
					{
						Require(frame < 300 && frames++ < 600, failure);
						main._Process(0.25);
						sawZ1 |= Enumerable.Range(0, s.GoblinCount).Any(g => s.GoblinZ[g] == 1);
						Require(GoblinTasks.FindViolation(s) is null, "A Task rule broke while the Grunts went underground.");
					}
				}

				Run(() => s.KindAt(s.TileIndex(20, 16, 0)) == TileKind.Stairs, "The surface stairs were never dug.");
				Key(main, Godot.Key.Period);
				Drag(main, view, MouseButton.Left, 20, 16, 20, 16);
				Run(() => s.KindAt(s.TileIndex(20, 16, 1)) == TileKind.Stairs, "The stairs down to z1 were never dug.");
				Key(main, Godot.Key.D);
				Drag(main, view, MouseButton.Left, 21, 16, 22, 16);
				Run(() => s.KindAt(s.TileIndex(21, 16, 1)) == TileKind.Floor && s.KindAt(s.TileIndex(22, 16, 1)) == TileKind.Floor, "The z1 room was never dug.");
				Key(main, Godot.Key.F);
				Drag(main, view, MouseButton.Left, 21, 16, 22, 16);
				Require(CountRooms(s, RoomKind.FungusFarm) == 2, "The Fungus Farm was not painted on z1.");
				var tended = false;
				Run(() =>
				{
					tended |= Enumerable.Range(0, s.GoblinCount).Any(g => (GoblinTask)s.GoblinTask[g] == GoblinTask.Tending && s.GoblinZ[g] == 1);
					return s.StoredCount(pile, Goblins.Sim.Material.Fungus) >= 1;
				}, "No Fungus reached the Pile.");
				sim.Apply(new SetPaused(true));
				Require(sawZ1 && tended, "No Grunt went underground to tend the farm.");
			});

			// The Brewery Tool follows the Pile's painting rules at any depth: Floor only, one Room per tile, right-drag removes only Breweries.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				sim.Apply(new PaintPile(0, 10, 10, 10, 10));
				s.TileKinds[s.TileIndex(13, 10, 0)] = (byte)TileKind.Stairs;
				sim.RecheckReachability();
				Key(main, Godot.Key.B);
				Drag(main, view, MouseButton.Left, 14, 10, 9, 10);
				Require(CountRooms(s, RoomKind.Brewery) == 4, $"{CountRooms(s, RoomKind.Brewery)} Brewery tiles were painted, not 4.");
				Require(s.RoomAt(s.TileIndex(10, 10, 0)) == RoomKind.Pile, "Painting a Brewery replaced a Pile.");
				Require(s.RoomAt(s.TileIndex(13, 10, 0)) == RoomKind.None, "Painting a Brewery covered Stairs.");
				Key(main, Godot.Key.P);
				Drag(main, view, MouseButton.Right, 9, 10, 14, 10);
				Require(CountRooms(s, RoomKind.Brewery) == 4 && s.RoomAt(s.TileIndex(10, 10, 0)) == RoomKind.None, "Right-drag with the Pile Tool did not take only the Pile.");
				Key(main, Godot.Key.B);
				Drag(main, view, MouseButton.Right, 9, 10, 14, 10);
				Require(CountRooms(s, RoomKind.Brewery) == 0, "Right-drag with the Brewery Tool left Breweries behind.");
			});

			// The real game: Fungus is farmed on z1, brewed on z1 into Grog, the Grog is hauled up to a Pile on the Surface, and a thirsty Grunt drinks it.
			// The starting Grub and Grog are cleared so that the Grog drunk can only be what the Grunts brewed.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				Array.Clear(s.TileItems);
				Array.Fill(s.GoblinGrub, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinGrog, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinSleep, (byte)NeedRules.MeterMax);
				var pile = s.TileIndex(10, 10, 0);
				Key(main, Godot.Key.P);
				Drag(main, view, MouseButton.Left, 10, 10, 10, 10);
				Key(main, Godot.Key.S);
				Drag(main, view, MouseButton.Left, 20, 16, 20, 16);
				sim.Apply(new SetPaused(false));
				sim.Apply(new SetSpeed(4));
				var frames = 0;
				var brewed = false;
				void Run(Func<bool> done, string failure)
				{
					while (!done())
					{
						Require(frames++ < 1500, failure);
						main._Process(0.25);
						brewed |= Enumerable.Range(0, s.GoblinCount).Any(g => (GoblinTask)s.GoblinTask[g] == GoblinTask.Brewing && s.GoblinZ[g] == 1);
						Require(GoblinTasks.FindViolation(s) is null, "A Task rule broke while the Grunts brewed.");
					}
				}

				Run(() => s.KindAt(s.TileIndex(20, 16, 0)) == TileKind.Stairs, "The surface stairs were never dug.");
				Key(main, Godot.Key.Period);
				Drag(main, view, MouseButton.Left, 20, 16, 20, 16);
				Run(() => s.KindAt(s.TileIndex(20, 16, 1)) == TileKind.Stairs, "The stairs down to z1 were never dug.");
				Key(main, Godot.Key.D);
				Drag(main, view, MouseButton.Left, 21, 16, 24, 16);
				Run(() => Enumerable.Range(21, 4).All(x => s.KindAt(s.TileIndex(x, 16, 1)) == TileKind.Floor), "The z1 room was never dug.");
				Key(main, Godot.Key.F);
				Drag(main, view, MouseButton.Left, 21, 16, 22, 16);
				Key(main, Godot.Key.B);
				Drag(main, view, MouseButton.Left, 23, 16, 24, 16);
				Require(CountRooms(s, RoomKind.FungusFarm) == 2 && CountRooms(s, RoomKind.Brewery) == 2, "The farm and the Brewery were not painted on z1.");
				Run(() => brewed && s.StoredCount(pile, Goblins.Sim.Material.Grog) >= 1, "No brewed Grog reached the Pile.");

				// A thirsty Grunt drinks the brewed Grog off the Pile.
				s.GoblinGrog[0] = 29;
				var drank = false;
				Run(() =>
				{
					drank |= (GoblinTask)s.GoblinTask[0] == GoblinTask.Drinking;
					return drank && s.GoblinGrog[0] > 60;
				}, "The thirsty Grunt never drank the brewed Grog.");
				sim.Apply(new SetPaused(true));
				Require(brewed, "No Grunt brewed on z1.");
			});

			foreach (var button in new[] { MouseButton.Left, MouseButton.Right })
			foreach (var key in new[] { Godot.Key.D, Godot.Key.S, Godot.Key.P, Godot.Key.G, Godot.Key.Escape, Godot.Key.Period, Godot.Key.Comma })
				WithMain((main, view, sim) =>
				{
					Key(main, Godot.Key.S);
					sim.Apply(new Designate(DesignationKind.DigStairs, 0, 4, 4, 7, 7));
					var before = SimJson.Serialize(sim);
					Button(main, view, button, true, 4, 4);
					Move(main, view, 7, 7);
					Require(view.Selection is not null, "Drag did not show its preview.");
					Key(main, key);
					Require(view.Selection is null, $"{key} kept a stale drag preview.");
					Button(main, view, button, false, 7, 7);
					Require(SimJson.Serialize(sim) == before, $"{key} allowed a cancelled {button} drag to submit.");
				});

			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				sim.Apply(new PaintPile(0, 4, 4, 5, 4));
				s.AddItems(s.TileIndex(4, 4, 0), Goblins.Sim.Material.Grub, 14);
				s.AddItems(s.TileIndex(4, 4, 0), Goblins.Sim.Material.Stone, SimState.PileCapacity);
				s.AddItems(s.TileIndex(4, 4, 0), Goblins.Sim.Material.Grog, 7);
				s.AddItems(s.TileIndex(6, 4, 0), Goblins.Sim.Material.Dirt, 2);
				sim.RecheckReachability();

				// The Inspector names every count on the Pile tile under the cursor, in Material order, full ones marked.
				Move(main, view, 4, 4);
				ExpectInspection(main, "Pile", "Stone 30 full, Grub 14, Grog 7");
				Move(main, view, 5, 4);
				ExpectInspection(main, "Pile", "", "An empty Pile, waiting with great dignity.");
				// A loose item is not on a Pile: the Inspector shows the Floor it lies on, and the unit, which has a Pile to go to.
				Move(main, view, 6, 4);
				ExpectInspection(main, "Floor", "Dirt 2");
				Move(main, view, 7, 4);
				ExpectInspection(main, "Floor", "");
				// Off the map there is nothing to inspect.
				Move(main, view, 4, 4);
				Move(main, view, -1, 4);
				ExpectInspection(main, "", "");
				// The Inspector follows the count as the Pile fills, and the level when it changes.
				Move(main, view, 5, 4);
				s.AddItems(s.TileIndex(5, 4, 0), Goblins.Sim.Material.Fungus, 3);
				sim.Apply(new SetPaused(false));
				main._Process(0.1);
				sim.Apply(new SetPaused(true));
				ExpectInspection(main, "Pile", "Fungus 3");
				Key(main, Godot.Key.Period);
				ExpectInspection(main, "Solid dirt", "");
			});

			// The Inspector says what any other tile is and what is marked on it.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				sim.Apply(new Designate(DesignationKind.DigStairs, 0, 3, 3, 3, 3));
				sim.Apply(new PaintRoom(RoomKind.Brewery, 0, 4, 3, 4, 3));
				Move(main, view, 3, 3);
				ExpectInspection(main, "Floor", "");
				Require(main.Inspection.Doing == "Marked for stairs", $"A stairs Designation reads \"{main.Inspection.Doing}\".");
				Move(main, view, 4, 3);
				ExpectInspection(main, "Brewery", "");
				Key(main, Godot.Key.Period);
				sim.Apply(new Designate(DesignationKind.Dig, 1, 3, 3, 3, 3));
				Move(main, view, 3, 3);
				ExpectInspection(main, "Solid dirt", "");
				Require(main.Inspection.Doing == "Marked for digging", $"A dig Designation reads \"{main.Inspection.Doing}\".");
				Require(main.Inspection.Note == "No goblin can reach it yet.", $"An unreachable dig says \"{main.Inspection.Note}\".");
			});

			// The real game: the starting Grub and Grog are hauled to a Pile painted with the Pile Tool, and the Inspector reads it.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				Require(s.GoblinCount == 7, $"A new warren has {s.GoblinCount} Grunts, not 7.");
				// The haul runs 1200 ticks; fed to the brim, no Grunt is hungry or thirsty enough to consume the Grub and Grog it is hauling.
				Array.Fill(s.GoblinGrub, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinGrog, (byte)NeedRules.MeterMax);
				Key(main, Godot.Key.P);
				Drag(main, view, MouseButton.Left, 12, 12, 13, 13);
				sim.Apply(new SetPaused(false));
				sim.Apply(new SetSpeed(4));
				for (var frame = 0; frame < 120; frame++)
					main._Process(0.25);
				int Stored(Goblins.Sim.Material material) =>
					new[] { (12, 12), (13, 12), (12, 13), (13, 13) }.Sum(p => s.StoredCount(s.TileIndex(p.Item1, p.Item2, 0), material));
				Require(Stored(Goblins.Sim.Material.Grub) == 14 && Stored(Goblins.Sim.Material.Grog) == 7,
					$"The Pile holds {Stored(Goblins.Sim.Material.Grub)} Grub and {Stored(Goblins.Sim.Material.Grog)} Grog, not 14 and 7.");
				sim.Apply(new SetPaused(true));
				// A goblin standing on the tile would put its own meter on the line, so pick a stocked tile with none.
				var (stockedX, stockedY) = new[] { (12, 12), (13, 12), (12, 13), (13, 13) }
					.First(p => s.StoredCount(s.TileIndex(p.Item1, p.Item2, 0), Goblins.Sim.Material.Grub) > 0
						&& GoblinHover.GoblinAt(s, s.TileIndex(p.Item1, p.Item2, 0)) < 0);
				Move(main, view, stockedX, stockedY);
				Require(main.Inspection.Title == "Pile" && main.Inspection.Stock.Any(line => line.Material == Goblins.Sim.Material.Grub),
					$"The Inspector over the stocked Pile shows {main.Inspection.Title}: {StockText(main.Inspection)}.");
			});

			// The Inspector shows the Grub, Grog and Sleep meters of the goblin under the cursor, ahead of any Pile under it.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				(s.GoblinGrub[0], s.GoblinGrub[1], s.GoblinGrub[2], s.GoblinGrub[3]) = (74, 12, 0, 90);
				(s.GoblinGrog[0], s.GoblinGrog[1], s.GoblinGrog[2], s.GoblinGrog[3]) = (91, 80, 100, 8);
				(s.GoblinSleep[0], s.GoblinSleep[1], s.GoblinSleep[2], s.GoblinSleep[3]) = (85, 60, 100, 45);
				(s.GoblinGrub[4], s.GoblinGrog[4], s.GoblinSleep[4]) = (100, 100, 12);
				(s.GoblinGrub[5], s.GoblinGrog[5], s.GoblinSleep[5]) = (100, 100, 40);
				GoblinTasks.BeginSleep(s, 5, dropTile: -1, asleep: true);
				Move(main, view, s.GoblinX[0], s.GoblinY[0]);
				ExpectGoblin(main, 74, 91, 85, "At peace with the world, and with lunch.");
				Require(main.Inspection.Doing == "Standing about, between jobs", $"An idle Grunt is \"{main.Inspection.Doing}\".");
				Move(main, view, s.GoblinX[1], s.GoblinY[1]);
				ExpectGoblin(main, 12, 80, 60, "Thinking about nothing but Grub.");
				Move(main, view, s.GoblinX[2], s.GoblinY[2]);
				ExpectGoblin(main, 0, 100, 100, "Starving, and bearing it at half speed with great dignity.");
				Move(main, view, s.GoblinX[3], s.GoblinY[3]);
				ExpectGoblin(main, 90, 8, 45, "Thinking about nothing but Grog.");
				Move(main, view, s.GoblinX[4], s.GoblinY[4]);
				ExpectGoblin(main, 100, 100, 12, "Thinking about nothing but a lie-down, on anything flat.");
				Move(main, view, s.GoblinX[5], s.GoblinY[5]);
				ExpectGoblin(main, 100, 100, 40, "Fast asleep. Hunger and thirst may take a number.");
				Require(main.Inspection.Doing == "Asleep on the bare floor", $"A Grunt asleep on the floor is \"{main.Inspection.Doing}\".");
				sim.Apply(new PaintPile(0, s.GoblinX[0], s.GoblinY[0], s.GoblinX[0], s.GoblinY[0]));
				Move(main, view, s.GoblinX[0], s.GoblinY[0]);
				Require(main.Inspection.Title == "Grunt", $"A Pile hid the goblin on it: the Inspector shows {main.Inspection.Title}.");
				// The meters follow the sim as it drains them, and a goblin on another level is not under the cursor.
				s.GoblinGrub[0] = 74;
				s.GoblinGrog[0] = 91;
				s.Tick = 34;
				sim.Apply(new SetPaused(false));
				main._Process(0.1);
				sim.Apply(new SetPaused(true));
				ExpectGoblin(main, 74, 90, 85, "At peace with the world, and with lunch.");
				Key(main, Godot.Key.Period);
				ExpectInspection(main, "Solid dirt", "");
			});

			// The real game: hungry Grunts leave a dig for the starting Grub, which is eaten where it lies, and the dig carries on.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				Array.Fill(s.GoblinGrub, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinGrog, (byte)NeedRules.MeterMax);
				Key(main, Godot.Key.S);
				Drag(main, view, MouseButton.Left, 6, 6, 9, 7);
				sim.Apply(new SetPaused(false));
				var frames = 0;
				while (!Enumerable.Range(0, s.GoblinCount).Any(g => (GoblinTask)s.GoblinTask[g] == GoblinTask.Digging) && frames++ < 200)
					main._Process(0.1);
				Require(frames < 200, "No Grunt started digging.");
				var digger = Enumerable.Range(0, s.GoblinCount).First(g => (GoblinTask)s.GoblinTask[g] == GoblinTask.Digging);
				Move(main, view, s.GoblinX[digger], s.GoblinY[digger]);
				Require(GoblinHover.GoblinAt(s, s.TileIndex(s.GoblinX[digger], s.GoblinY[digger], 0)) != digger
					|| main.Inspection.Doing == "Digging", $"A digging Grunt is \"{main.Inspection.Doing}\".");
				// Hunger strikes mid-dig.
				Array.Fill(s.GoblinGrub, (byte)29, 0, s.GoblinCount);
				main._Process(0.1);
				Require((GoblinTask)s.GoblinTask[digger] is GoblinTask.Seeking or GoblinTask.Eating,
					$"The digging Grunt went on as {(GoblinTask)s.GoblinTask[digger]} instead of going to eat.");
				Require(GoblinTasks.FindViolation(s) is null, "A Task rule broke while the Grunts went to eat.");
				sim.Apply(new SetSpeed(4));
				for (var frame = 0; frame < 40; frame++)
					main._Process(0.25);
				sim.Apply(new SetPaused(true));
				var grub = Enumerable.Range(0, s.Piles.Length).Sum(t => s.ItemCount(t, Goblins.Sim.Material.Grub));
				Require(grub == 14 - 7, $"{14 - grub} units of Grub were eaten by seven hungry Grunts, not 7.");
				Require(Enumerable.Range(0, s.GoblinCount).All(g => !NeedRules.IsCraving(s.GoblinGrub[g])), "A Grunt is still hungry after the meal.");
				Require(Enumerable.Range(0, 4 * 2).All(i => s.DesignationAt(s.TileIndex(6 + i % 4, 6 + i / 4, 0)) == DesignationKind.None),
					"The dig was never finished after the meals.");
			});

			// The real game: a Grunt stuck holding dirt, with the only Pile filled up, leaves the dirt on the floor and walks off for the starting Grog.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				// The one Grunt, its dirt and the Grog are all there is, so nothing else bears on the wait.
				s.GoblinCount = 1;
				Array.Clear(s.TileItems);
				Array.Fill(s.GoblinGrub, (byte)NeedRules.MeterMax);
				s.GoblinGrog[0] = 40;
				var pile = s.TileIndex(12, 12, 0);
				s.AddItems(pile, Goblins.Sim.Material.Dirt, SimState.PileCapacity - 1);
				s.AddItems(s.TileIndex(s.GoblinX[0], s.GoblinY[0], 0), Goblins.Sim.Material.Dirt, 1);
				s.AddItems(s.TileIndex(22, 16, 0), Goblins.Sim.Material.Grog, 1);
				Key(main, Godot.Key.P);
				Drag(main, view, MouseButton.Left, 12, 12, 12, 12);
				Require(s.StoredCount(pile, Goblins.Sim.Material.Dirt) == SimState.PileCapacity - 1, "The Pile was not painted over its dirt.");

				sim.Apply(new SetPaused(false));
				var frames = 0;
				while ((GoblinTask)s.GoblinTask[0] != GoblinTask.Carrying && frames++ < 100)
					main._Process(0.1);
				Require(frames < 100, "The Grunt never picked the dirt up.");
				// The last slot of the Pile goes to some other hauler's drop, so the Grunt waits holding the dirt, and its Grog drains twice as fast.
				s.AddItems(pile, Goblins.Sim.Material.Dirt, 1);
				sim.RecheckReachability();
				Require(s.GoblinCarry[0] == (byte)Goblins.Sim.Material.Dirt && NeedsSystem.IsWaitingCarrier(s, 0),
					"The Grunt did not wait holding the dirt.");
				var grog = s.GoblinGrog[0];
				for (var frame = 0; frame < 35; frame++)
					main._Process(0.1);
				Require((GoblinTask)s.GoblinTask[0] == GoblinTask.Carrying && s.GoblinGrog[0] == grog - 2,
					$"The waiting Grunt's Grog went from {grog} to {s.GoblinGrog[0]} in 35 ticks, not by 2.");

				frames = 0;
				while ((GoblinTask)s.GoblinTask[0] != GoblinTask.SeekingGrog && frames++ < 400)
				{
					main._Process(0.1);
					Require(GoblinTasks.FindViolation(s) is null, "A Task rule broke while the Grunt waited.");
				}
				Require(frames < 400, "The waiting Grunt never went for a drink.");
				Require(s.GoblinCarry[0] == (byte)Goblins.Sim.Material.None
					&& s.LooseCount(s.TileIndex(s.GoblinX[0], s.GoblinY[0], 0), Goblins.Sim.Material.Dirt) == 1,
					"The Grunt did not leave the dirt on the floor where it stood.");

				sim.Apply(new SetSpeed(4));
				for (var frame = 0; frame < 20; frame++)
					main._Process(0.25);
				sim.Apply(new SetPaused(true));
				Require(s.ItemCount(s.TileIndex(22, 16, 0), Goblins.Sim.Material.Grog) == 0, "The Grog was not drunk.");
				Require(s.GoblinGrog[0] > 70, $"The Grunt's Grog meter is {s.GoblinGrog[0]} after the drink.");
				Require(s.StoredCount(pile, Goblins.Sim.Material.Dirt) == SimState.PileCapacity, "The full Pile gained or lost dirt.");
				Require(GoblinTasks.FindViolation(s) is null, "A Task rule broke at the end of the drink.");
			});

			// The top bar's buttons apply the same Commands as the keys, and every button shows what the keys did too.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				// The pause was applied straight to the sim, so the HUD hears of it next frame.
				main._Process(0);
				Require(ButtonNamed(main, "Pause").ButtonPressed, "The Pause button does not show the paused warren.");
				Press(main, "Pause");
				Require(!s.Paused && !ButtonNamed(main, "Pause").ButtonPressed, "The Pause button did not unpause the warren.");
				Key(main, Godot.Key.Space);
				Require(s.Paused && ButtonNamed(main, "Pause").ButtonPressed, "Space paused the warren but the button did not show it.");

				Press(main, "Speed4");
				ExpectSpeed(main, s, 4);
				Key(main, Godot.Key.Key2);
				ExpectSpeed(main, s, 2);
				// A click on the speed already chosen keeps it chosen.
				Press(main, "Speed2");
				ExpectSpeed(main, s, 2);

				Require(ButtonNamed(main, "DepthUp").Disabled && !ButtonNamed(main, "DepthDown").Disabled, "At the Surface, only Down should work.");
				Require(main.DepthText == "Depth 0 · Surface", $"The depth reads \"{main.DepthText}\".");
				Press(main, "DepthDown");
				Require(view.ViewZ == 1 && main.DepthText == "Depth 1 · Dirt", $"Down showed z{view.ViewZ}, \"{main.DepthText}\".");
				Press(main, "DepthDown");
				Require(view.ViewZ == 2 && ButtonNamed(main, "DepthDown").Disabled, "At the bottom level, Down should not work.");
				Key(main, Godot.Key.Comma);
				Require(view.ViewZ == 1 && !ButtonNamed(main, "DepthDown").Disabled && !ButtonNamed(main, "DepthUp").Disabled,
					"The comma key went up a level but the buttons did not follow.");

				s.Tick = 1234;
				Press(main, "Speed1");
				Press(main, "Pause");
				main._Process(0.1);
				Require(main.ClockText == "Tick 1235", $"The clock reads \"{main.ClockText}\" a tick after 1234.");
			});

			// The Tool buttons pick Tools as the keys do, and show the Tool picked whichever way it was picked.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				ExpectTool(main, null);
				var idleHint = main.ToolHint;
				Press(main, "ToolDigStairs");
				ExpectTool(main, "ToolDigStairs");
				Drag(main, view, MouseButton.Left, 4, 4, 5, 5);
				ExpectRectangle(s, 0, DesignationKind.DigStairs, 4, 4, 5, 5);
				Key(main, Godot.Key.P);
				ExpectTool(main, "ToolPile");
				Require(main.ToolHint != idleHint, "The hint did not change when the Pile Tool was picked.");
				// A click on the picked Tool puts it away, as its key does.
				Press(main, "ToolPile");
				ExpectTool(main, null);
				Require(main.ToolHint == idleHint, "The hint still describes a Tool that was put away.");
				Key(main, Godot.Key.H);
				ExpectTool(main, "ToolHeap");
				Key(main, Godot.Key.Escape);
				ExpectTool(main, null);
				Drag(main, view, MouseButton.Left, 8, 8, 9, 9);
				ExpectRectangle(s, 0, DesignationKind.None, 8, 8, 9, 9);
				Require(CountRooms(s, RoomKind.SleepingHeap) == 0, "A put-away Tool still painted.");
			});

			// The Warren summary totals every material, stored and loose, and counts each goblin once by state.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				var (dirt, stone, grub, grog) = (Goblins.Sim.Material.Dirt, Goblins.Sim.Material.Stone, Goblins.Sim.Material.Grub, Goblins.Sim.Material.Grog);
				Array.Clear(s.TileItems);
				Array.Fill(s.GoblinGrub, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinGrog, (byte)NeedRules.MeterMax);
				Array.Fill(s.GoblinSleep, (byte)NeedRules.MeterMax);
				sim.Apply(new PaintPile(0, 4, 4, 5, 4));
				s.AddItems(s.TileIndex(4, 4, 0), grub, 14);
				s.AddItems(s.TileIndex(5, 4, 0), grub, 3);
				s.AddItems(s.TileIndex(5, 4, 0), stone, SimState.PileCapacity);
				s.AddItems(s.TileIndex(8, 8, 0), grub, 2);
				s.AddItems(s.TileIndex(8, 8, 0), dirt, 5);
				s.GoblinSleep[0] = 20;
				GoblinTasks.BeginSleep(s, 0, dropTile: -1, asleep: true);
				s.GoblinGrub[1] = 10;
				s.GoblinGrub[2] = 0;
				main._Process(0);
				var tally = main.Summary;
				Require(tally.Stored(grub) == 17 && tally.Loose(grub) == 2, $"Grub reads {tally.Stored(grub)} stored, {tally.Loose(grub)} loose, not 17 and 2.");
				Require(tally.Stored(stone) == 30 && tally.Loose(stone) == 0, $"Stone reads {tally.Stored(stone)} stored, {tally.Loose(stone)} loose, not 30 and 0.");
				Require(tally.Stored(dirt) == 0 && tally.Loose(dirt) == 5, $"Dirt reads {tally.Stored(dirt)} stored, {tally.Loose(dirt)} loose, not 0 and 5.");
				Require(tally.Stored(grog) == 0 && tally.Loose(grog) == 0, "Grog was counted where there is none.");
				// The sleeper craves Sleep but counts as asleep; the Starving goblin counts as craving, and as Starving too.
				Require((tally.Goblins, tally.Asleep, tally.Craving, tally.Idle, tally.Working, tally.Starving) == (7, 1, 2, 4, 0, 1),
					$"Goblins read {tally.Goblins}: {tally.Asleep} asleep, {tally.Craving} craving, {tally.Idle} idle, {tally.Working} working, {tally.Starving} Starving.");

				// A unit in a goblin's hands is loose: hauled to the Pile, the dirt moves from loose to stored and is never lost on the way.
				Array.Fill(s.GoblinGrub, (byte)NeedRules.MeterMax);
				sim.RecheckReachability();
				sim.Apply(new SetPaused(false));
				var carried = false;
				for (var frame = 0; frame < 400 && tally.Stored(dirt) < 5; frame++)
				{
					main._Process(0.1);
					tally = main.Summary;
					Require(tally.Stored(dirt) + tally.Loose(dirt) == 5, $"Dirt reads {tally.Stored(dirt)} stored and {tally.Loose(dirt)} loose mid-haul, not 5 in all.");
					// Each carrier is itself counted as working, not merely some goblin somewhere.
					foreach (var carrier in Enumerable.Range(0, s.GoblinCount).Where(g => s.GoblinCarry[g] == (byte)dirt))
					{
						Require(WarrenSummary.StateOf(s, carrier) == WarrenSummary.GoblinState.Working,
							$"Goblin {carrier}, hauling dirt as {(GoblinTask)s.GoblinTask[carrier]}, counts as {WarrenSummary.StateOf(s, carrier)}.");
						carried = true;
					}
					Require(tally.Asleep + tally.Craving + tally.Idle + tally.Working == tally.Goblins, "A goblin was counted twice or not at all.");
				}
				Require(carried && tally.Stored(dirt) == 5, $"The dirt was not all hauled: {tally.Stored(dirt)} stored.");
			});

			// When the map moves under a still pointer (the window resized, a panel grown), the Inspector and the outline follow
			// the pointer to the cell now under it, paused or not.
			WithMain((main, view, sim) =>
			{
				var s = sim.State;
				Move(main, view, s.GoblinX[0], s.GoblinY[0]);
				Require(main.Inspection.Title == "Grunt", $"The Inspector over a Grunt shows {main.Inspection.Title}.");
				view.Position += new Vector2(9 * WarrenView.CellSize, 0);
				main._Process(0);
				var under = new Vector2I(s.GoblinX[0] - 9, s.GoblinY[0]);
				Require(view.Hover == under, $"The map outlines {view.Hover} after moving; the pointer is over {under}.");
				var expected = Inspector.Inspect(s, s.TileIndex(under.X, under.Y, 0));
				ExpectInspection(main, expected.Title, StockText(expected), expected.Note);
			});

			// The drag box wears the colour of what it will make, red when it unmakes; the hovered cell is outlined on the map.
			WithMain((main, view, sim) =>
			{
				Move(main, view, 6, 7);
				Require(view.Hover == new Vector2I(6, 7), $"The map outlines {view.Hover} under the cursor at (6, 7).");
				Move(main, view, -3, 7);
				Require(view.Hover is null, "The map outlines a cell with the cursor off the map.");

				Key(main, Godot.Key.G);
				Button(main, view, MouseButton.Left, true, 4, 4);
				Require(view.SelectionColor == ToolInfo.Swatch(Tool.GrubPit), "A Grub Pit drag is not drawn in the Grub Pit's colour.");
				Button(main, view, MouseButton.Left, false, 4, 4);
				Button(main, view, MouseButton.Right, true, 4, 4);
				Require(view.SelectionColor == WarrenView.RemovalColor, "A right-drag is not drawn in the removal colour.");
				Button(main, view, MouseButton.Right, false, 4, 4);
				Key(main, Godot.Key.D);
				Button(main, view, MouseButton.Left, true, 4, 4);
				Require(view.SelectionColor == ToolInfo.Swatch(Tool.Dig), "A dig drag is not drawn in the Designation colour.");
				Button(main, view, MouseButton.Left, false, 4, 4);
			});

			await RealMouseCheck();
			CountLabelChecks();
			PileCountChecks();

			GD.Print($"Selection input checks passed ({_scenarios} scenarios, count labels, pile counts).");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	// A Pile tile holds at most 30 of a material, but a label must still read inside its 18 px cell, with the 2 px left inset, for any count.
	static void CountLabelChecks()
	{
		var expected = new (int Count, string Label)[]
		{
			(0, ""), (-1, ""), (1, "1"), (9, "9"), (10, "10"), (99, "99"), (100, "100"), (999, "999"),
			(1000, "1k"), (1999, "1k"), (2000, "2k"), (9999, "9k"), (10_000, "10k"), (99_999, "99k"),
			(100_000, "99k"), (1_000_000, "99k"), (int.MaxValue, "99k"),
		};
		var font = ThemeDB.FallbackFont;
		foreach (var (count, label) in expected)
		{
			var actual = CountLabel.Format(count);
			Require(actual == label, $"CountLabel.Format({count}) was \"{actual}\", expected \"{label}\".");
			var width = font.GetStringSize(actual, HorizontalAlignment.Left, -1, WarrenView.CountFontSize).X;
			Require(width + 2 <= WarrenView.CellSize, $"Count {count} (\"{actual}\") is {width} px wide and overflows its cell.");
		}
	}

	static Godot.Button ButtonNamed(Main main, string name) =>
		main.FindChild(name, recursive: true, owned: false) as Godot.Button ?? throw new InvalidOperationException($"No button named {name}.");

	// What a click does: a toggle button flips, then every button emits pressed.
	static void Press(Main main, string name)
	{
		var button = ButtonNamed(main, name);
		Require(!button.Disabled, $"The {name} button was clicked while disabled.");
		if (button.ToggleMode)
			button.SetPressedNoSignal(!button.ButtonPressed);
		button.EmitSignal(BaseButton.SignalName.Pressed);
	}

	static readonly string[] ToolButtons = ["ToolDig", "ToolDigStairs", "ToolPile", "ToolGrubPit", "ToolFungusFarm", "ToolBrewery", "ToolHeap"];

	// Exactly the named Tool button is down, or none for null.
	static void ExpectTool(Main main, string? picked)
	{
		foreach (var name in ToolButtons)
			Require(ButtonNamed(main, name).ButtonPressed == (name == picked), $"The {name} button is wrong with {picked ?? "no Tool"} picked.");
	}

	static void ExpectSpeed(Main main, SimState s, int speed)
	{
		Require(s.Speed == speed, $"The warren runs at {s.Speed}×, not {speed}×.");
		foreach (var each in new[] { 1, 2, 4 })
			Require(ButtonNamed(main, $"Speed{each}").ButtonPressed == (each == speed), $"The {each}× button is wrong at {speed}×.");
	}

	// Every stock line as "Material count", with "full" and "waiting" marked, in the order shown.
	static string StockText(Inspection inspection) =>
		string.Join(", ", inspection.Stock.Select(line => $"{line.Material} {line.Count}{(line.Full ? " full" : "")}{(line.Waiting ? " waiting" : "")}"));

	static void ExpectInspection(Main main, string title, string stock, string? note = null)
	{
		var inspection = main.Inspection;
		Require(inspection.Title == title && StockText(inspection) == stock && (note is null || inspection.Note == note),
			$"The Inspector shows \"{inspection.Title}\" [{StockText(inspection)}] \"{inspection.Note}\", expected \"{title}\" [{stock}] \"{note}\".");
	}

	static void ExpectGoblin(Main main, int grub, int grog, int sleep, string note)
	{
		var inspection = main.Inspection;
		var meters = string.Join(" ", inspection.Meters.Select(meter => $"{meter.Need} {meter.Value}"));
		var expected = $"Grub {grub} Grog {grog} Sleep {sleep}";
		Require(inspection.Title == "Grunt" && meters == expected && inspection.Note == note,
			$"The Inspector shows \"{inspection.Title}\" [{meters}] \"{inspection.Note}\", expected a Grunt [{expected}] \"{note}\".");
	}

	// A Pile tile draws two counts: full materials first, then the larger count, and Material order on a tie. Counts are
	// given in Material order: Dirt, Stone, Grub, Fungus, Grog.
	static void PileCountChecks()
	{
		var (none, dirt, stone, grub, fungus, grog) = (Goblins.Sim.Material.None, Goblins.Sim.Material.Dirt, Goblins.Sim.Material.Stone,
			Goblins.Sim.Material.Grub, Goblins.Sim.Material.Fungus, Goblins.Sim.Material.Grog);
		var cases = new (int[] Counts, Goblins.Sim.Material First, Goblins.Sim.Material Second)[]
		{
			([0, 0, 0, 0, 0], none, none),
			([0, 0, 0, 0, 5], grog, none),
			([5, 30, 0, 0, 0], stone, dirt),
			([30, 30, 0, 0, 0], dirt, stone),
			([29, 28, 30, 1, 30], grub, grog),
			([3, 20, 7, 20, 2], stone, fungus),
			([10, 10, 10, 10, 10], dirt, stone),
			([29, 30, 29, 29, 29], stone, dirt),
			([1, 2, 3, 4, 5], grog, fungus),
			([30, 1, 0, 0, 0], dirt, stone),
			([0, 0, 29, 30, 0], fungus, grub),
			([0, 0, 0, 30, 30], fungus, grog),
		};
		foreach (var (counts, first, second) in cases)
		{
			var s = Simulation.NewWorld(seed: 1).State;
			var tile = s.TileIndex(4, 4, 0);
			Array.Clear(s.TileItems);
			s.Piles[tile] = 1;
			for (var i = 0; i < counts.Length; i++)
				s.AddItems(tile, (Goblins.Sim.Material)(i + 1), counts[i]);
			PileCounts.Pick(s, tile, out var actualFirst, out var actualSecond);
			Require(actualFirst == first && actualSecond == second,
				$"Counts [{string.Join(", ", counts)}] drew {actualFirst}, {actualSecond}; expected {first}, {second}.");
		}
	}

	// Real mouse events through the viewport and the GUI, after a layout pass: a click on a Tool button picks the Tool and
	// stops there, a press on the map starts a drag the panels let through, and the drag ends where it is released, even
	// over a panel, clamped to the map.
	async Task RealMouseCheck()
	{
		_scenarios++;
		var main = new Main();
		AddChild(main);
		try
		{
			for (var frame = 0; frame < 3; frame++)
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			var view = main.View;
			var sim = view.Sim!;
			var s = sim.State;
			sim.Apply(new SetPaused(true));

			var stairs = ButtonNamed(main, "ToolDigStairs").GetGlobalRect().GetCenter();
			Push(new InputEventMouseMotion { Position = stairs, GlobalPosition = stairs });
			Push(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = stairs, GlobalPosition = stairs });
			Push(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = stairs, GlobalPosition = stairs });
			ExpectTool(main, "ToolDigStairs");
			Require(Enumerable.Range(0, s.Designations.Length).All(t => s.Designations[t] == 0), "A click on a Tool button reached the map.");

			var from = Position(view, 4, 4);
			// Over the right panel, level with row 6.
			var panel = ButtonNamed(main, "ToolDigStairs").GetGlobalRect().Position.X < from.X
				? new Vector2(GetViewport().GetVisibleRect().Size.X - 20, Position(view, 0, 6).Y)
				: throw new InvalidOperationException("The map is not right of the Tool palette.");
			Push(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = from, GlobalPosition = from });
			Push(new InputEventMouseMotion { Position = panel, GlobalPosition = panel, ButtonMask = MouseButtonMask.Left });
			Push(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = panel, GlobalPosition = panel });
			Require(view.Selection is null, "A drag released over a panel never ended.");
			ExpectRectangle(s, 0, DesignationKind.DigStairs, 4, 4, s.Width - 1, 6);
		}
		finally
		{
			RemoveChild(main);
			main.Free();
		}
	}

	// Positions are in the viewport's own coordinates, as the map and the buttons are: under --headless the window is
	// 64 px square, and stretch would rescale anything given in window coordinates.
	void Push(InputEvent input)
	{
		GetViewport().PushInput(input, inLocalCoords: true);
		input.Dispose();
	}

	// Every scenario goes through here, so the passing line reports how many actually ran.
	int _scenarios;

	void WithMain(Action<Main, WarrenView, Simulation> check)
	{
		_scenarios++;
		var main = new Main();
		AddChild(main);
		try
		{
			var view = main.View;
			var sim = view.Sim!;
			sim.Apply(new SetPaused(true));
			check(main, view, sim);
		}
		finally
		{
			RemoveChild(main);
			main.Free();
		}
	}

	static Vector2 Position(WarrenView view, int x, int y) =>
		view.GlobalPosition + new Vector2((x + 0.5f) * WarrenView.CellSize, (y + 0.5f) * WarrenView.CellSize);

	static void Button(Main main, WarrenView view, MouseButton button, bool pressed, int x, int y)
	{
		using var input = new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = Position(view, x, y) };
		main._UnhandledInput(input);
	}

	static void Move(Main main, WarrenView view, int x, int y)
	{
		using var input = new InputEventMouseMotion { Position = Position(view, x, y) };
		main._UnhandledInput(input);
	}

	static void Drag(Main main, WarrenView view, MouseButton button, int x0, int y0, int x1, int y1)
	{
		Button(main, view, button, true, x0, y0);
		Move(main, view, x1, y1);
		Button(main, view, button, false, x1, y1);
	}

	static void Key(Main main, Godot.Key key)
	{
		using var input = new InputEventKey { Keycode = key, Pressed = true };
		main._UnhandledKeyInput(input);
	}

	static void ExpectRectangle(SimState state, int z, DesignationKind kind, int x0, int y0, int x1, int y1)
	{
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
			Require(state.DesignationAt(state.TileIndex(x, y, z)) == kind, $"Expected {kind} at ({x}, {y}, {z}).");
	}

	static void ExpectPiles(SimState state, int z, bool piled, int x0, int y0, int x1, int y1)
	{
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
			Require(state.IsPile(state.TileIndex(x, y, z)) == piled, $"Expected Pile {(piled ? "set" : "clear")} at ({x}, {y}, {z}).");
	}

	static void ExpectRooms(SimState state, RoomKind room, int x0, int y0, int x1, int y1)
	{
		for (var y = y0; y <= y1; y++)
		for (var x = x0; x <= x1; x++)
			Require(state.RoomAt(state.TileIndex(x, y, 0)) == room, $"Expected {room} at ({x}, {y}, 0).");
	}

	static int CountRooms(SimState state, RoomKind room) =>
		Enumerable.Range(0, state.TileKinds.Length).Count(tile => state.RoomAt(tile) == room);

	static void Require(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}
}
