# Terrain textures

Opaque 32×32 PNGs for the existing map, drawn with nearest-neighbour filtering:

| File | Use |
| --- | --- |
| `surface.png` | Dirt Floor and the base under Stairs on z0: muted earth with sparse grass and pebbles. |
| `dirt-floor.png` | Dirt Floor and the base under Stairs below z0: packed earth. |
| `dirt-solid.png` | Unexcavated Solid dirt: dark clay and roots. |
| `stone-floor.png` | Stone Floor and the base under stone Stairs at any level: worn slate. |
| `stone-solid.png` | Unexcavated Solid stone: dark fractured rock. |

Solid textures may transpose by tile coordinates and level, without drawing from the simulation RNG. The renderer adds exposed Solid edges, a subtle grid, ladder rungs over a dark stair opening, and all existing Room, designation and item overlays. Open air is dark and never drawn as Floor.

The grass, grit and roots are visual details; they add no materials or obstacles. [Room fixtures](../rooms/README.md) draw above the terrain and Room tints. Improving the connections between dug Solid edges is explicitly deferred; these textures are a first pass.

These textures were generated with the built-in `image_gen` tool, guided by Theo's control-room reference. Original sources and exact prompts are retained in `tools/prototypes/terrain/`. ImageMagick exported the 32×32 files with point sampling and limited palettes. The first surface was too busy in play; the selected revision uses broad, quiet earth patches so Grunts remain readable.
