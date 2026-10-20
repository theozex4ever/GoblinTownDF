# ADR 0003: The sim is deterministic

**Status:** Accepted · 2026-10-19

## Context

Emergent bugs are hard to reproduce. Agents and tests need exact, repeatable outcomes.

## Decision

One seeded RNG for all randomness, fixed ticks, no wall-clock time in the sim, full state serialisable to JSON. A seed plus a command log replays a run exactly.

## Consequences

- Any bug report can be a save file plus a command log.
- Tests assert exact outcomes from a seed.
- Cost: discipline. Every random draw goes through the sim RNG.
