# A Thousand Goblins Deep

A top-down, z-level goblin colony sim inspired by Dwarf Fortress: simpler economy, more chaos, Pratchett-style comedy. Solo project, Godot 4 + C#.

**Status:** pre-prototype. Next step is P1 (map and digging). See `docs/design/GDD.md`.

## Folder structure

```
a-thousand-goblins-deep/
├── AGENTS.md              # How agents work on this project (start here)
├── CLAUDE.md              # Points Claude Code at AGENTS.md
├── README.md              # This file: overview, setup, skill stack
├── .mcp.json              # Godot MCP server config
├── .claude/skills/        # Project-specific skills
│   ├── sim-architecture/  # Hard rules for the sim core
│   ├── goblin-voice/      # Tone for all player-facing text
│   └── fun-check/         # Post-playtest verdict
├── docs/
│   ├── design/GDD.md      # Game design doc (source of truth)
│   ├── GLOSSARY.md        # Domain vocabulary
│   ├── adr/               # Architecture decision records
│   ├── tickets/           # Work queue, one file per ticket
│   └── playtests/         # Fun-check logs
├── sim/                   # Pure C# simulation (no Godot)
│   ├── src/
│   └── tests/
├── game/                  # Godot project: rendering, input, UI
└── tools/                 # Scripts, throwaway prototypes
```

## Setup

1. Install Godot 4 (.NET build) and the .NET SDK. Godot 4.7.2 mono and .NET SDKs 8 and 10 are installed per-user (`~/.local/opt/godot/4.7.2-mono`, `~/.dotnet`). The projects target `net8.0`, pinned by `global.json`.
2. Install Matt Pocock's skills, then run `setup-supermatt-skills` once and choose **local Markdown tickets** in `docs/tickets/` and the docs layout above.
3. Set `GODOT_PATH` in `.mcp.json` to your Godot **.NET** binary (currently `~/.local/bin/godot-mono`).
4. Install the recommended skills below.
5. Start Claude Code in this folder. It reads `CLAUDE.md` → `AGENTS.md`.
6. Run `tools/check.sh`. It runs the sim tests, builds the Godot C# project, and boots the game headless, failing on any console error.

Day to day:

```
dotnet test sim/tests                          # sim tests
godot-mono --headless --path game --build-solutions --quit   # build the game's C#
godot-mono --path game                         # run the game
```

## Agent skill stack

### Recommended (install these)

| Skill | Source | What it does here | Notes |
| --- | --- | --- | --- |
| Matt Pocock's skills | [mattpocock/skills](https://github.com/mattpocock/skills) | Planning, tickets, TDD, review, PRs | The backbone of the workflow in `AGENTS.md`. Most useful: `grill-with-docs`, `domain-modeling`, `to-tickets`, `tdd`, `prototype`, `code-review`, `codebase-design`. |
| Godot MCP | [Coding-Solo/godot-mcp](https://github.com/Coding-Solo/godot-mcp) | Agent launches the editor, runs the game, reads debug output | MCP server, not a skill. Biggest single win: the agent sees its own runtime errors. Configured in `.mcp.json`. |
| Godot skill | [Randroids-Dojo/Godot-Claude-Skills](https://github.com/Randroids-Dojo/Godot-Claude-Skills) | Godot conventions, GdUnit4 tests, headless runs, PlayGodot automation | Use for the `game/` side. The sim is tested with plain C# tests instead. |
| game-ai | [gamedev-skills/awesome-gamedev-agent-skills](https://github.com/gamedev-skills/awesome-gamedev-agent-skills) | State machines, behavior trees, steering, A* (engine-neutral) | Install this one skill, not the full 70-skill set. Feeds the goblin brain design pass. |
| sim-architecture | `.claude/skills/` (this repo) | Hard rules: headless sim, data in arrays, seeded RNG, fixed ticks | Written for this project. Edit it when an ADR changes the rules. |
| goblin-voice | `.claude/skills/` (this repo) | Pratchett tone for event lines, legends, names | Written for this project. |
| fun-check | `.claude/skills/` (this repo) | Turns a playtest into a logged go / tune / rethink verdict | Written for this project. Run after every milestone. |

### Worth cherry-picking later

| Skill | Source | When to reach for it | Notes |
| --- | --- | --- | --- |
| Godot performance, pathfinding, save/load, procedural generation | [thedivergentai/gd-agentic-skills](https://github.com/thedivergentai/gd-agentic-skills) ([listing](https://claudemarketplaces.com/skills/thedivergentai/gd-agentic-skills/godot-project-foundations)) | When scaling past 30 goblins, or building saves and world-gen | 51 skills; pick 3–4. Many assume node-based Godot patterns, so check them against `sim-architecture`. |
| Godot economy and inventory | [thedivergentai/gd-agentic-skills](https://claudemarketplaces.com/skills/thedivergentai/gd-agentic-skills/godot-inventory-system) | If piles or crafting grow beyond six resources | Probably overkill for the planned economy. |
| Procedural generation, performance (router set) | [gamedev-skills/awesome-gamedev-agent-skills](https://github.com/gamedev-skills/awesome-gamedev-agent-skills) | World generation, profiling | A router auto-loads skills by engine and task; install only what you need. |

### Reference only (read for patterns, don't install)

| Skill | Source | Why it's relevant | Notes |
| --- | --- | --- | --- |
| unity-ecs-patterns | [wshobson/agents](https://supercode.sh/en/skills/wshobson/agents/unity-ecs-patterns) | Data-oriented design for thousands of entities | Unity-specific. Good ideas for the array-based goblin layout. |
| bitecs | [kadajett/bitecs-agent-skill](https://www.skills.sh/kadajett/bitecs-agent-skill/bitecs) | Structure-of-arrays component layouts, ECS serialisation | TypeScript-specific. |

### Skipped

| Skill | Source | Why skipped |
| --- | --- | --- |
| Claude-Code-Game-Studios | [pixel-cellar (overview)](https://www.sourcepulse.org/projects/31857564) | A simulated multi-agent studio (37 skills, hooks, rules). Too heavy for a solo prototype and overlaps with Pocock's workflow. |
| Generic game-design skills | [sickn33/agentic-awesome-skills](https://claudeskills.info/zh/skills/sickn33/agentic-awesome-skills/game-design/) | Core-loop and GDD basics the design doc already covers. |

Skill quality varies a lot across registries. Before installing any third-party skill, read its `SKILL.md`, and vet any package it pulls in.
