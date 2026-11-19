# Runtime art

The first visual pass uses chunky pixel art, a square overhead map and front-facing Grunts with visible faces. Starting society is rough: ragged clothes, bare feet, timber, rope, clay and straw. The supplied control-room reference depicts the later, more developed society; knowledge blessings remain visual direction rather than a gameplay system.

| Folder | Source size | Runtime use | Provenance |
| --- | --- | --- | --- |
| `goblins/` | 32×32 RGBA | One static Grunt idle frame; starvation tint and existing status overlays | Built-in image generation; selected [reference-led study](../../tools/prototypes/sprites/reference-study/README.md) |
| [terrain/](terrain/README.md) | 32×32 opaque | Surface, dirt and stone Floor/Solid textures; stairs and Open air drawn separately | Built-in image generation; [prompts and sources](../../tools/prototypes/terrain/PROMPTS.md) |
| [materials/](materials/README.md) | 16×16 RGBA | Loose and carried resources, Pile rows, Warren summary and Inspector | PixelLab Pixen; [generation manifest](../../tools/prototypes/materials/pixellab-manifest.json) |
| [rooms/](rooms/README.md) | 32×32 RGBA | One low, roofless fixture per painted Room tile and matching Tool icon | PixelLab Pixen generation/edits; [manifest](../../tools/prototypes/rooms/pixellab-manifest.json) |

## Display and cues

The logical tile is still 18 pixels. Nearest-neighbour filtering fits artwork to it, and 1×–4× zoom enlarges tiles up to 72 screen pixels. Native 32-pixel map tiles and animation remain future work. Whole-map overview, pointer-anchored zoom, bounded middle-drag pan, and Home/Overview reset change only presentation; the HUD and simulation remain separate.

`WarrenView` draws terrain, Room tints and fixtures, resource items or Pile counts, then Grunts and their carried resources/status marks. Designation, hover and chat-bubble overlays retain their existing purposes. Piles retain full-first ordering, two counts and capacity highlights, with dark count backing over the rack artwork.

Grub Pit and Fungus Farm fixtures are empty growing beds. Only real spawned Grub or Fungus appears as yield; hauling and consumption remove those resource sprites. There is no decorative crop or fake progress indicator. Waiting loose items retain their shape in grey; a waiting Brewery greys its vat as well as its tint. Extremely crowded one- and two-pixel carried slots use material colours because a sprite cannot be read at that size.

## Assets and validation

Runtime needs only these local PNGs. PixelLab is an authoring tool; no account, network connection or API key is needed to play. Original sources, exact prompts, rejected trials and superseded designs live under `tools/prototypes/`, outside the simulation.

After changing PNGs, import them in the Godot .NET editor, or run:

```bash
godot-mono --headless --path game --editor --import --quit
```

Run `bash tools/check.sh` from the repository root. With a display, `CarryDrawChecks` verifies sprite pixels, transparency, terrain/material selection, Room removal, resource/goblin layering, waiting cues and count ordering. Its 21 pixel scenarios are explicitly skipped under `--headless`; geometry alone does not verify appearance. `SelectionInputChecks` covers camera/input routing and `InspectorLayoutChecks` covers both supported test viewport layouts. Set `GOBLINTOWN_CAPTURE_DIR` to retain rendered captures. See [visual evidence](../../docs/evidence/pixel-art-pass/README.md).

Known follow-ups: improve dug Solid edge connections, refine sprite consistency and overview readability, and develop later-society buildings. These fixtures add no construction costs, blocking tiles, required furniture or production rules.
