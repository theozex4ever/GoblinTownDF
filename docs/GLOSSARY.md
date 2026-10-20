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
| Material | What a Solid tile is made of, such as dirt or stone. |
| Designation | A player order on tiles: Dig or Dig stairs. |
| Dig | A designation that turns a Solid tile into Floor. |
| Dig stairs | A designation that turns a Solid or Floor tile into Stairs. |
| Job | A unit of work in the global job list (dig, haul, brew, build) that an idle goblin claims. |
| Pile | A floor zone where goblins dump items; counted per resource, not per item. |
| Room | A painted zone that works once it holds its required item: Sleeping Heap, Grub Pit, Brewery, Pile, Shrine, Boss Throne. |
| Raid | An abstracted off-map trip that returns loot and a story after a timer. |
| Anger | Surface factions' hostility; raised by raids, drives invasion size. |
| Invasion | Enemies entering the warren from the map edge or surface. |
| Legend | A chronicle entry written from a notable event. |
| Tick | One fixed sim step; 10 per second at normal speed. |
| Command | An input from Godot to the sim. |
| Event | An output from the sim to Godot. |
| Tracer bullet | A thin, end-to-end, playable slice of a milestone. |
| Fun check | The five-question playtest verdict, logged in `docs/playtests/`. |
