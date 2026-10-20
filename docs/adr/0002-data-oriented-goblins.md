# ADR 0002: Goblins and tiles are stored as arrays of plain data

**Status:** Accepted · 2026-10-19

## Context

The game may eventually simulate hundreds or thousands of goblins. One object or Godot node per goblin does not scale for update loops, pathfinding, or rendering.

## Decision

Goblins and tiles use a structure-of-arrays layout, addressed by index. Systems loop over arrays. Rendering batches goblins (MultiMesh or RenderingServer) rather than one node each.

## Consequences

- Cache-friendly loops, cheap serialisation, a path to staggered updates and multithreading.
- Cost: less convenient than objects; per-goblin variable data needs side tables.
