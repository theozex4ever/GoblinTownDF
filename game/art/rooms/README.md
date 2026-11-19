# Starting Room fixtures

Five static, transparent 32×32 PNGs generated with PixelLab Pixen. Early society uses rough timber, rope, soil and straw, viewed from above with visible front edges. Open fixtures leave goblins visible and suit dug rooms as well as the Surface.

| File | Room | Fixture |
| --- | --- | --- |
| `pile.png` | Pile | Crude timber storage rack |
| `grub-pit.png` | Grub Pit | Empty clay-rimmed compost bed |
| `fungus-farm.png` | Fungus Farm | Empty log-edged soil bed |
| `brewery.png` | Brewery | Wooden brewing vat with an open top |
| `sleeping-heap.png` | Sleeping Heap | Straw mat, sack pillow and faded blanket |

`RoomSprites` caches the shared textures and a grey waiting Brewery variant. `WarrenView` draws one fixture per painted tile over the Room tint, below goblins, loose resources and storage counts. The Room palette uses matching icons. The map uses nearest-neighbour filtering in the existing 18-pixel logical tiles; camera zoom enlarges the result. Fixtures are cosmetic: walking, production, construction requirements and Room painting remain unchanged. Grub Pit and Fungus Farm fixtures contain no crops. Only actual spawned Grub or Fungus resources appear on the growing beds; hauling or consumption removes those yield sprites.

Exact prompts, seeds, job IDs and gallery asset IDs are recorded in [`pixellab-manifest.json`](../../../tools/prototypes/rooms/pixellab-manifest.json). Two initial designs were refined; rejected originals are retained in `tools/prototypes/rooms/rejected/`. Seven initial generation calls and two PixelLab edits produced the five current assets. Earlier crop-filled beds are kept in `tools/prototypes/rooms/superseded/`. PNGs are local runtime assets; playing needs no PixelLab account or API connection.
