# Glossary

One meaning per term. Use these words in code, tests, tickets, and commits. Add a term here before using a new one.

| Term | Meaning |
| --- | --- |
| Warren | The player's whole colony: tunnels, rooms, goblins. |
| Goblin | One simulated colonist, identified by its index in the goblin arrays. |
| Role | A goblin's job class: Grunt, Brute, Sneak, Tinkerer, Shaman, Boss. |
| Need | A 0–100 meter that drains over time. Prototype needs: Grub, Grog, Sleep. |
| Chaos | Per-goblin meter raised by unmet needs and grudges; high chaos triggers acting out. |
| Acting out | A chaos behaviour: shirk, fight, steal, set fire. |
| Authority | The Boss's hold on the warren; low authority triggers a challenge. |
| Grudge | A goblin's remembered grievance against another goblin or faction. |
| Quirk | A fixed personality trait that bends a goblin's choices. |
| Band | A depth range of z-levels with shared materials and hazards: Surface, Dirt, Stone, Ore, The Deep. |
| Z-level | One horizontal layer of the map. 0 is the surface; higher numbers are deeper. |
| Tile | One cell of the map at an (x, y, z-level) position. Its kind is Solid, Floor, Open air, or Stairs. |
| Solid | An undug tile made of a material; nothing walks through it. Avoid: wall (kept for walls goblins build). |
| Floor | A tile goblins can walk on. |
| Open air | An empty tile with nothing to stand on. |
| Stairs | A walkable tile that links to a Stairs tile directly above or below it. |
| Material | What a tile is made of, such as dirt or stone. Digging a tile yields its material. |
| Designation | A player order on tiles: Dig or Dig stairs. |
| Dig | A designation that turns a Solid tile into Floor. |
| Dig stairs | A designation that turns a Solid or Floor tile into Stairs. |
| Reachable | A Designation is reachable when some goblin can path to a tile it can dig from. An unreachable Designation waits: it stays designated, is skipped when goblins claim a Job, and shows greyed out until a dug tile opens a path. |
| Selection | A click-drag on the viewed level that picks a rectangle of tiles; a single click is a 1×1 rectangle. It does not know what the active tool will do with it. |
| Tool | The action a selection applies, picked by a key: Dig, Dig stairs, or Pile. Right-drag removes what the active Tool makes. |
| Cancel designation | The `CancelDesignation` Command: removes Designations from a rectangle of tiles. A tile mid-dig loses its progress and stays as it was. |
| Job | A unit of work in the global job list (dig, haul, brew, build) that an idle goblin claims. |
| Task | What one goblin is doing right now. A dig Job is worked as Walking to the claimed tile, then Digging it. A Haul is worked as Fetching (walking to the loose unit), PickingUp (5 ticks), Carrying (walking to the nearest Pile tile, one unit at a time, at the usual walking speed), and Dropping (5 ticks, which stores the unit). A goblin with no Job is Idle, or Wandering. The Job is the work itself, and stays on the job list until it is done or cancelled; a carrying goblin is never given another Job until it has dropped its unit. |
| Wander | What an idle goblin does when it has no reachable Job: after a 3–6 second pause it walks to a random walkable tile within 4 tiles on its own z-level, then pauses again. A goblin mid-walk is Wandering. A reachable Designation cuts it short. |
| Pile | A flag painted on Floor tiles with the Pile Tool. Units on a Pile tile are stored and counted per material, with no cap; goblins drop each Haul on the nearest reachable Pile tile. |
| Loose item | A unit of material on a tile that is not a Pile, such as the one a dug tile drops. Each loose unit is a Haul Job, so a tile holding several (a removed Pile's stock, or dirt and stone together) is hauled one unit per trip and claimed by at most as many goblins as it has units. |
| Haul | The Job of carrying one loose unit to the nearest reachable Pile tile. With no reachable Pile, a Haul waits and shows greyed out. A goblin whose Pile vanishes mid-carry heads for the next nearest one, or puts the unit down where it stands, loose, with its own Haul. |
| Room | A painted zone that works once it holds its required item: Sleeping Heap, Grub Pit, Brewery, Pile, Shrine, Boss Throne. The Pile needs no item. |
| Raid | An abstracted off-map trip that returns loot and a story after a timer. |
| Anger | Surface factions' hostility; raised by raids, drives invasion size. |
| Invasion | Enemies entering the warren from the map edge or surface. |
| Legend | A chronicle entry written from a notable event. |
| Tick | One fixed sim step; 10 per second at normal speed. |
| Speed | How many ticks run per second of real time: 1×, 2× or 4×. Never changes the tick length. |
| Pause | A flag, separate from Speed, that stops ticks. Commands still apply, and Speed is kept for unpausing. |
| Command | An input from Godot to the sim. |
| Event | An output from the sim to Godot. |
| Tracer bullet | A thin, end-to-end, playable slice of a milestone. |
| Fun check | The five-question playtest verdict, logged in `docs/playtests/`. |
