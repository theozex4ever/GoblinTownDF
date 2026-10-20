# AGENTS.md — A Thousand Goblins Deep

A top-down, z-level goblin colony sim inspired by Dwarf Fortress, built solo in Godot. The current goal is a grey-box prototype (P1–P6) that answers one question: **is it fun?** Every task serves that question.

## Ground truth

- Design: `docs/design/GDD.md`. Read the section your task touches before writing code.
- Vocabulary: `docs/GLOSSARY.md`. Use its terms in code, tests, commits, and tickets. Add a term there before using a new one.
- Decisions: `docs/adr/`. Read the ADRs a change touches; write a new ADR when a change reverses or adds an architectural decision.
- Work queue: GitHub issues, one per ticket. See `docs/agents/issue-tracker.md`.
- Playtest results: `docs/playtests/`.

## Architecture rules

The sim is a pure C# library; Godot is a renderer and input device. Before touching `sim/` or the Godot bridge, load the `sim-architecture` skill. It holds the full rules; the short version:

- `sim/` has zero Godot references and runs headless.
- Goblins and tiles live in flat arrays of plain data, addressed by index.
- One seeded RNG, fixed ticks, JSON-serialisable state: a seed plus a command log replays any run.

## Workflow

Each prototype milestone runs as one **tracer bullet**: thin, end to end, playable.

1. **Shape.** Unclear design → `grill-with-docs` until the decision is made and recorded in the GDD, glossary, or an ADR.
2. **Ticket.** Break the milestone with `to-tickets` into GitHub issues. Each ticket names its GDD section and its done-condition.
3. **Build.** `tdd` for everything in `sim/`: the test goes red, then green. `prototype` for throwaway feel experiments, kept in `tools/prototypes/` and never imported by `sim/`.
4. **Run.** Launch the game through the Godot MCP server and read the console. Done means it runs with zero errors, not that it compiles.
5. **Review.** `code-review` against the GDD and `sim-architecture` before each merge.
6. **Playtest.** After each milestone, load `fun-check` and log the result in `docs/playtests/`. A failed fun check pauses new features until the cause is understood.

## Text and tone

All player-facing text (event lines, legends, names, tooltips) goes through the `goblin-voice` skill.

## Scope guard

The GDD's **Not in scope** list is closed until P6 passes the fun check. A task that needs something on that list stops and asks Theo.

## Commits

Small commits, one ticket each, message in the form `P2: goblins haul dug stone to nearest pile`.

## Agent skills

### Issue tracker

GitHub issues in `theozex4ever/GoblinTownDF`, via the `gh` CLI. Tickets are not files in the repo. See `docs/agents/issue-tracker.md`.

### Triage labels

Default five-role vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`) as GitHub labels. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context, with the glossary at `docs/GLOSSARY.md` and ADRs in `docs/adr/`. See `docs/agents/domain.md`.
