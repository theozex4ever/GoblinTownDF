using System;
using Godot;
using Goblins.Sim;

namespace AThousandGoblinsDeep;

/// <summary>Shared artwork for painted Rooms. Fixtures are visual only and do not occupy or block tiles.</summary>
internal static class RoomSprites
{
	static readonly Texture2D?[] Normal = new Texture2D?[(int)RoomKind.Brewery + 1];
	static Texture2D? _waitingBrewery;

	public static string FileName(RoomKind room) => room switch
	{
		RoomKind.Pile => "pile",
		RoomKind.GrubPit => "grub-pit",
		RoomKind.SleepingHeap => "sleeping-heap",
		RoomKind.FungusFarm => "fungus-farm",
		RoomKind.Brewery => "brewery",
		_ => throw new ArgumentOutOfRangeException(nameof(room), room, "No sprite for this Room."),
	};

	public static Texture2D Get(RoomKind room, bool waiting = false)
	{
		var file = FileName(room);
		var normal = Normal[(int)room] ??= GD.Load<Texture2D>($"res://art/rooms/{file}.png");
		if (!waiting || room != RoomKind.Brewery)
			return normal;
		if (_waitingBrewery == null)
		{
			using var image = normal.GetImage();
			image.AdjustBcs(0.8f, 1, 0);
			_waitingBrewery = ImageTexture.CreateFromImage(image);
		}
		return _waitingBrewery;
	}

	public static RoomKind ForTool(Tool tool) => tool switch
	{
		Tool.Pile => RoomKind.Pile,
		Tool.GrubPit => RoomKind.GrubPit,
		Tool.FungusFarm => RoomKind.FungusFarm,
		Tool.Brewery => RoomKind.Brewery,
		Tool.Heap => RoomKind.SleepingHeap,
		_ => RoomKind.None,
	};
}
