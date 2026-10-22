# ADR 0004: Derived state is rebuilt on load, not saved

**Status:** Accepted · 2026-10-21

## Context

Some sim state is a pure function of other state. Whether a Designation is reachable depends only on the tiles, the Designations and the goblin positions. Godot reads it to grey out waiting Designations, and `JobSystem` reads it to skip them when claiming. Saving it as well would give a save two sources of truth, and a hand-edited or corrupt save could disagree with itself.

## Decision

Derived state lives in `SimState` as a flat array, marked `[JsonIgnore]`, so the save format does not change. Building a `Simulation` rebuilds it from the saved state. While a sim runs, the commands and ticks that change its inputs rebuild it eagerly; anything else that changes them calls `Simulation.RecheckReachability()`. A tick that changed none of its inputs does not rebuild it.

## Consequences

- A save plus the command log still replays a run exactly, because the derived array is the same function of the same state on both paths.
- Tests that poke tiles or Designations straight into a live sim must call `RecheckReachability()` afterwards, as a command or tick would.
- Cost: a rebuild is a flood fill over every tile, so it must stay off ticks that did not change its inputs.
