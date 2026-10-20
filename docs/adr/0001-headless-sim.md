# ADR 0001: The simulation is a headless C# library

**Status:** Accepted · 2026-10-19

## Context

Colony sims are limited by simulation cost, not rendering. GDScript is slow for tight loops, and coupling sim logic to Godot nodes makes testing and engine changes hard.

## Decision

All game rules live in `sim/`, a C# library with no Godot references. Godot sends Commands in and reads Events and state snapshots out.

## Consequences

- The sim runs and is tested headless, fast and deterministically.
- The project can move engines without rewriting rules.
- Cost: a bridge layer in `game/` to map sim state to tiles and sprites.
