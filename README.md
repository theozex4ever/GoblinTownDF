# A Thousand Goblins Deep

A top-down, z-level goblin colony sim inspired by Dwarf Fortress. Dig a warren, steal what you need, and survive the heroes who come to clean you out.

The economy is small. The goblins have needs, grudges, and opinions. The aim is a colony sim where stories come from the inhabitants: a hungry Grunt picking a fight, a Sneak stealing the Boss's shinies, or a forgotten trap saving the warren.

Built solo in Godot 4 and C#, with affectionate, Pratchett-inspired comedy.

## The game

The planned core loop is **dig → haul loot to piles → feed and grog the goblins → defend the warren → record the legends → dig deeper**.

- **Build downward.** Carve tunnels and rooms across z-levels, from surface dirt to stone, ore, and the hazards of the Deep. Your layout becomes your defence.
- **Keep the horde fed.** Manage six resources with short production chains. Raiding and scavenging supply the warren, but stolen loot brings angrier neighbours and stronger invasions.
- **Manage goblin chaos.** Grub, Grog, and Sleep keep goblins working. Unmet needs and grudges lead to shirking, fights, theft, and fires. The Boss holds things together for as long as their authority lasts.
- **Be the dungeon.** Heroes arrive looking for shinies and the Boss. Meet them with traps, chokepoints, and Brutes.
- **Leave a legend.** Deaths, raids, invasions, and disasters become a chronicle of the warren, complete with grand claims and undermining footnotes.

Goblins are earnest underdogs who take absurd traditions seriously. Failure should have consequences—and leave you with a story worth sharing.

## Development status

**Early grey-box prototype. P1 (map and digging) is done and passed its [playtest](docs/playtests/2026-10-30-P1.md); next up is P2: hauling and piles.** The current build has a 32×32 map across three z-levels with one Grunt who digs designated tunnels and stairs and wanders when idle. You can switch levels, pause, and change speed. The wider colony systems are still to come.

The first prototype asks: **is it fun to dig a warren and watch goblins mess it up?** It will grow through six playable milestones:

1. **P1 — Map and digging:** designate tunnels and stairs; one goblin digs them out.
2. **P2 — Hauling and piles:** five goblins carry dug material to piles.
3. **P3 — Needs:** food, drink, sleep, fungus farming, and a Brewery.
4. **P4 — Chaos:** unmet needs lead to shirking, fights, and theft.
5. **P5 — One invasion:** three heroes, one trap type, and Brutes fighting back.
6. **P6 — Legends:** a running event log for deaths, invasions, and chaos.

The focus is on testing the fun before adding art or expanding the systems. See the [game design document](docs/design/GDD.md) for the full design and playtest questions.

## Run locally

Use Godot's **.NET build** and the .NET 8 SDK. The Godot C# project currently uses `Godot.NET.Sdk/4.7.2`; [global.json](global.json) selects .NET SDK 8.0.400 or a later 8.0 feature band.

From the repository root, with `dotnet` and `godot-mono` on your `PATH`:

```bash
godot-mono --headless --path game --build-solutions --quit
godot-mono --path game
```

You can also open `game/project.godot` in the Godot .NET editor and build and run it there.

Current controls: **Space** pauses; **1**, **2**, and **4** select speed; **,** and **.** move up and down a z-level.

Press **D** for Dig or **S** for Dig stairs, then click a tile or left-drag a rectangle to designate it. Right-drag cancels designations. **Escape** clears the selected tool and any active drag.

## Development

The simulation is a pure C# library that runs without Godot. Godot renders the world and sends player commands to it. Fixed ticks and a seeded random number generator keep runs reproducible.

- [`sim/src/`](sim/src/) — simulation state and logic.
- [`sim/tests/`](sim/tests/) — headless simulation tests using xUnit.
- [`game/`](game/) — Godot rendering, input, and UI.
- [`docs/design/GDD.md`](docs/design/GDD.md) — game design and prototype milestones.
- [`docs/GLOSSARY.md`](docs/GLOSSARY.md) — game and simulation vocabulary.
- [`docs/adr/`](docs/adr/) — architecture decisions.
- [`docs/playtests/`](docs/playtests/) — playtest records.

Run the simulation tests with:

```bash
dotnet test sim/tests
```

For the full local check—simulation tests, Godot C# build, a headless boot that checks for console errors, and selection input checks—run:

```bash
bash tools/check.sh
```

If your Godot executable has another name or location, set `GODOT_PATH` when running the check script.
