---
name: sim-architecture
description: Hard rules for the simulation core of A Thousand Goblins Deep. Load before writing or reviewing code in sim/, the Godot bridge, saves, pathfinding, or anything touching goblin or tile data.
---

# Sim architecture

The sim is a **headless** C# library. Godot reads snapshots and sends commands; it owns nothing.

## Boundary

- `sim/` references no Godot assembly. A `using Godot;` in `sim/` is a build error to fix, not a style issue.
- Godot talks to the sim through exactly two channels: `Commands` in (dig, zone, raid, speed), `Events` out (goblin died, tile dug, legend written).
- The renderer reads state; it never writes it.

## Data layout

- Data-oriented: goblins and tiles are **structure-of-arrays** — parallel arrays of plain values (`int[] posX`, `byte[] grub`, `byte[] role`), addressed by index.
- Behaviour lives in systems (`NeedsSystem`, `JobSystem`, `ChaosSystem`) that loop over arrays. Data structs carry no methods beyond trivial accessors.
- Tiles: one flat array indexed `x + y*W + z*W*H`, grouped into 16×16 chunks with a dirty flag per chunk for redraws.
- Variable-length per-goblin data (grudges, quirks) lives in side tables keyed by goblin index.
- Hot loops allocate nothing: no LINQ, no `new` per tick.
- Goblin Task fields (`GoblinTask`, `GoblinJobTile`, `GoblinWanderTile`, `GoblinWanderTicks`, `GoblinStepTicks`, `GoblinCarry`, `GoblinWorkTicks`) change only through `GoblinTasks`. Its `FindViolation` holds the rules; a new Task is added there. `ArchitectureTests` fails on a stray write.

## Determinism

- One `SimRandom` seeded at world creation; every random draw goes through it.
- Fixed tick (10/s at normal speed). Time comes from the tick counter, never the wall clock.
- Every test can build a world from a seed and assert exact outcomes.

## Saves

- Whole sim state serialises to JSON. A save plus the command log replays a run exactly; use this to reproduce bugs.

## Scale budget

- Prototype cap: 30 goblins. Write systems so the cap can rise: staggered updates (a slice of goblins per tick) and pathfinding behind an interface so A* can later become hierarchical or flow-field.

## Done when

Every changed file in `sim/` passes: no Godot reference, data in arrays, randomness via `SimRandom`, a deterministic test covering the change.
