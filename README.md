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

> [!NOTE]
> **Early grey-box prototype.** P1, P2 and P3 have been played and logged. P3 passed its fun check with five yeses out of five, and P4 (Chaos) is next.

The first prototype asks one question: **is it fun to dig a warren and watch goblins mess it up?** It grows through six playable milestones, each tested for fun before any art or extra systems.

| | Milestone | Adds | Status |
|:-:|---|---|---|
| ✅ | **P1 — Map and digging** | Designate tunnels and stairs; one goblin digs them out | [Played](docs/playtests/2026-10-30-P1.md) |
| ✅ | **P2 — Hauling and piles** | Goblins carry dug material to Piles | [Played](docs/playtests/2026-10-31-P2.md) |
| ✅ | **P3 — Needs** | Food, drink, sleep, Fungus farming and a Brewery | [Played, 5/5 yes](docs/playtests/2026-11-06-P3.md) |
| ⬜ | **P4 — Chaos** | Unmet needs lead to shirking, fights and theft | Planned |
| ⬜ | **P5 — One invasion** | Three heroes, one trap type, and Brutes fighting back | Planned |
| ⬜ | **P6 — Legends** | A running event log of deaths, invasions and chaos | Planned |

See the [game design document](docs/design/GDD.md) for the full design and playtest questions.

### What's in the build

A **32×32 map** across **three z-levels**. **Seven Grunts** start together on the surface beside 14 loose Grub and 7 loose Grog.

| | System | What the Grunts do |
|:-:|---|---|
| ⛏️ | **Digging** | Dig the tunnels and stairs you designate |
| 📦 | **Hauling** | Carry what they dig to Piles, and wander when idle |
| 🍖 | **Needs** | Eat, drink and sleep when their Grub, Grog or Sleep meter runs low |
| 🌱 | **Grub Pit** | Tend it to grow Grub |
| 🍄 | **Fungus Farm** | Tend it to grow Fungus, below the surface only |
| 🍺 | **Brewery** | Brew Fungus into Grog |
| 💤 | **Sleeping Heap** | Sleep there, faster than on the bare floor |

<details>
<summary><strong>How Needs work</strong></summary>

<br>

Each Grunt has three meters: **Grub**, **Grog** and **Sleep**.

- **Craving.** When a meter falls below 30, the Grunt drops its work and anything it carries. It goes to eat or drink the nearest unit it can reach, or to sleep.
- **Priority.** The lowest meter goes first. A tie goes Grub, then Grog, then Sleep.
- **Stuck carrying.** A Grunt holding a unit with no Pile to take it loses Grog twice as fast, so it eventually drops the unit and goes for a drink.
- **Sleeping.** A Grunt sleeps until its Sleep meter is full. Hunger and thirst do not wake it.

| Meter at 0 | Effect |
|---|---|
| Grub | **Starving:** moves at half speed |
| Grog | Nothing yet |
| Sleep | **Collapses** and sleeps where it stands |

</details>

<details>
<summary><strong>How Rooms work</strong></summary>

<br>

| Room | Where | Work | Output |
|---|---|---|---|
| 🌱 **Grub Pit** | Any floor | One Grunt tends a tile for 20 ticks | 3 Grub, one every 200 ticks |
| 🍄 **Fungus Farm** | z1 or deeper | One Grunt tends a tile for 20 ticks | 1 Fungus, 300 ticks later. Nobody eats it. |
| 🍺 **Brewery** | Any floor | One brewer per tile works 1 Fungus for 20 ticks | 1 Grog, loose on the tile |
| 💤 **Sleeping Heap** | Any floor | Any number of Grunts share a tile | +1 Sleep per 5 ticks (bare floor: +1 per 10) |

All produce lies loose until the Grunts haul it to a Pile. A sleepy Grunt walks to the nearest Heap it can reach, or lies down on the floor if none is in reach.

**Brewing in detail.** A brewer takes one Fungus, from a Pile or off the floor, to a free Brewery tile. While no Pile has room for Grog, the Brew waits and its tile turns grey: a brewer at work stops where it is, holding its Fungus, and carries on from its progress when room appears. A Craving that interrupts a brewer loses the brew and drops the Fungus.

</details>

## Run locally

Use Godot's **.NET build** and the .NET 8 SDK. The Godot C# project currently uses `Godot.NET.Sdk/4.7.2`; [global.json](global.json) selects .NET SDK 8.0.400 or a later 8.0 feature band.

From the repository root, with `dotnet` and `godot-mono` on your `PATH`:

```bash
godot-mono --headless --path game --build-solutions --quit
godot-mono --path game
```

You can also open `game/project.godot` in the Godot .NET editor and build and run it there.

### Controls

| Key | Action |
|---|---|
| <kbd>Space</kbd> | Pause |
| <kbd>1</kbd> <kbd>2</kbd> <kbd>4</kbd> | Set speed |
| <kbd>,</kbd> <kbd>.</kbd> | Move up / down a z-level |
| <kbd>Esc</kbd> | Clear the selected tool and any active drag |

Pick a tool, then **click a tile or left-drag a rectangle** to designate it or paint a Room on its Floor tiles. **Right-drag removes** what the active tool makes. With no tool picked, right-drag cancels designations.

| Key | Tool | Right-drag removes… |
|---|---|---|
| <kbd>D</kbd> | Dig | Dig designations |
| <kbd>S</kbd> | Dig stairs | Stair designations. Digging stairs through a Room clears it. |
| <kbd>P</kbd> | Pile | Piles. Their stock stays as loose items. |
| <kbd>G</kbd> | Grub Pit | Grub Pits. Grub not yet spawned is lost. |
| <kbd>F</kbd> | Fungus Farm | Farms. Fungus still growing is lost. Painting skips the Surface. |
| <kbd>B</kbd> | Brewery | Breweries. A brewer on one carries its Fungus to a Pile. |
| <kbd>H</kbd> | Sleeping Heap | Heaps. A sleeper on one carries on at the floor rate. |

A tile holds one Room, so painting skips a tile that already holds a different one.

### Reading the map

**Piles.** Each Pile tile holds up to 30 units of each material. Goblins carry to the nearest Pile with room, or wait holding the unit until one appears. Painting skips any tile holding more than 30 of a material. A Pile tile draws two counts in their materials' colours: a full material first (highlighted), then the largest.

**Inspector.** The right-hand panel shows what is under the cursor: over a goblin, its name and Role, its Task and its Grub, Grog and Sleep meters as bars, with the Craving threshold marked; over a Pile tile, every count; over any other tile, what it is, what is marked on it and what lies loose there. Above it, the **Warren summary** totals each material, stored and loose, and counts the goblins working, idle, craving and asleep.

| You see | It means |
|---|---|
| Pink pip | Craving Grub |
| Teal pip | Craving Grog |
| Blue pip | Craving Sleep (with several Cravings, the lowest meter's colour shows) |
| "zzz" | Sleeping |
| Yellow goblin | Starving |
| Blue tile | Sleeping Heap |
| Green tile | Fungus Farm |
| Rust-red tile | Brewery |
| Grey Brewery tile | Its Brews wait for room to put the Grog |

### Saves

Saves are whole-state JSON and are never migrated. A P2 save, which counted two materials, is rejected, and so are saves from before the Grog and Sleep meters. There is no player-facing save or load yet.

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
