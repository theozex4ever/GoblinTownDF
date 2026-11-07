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
> **Early grey-box prototype.** P1, P2 and P3 have been played and logged. P3 passed its fun check with five yeses out of five. P4 (Chaos) is under way: unmet Needs now raise Chaos and a goblin can shirk, pick a fight or steal supplies.

The first prototype asks one question: **is it fun to dig a warren and watch goblins mess it up?** It grows through six playable milestones, each tested for fun before any art or extra systems.

| | Milestone | Adds | Status |
|:-:|---|---|---|
| ✅ | **P1 — Map and digging** | Designate tunnels and stairs; one goblin digs them out | [Played](docs/playtests/2026-10-30-P1.md) |
| ✅ | **P2 — Hauling and piles** | Goblins carry dug material to Piles | [Played](docs/playtests/2026-10-31-P2.md) |
| ✅ | **P3 — Needs** | Food, drink, sleep, Fungus farming and a Brewery | [Played, 5/5 yes](docs/playtests/2026-11-06-P3.md) |
| 🚧 | **P4 — Chaos** | Unmet needs lead to shirking, fights and theft | In progress: Chaos, shirking, fights and theft built; human fun check pending |
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
| 😤 | **Chaos** | Build up from neglect, then shirk |

<details>
<summary><strong>How Needs work</strong></summary>

<br>

Each Grunt has three meters: **Grub**, **Grog** and **Sleep**.

- **Craving.** When a meter falls below 30, the Grunt drops its work and anything it carries. It goes to eat or drink the nearest unit it can reach, or to sleep.
- **Priority.** The lowest meter goes first. A tie goes Grub, then Grog, then Sleep.
- **Stuck carrying.** A Grunt holding a unit with no Pile to take it waits, still carrying. Its Grog drains at the ordinary rate, but waiting raises its Chaos (below), and a Grog Craving still makes it drop the unit and go for a drink.
- **Sleeping.** A Grunt sleeps until its Sleep meter is full. Hunger and thirst do not wake it.

| Meter at 0 | Effect |
|---|---|
| Grub | **Starving:** moves at half speed |
| Grog | No slowdown, but Chaos keeps rising |
| Sleep | **Collapses** and sleeps where it stands |

</details>

<details>
<summary><strong>How Chaos works</strong></summary>

<br>

Each Grunt has a **Chaos** meter from 0 to 100, starting at 0. Neglect raises it, contentment lowers it, and a high enough meter makes a Grunt walk off the job.

| What happens | Chaos |
|---|---|
| Every 10 ticks, for each Need below 30, even when supply is within reach | +1 |
| Every 20 ticks a Grunt stands waiting for a Pile to take its unit | +1 more |
| Another Grunt eats or drinks the unit a hauler was walking to fetch | +15, once |
| Every 10 ticks with every Need at 30 or more, and no unit left waiting | −1 |

The Need a Grunt is eating, drinking or sleeping to restore adds nothing while it does, but any other low Need still does, even in its sleep. A meal does not clear Chaos; it only stops it growing. Painting a Pile over a claimed unit, or another hauler collecting it, annoys nobody.

- **Shirking.** From 70 Chaos, a Grunt with no cooldown abandons its Job and loafs where it stands for 100 ticks, taking no work and not wandering. A Craving it can satisfy, Collapse and sleep all come first. A dig keeps its progress, a Tend or a Brew loses it, and a unit it carries is put down by the same rules as for a Craving. With nowhere legal to put the unit it holds on to it and tries again.
- **Finishing.** A full 100 ticks of shirking removes 40 Chaos. A Craving it can meet, or Collapse, ends the shirk early for no relief, and it does not resume. Either way the Grunt then waits 300 ticks before it can act out again, though its Chaos can keep rising. Starving does not lengthen any of this.
- **Seeing it.** A shirking Grunt wears a lilac ring and a lilac band across its body, and the Inspector shows its Chaos beside its Needs.
- **Fighting.** At 70 Chaos with no cooldown, a Grunt chooses equally among the behaviours available: shirking, fighting when an eligible Grunt is reachable, and theft when supplies and a private destination are available. With neither a fight nor a theft available, it simply shirks. A fight picks the nearest reachable Grunt, ties to the lower number, and skips anyone asleep, meeting a Need, acting out, in a cooldown, or holding a unit with nowhere legal to put it. The instigator stomps after its target for up to 100 ticks while the target carries on working; on contact, the same tile or a side-on neighbour, the target is rechecked, and if it is still fair game it abandons its work as a shirker would. The two scuffle for 30 ticks: the victim gains 15 Chaos, once, and nobody is hurt. A scuffle that runs its course takes 40 Chaos off the instigator alone, then both sit out 100 ticks of recovery and start their 300-tick cooldown together. A Craving it can meet, or Collapse, in either Grunt ends the scuffle for both with no relief and no recovery, though the victim keeps its 15; a Need can also end recovery early without restarting the cooldown. A pursuit that times out, loses its path or finds its target gone ends with the instigator's cooldown and nobody else's. Starving slows only the pursuer's walk.
- **Seeing a fight.** An orange ring marks a Grunt chasing, scuffling or being scuffled, with an orange cross over the two scuffling; a duller ring marks the recovery. The Inspector names the other Grunt in each one's Task, and the Need pip and any carried square still show.

**Try it: neglect, then recover.** Start the game, give no orders and press <kbd>4</kbd>. The starting Grub and Grog are eaten once; when the Needs run low again with nothing left, Chaos climbs on the Inspector's bars, and around tick 3000 the first Grunt loafs under a lilac ring and band. Then fix it as a player would: paint a Pile, a Grub Pit and a Brewery, dig stairs down and paint a Fungus Farm below, paint a Heap. Once the Grunts eat, drink and sleep again, Chaos drains away and the shirking stops. For a storage blockage instead, paint a one-tile Pile, dig a large area so it fills, and watch the Grunts left holding dirt; painting more Pile ends their wait.

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

**Inspector.** The right-hand panel shows what is under the cursor: over goblins, one block for each goblin on the tile in order, with its name and Role, its Task, its Grub, Grog and Sleep meters as bars with the Craving threshold marked, and its Chaos bar with the acting-out threshold marked (a crowd taller than the panel scrolls inside it, by the mouse wheel over the tile, or over the panel once the pointer has left the map for it, or by its scrollbar; the map never moves); over a Pile tile, every count; over any other tile, what it is, what is marked on it and what lies loose there. Above it, the **Warren summary** totals each material, stored and loose, and counts the goblins working, idle, craving and asleep.

| You see | It means |
|---|---|
| Pink pip | Craving Grub |
| Teal pip | Craving Grog |
| Blue pip | Craving Sleep (with several Cravings, the lowest meter's colour shows) |
| "zzz" | Sleeping |
| Yellow goblin | Starving |
| Lilac ring and band | Shirking |
| Orange ring (crossed while scuffling) | Pursuing, scuffling or being scuffled |
| Dull orange ring | Recovering from a scuffle |
| Blue tile | Sleeping Heap |
| Green tile | Fungus Farm |
| Rust-red tile | Brewery |
| Grey Brewery tile | Its Brews wait for room to put the Grog |

### Saves

Saves are whole-state JSON and are never migrated. A P2 save, which counted two materials, is rejected, and so are saves from before the Grog and Sleep meters, from before Chaos, from before fights or from before theft. There is no player-facing save or load yet.

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
