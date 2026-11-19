# Resource sprites

Transparent 16×16 PNGs generated with PixelLab Pixen for the rough starting warren. Runtime display uses nearest-neighbour filtering.

| File | Material | Shape |
| --- | --- | --- |
| `dirt.png` | Dirt | Brown earth clod with a root |
| `stone.png` | Stone | Angular slate rock |
| `grub.png` | Grub | Curled pink food grub |
| `fungus.png` | Fungus | Purple mushroom with a cream stem |
| `grog.png` | Grog | Squat teal jug with a cork |

`MaterialSprites` shares these textures between map items and HUD stock rows, and caches grey variants for waiting loose items. Tiny crowded carried slots retain flat material colours. Pile icons accompany the existing two best counts.

Exact prompts, seeds, PixelLab job IDs and gallery asset IDs are recorded in [`pixellab-manifest.json`](../../../tools/prototypes/materials/pixellab-manifest.json). This pass used five successful generations. The PNGs are copied into the project; no PixelLab connection or API key is needed to run the game.
