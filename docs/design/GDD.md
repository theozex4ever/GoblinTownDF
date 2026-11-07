# A Thousand Goblins Deep — Game Design Doc

Oct 19, 2026 · @Theo

## Pitch and pillars

A top-down, z-level colony sim where you run a goblin warren: dig down, steal what you need, and survive the heroes who come to clean you out. It keeps Dwarf Fortress's emergent stories but cuts the economy to six resources.

**Working title:** A Thousand Goblins Deep.

**Core loop:** dig → haul loot to piles → feed and grog the goblins → defend against invaders → the game writes down what happened → dig deeper.

**Pillars**

1. **Simple economy, deep goblins.** Cut resource depth, keep simulation depth on personalities, grudges, and injuries. Stories come from goblins, not supply chains.
2. **Horde, not heroes.** Goblins breed fast and die often. Standouts emerge from what they survive, not from the player picking favourites.
3. **Steal, don't produce.** Raiding and scavenging are the main inputs. Crafting is never more than two steps.
4. **Chaos is the stress system.** Bored, hungry goblins cause trouble. The Boss keeps order through fear, food, and loot splits.
5. **Losing is funny.** Failure is entertaining and has real consequences, and it always ends up in the legends.

**Not in scope:** item quality tiers, detailed body-part combat, nobles and mandates, world-gen history, trade caravans, magic systems.

## Tone: Pratchett comedy

Goblins are earnest, not stupid. The comedy comes from them taking absurd things very seriously, the way Discworld treats its characters with affection.

- **Tradition for everything.** A nine-step ritual to bless a cooking pot. A formal process for filing a grudge, with a grudge-keeper who takes it seriously.
- **Names do work.** Snaggle Wartnose, Third-Best Ugg, Mother Grubb, Kevin.
- **Deadpan event text.** "Nubbin has invented the wheel. It is square. Nubbin is very proud."
- **History with footnotes.** Legends read like a pompous historian wrote them, with asides that undercut the claims.
- **Never mean-spirited.** Goblins are the underdogs. Humans, elves, and dwarves are the snobs, and the joke is usually on them.

Tone rule of thumb: every failure should produce a line of text you would want to screenshot.

## World and z-levels

One embark map, about 96×96 tiles and 16 z-levels, viewed top-down one layer at a time. The level below shows faded through open floor, so depth stays readable.

| Band | Levels | Main materials | Hazards | Role in play |
| --- | --- | --- | --- | --- |
| Surface | 0 | Wood, scrap, raid targets | Heroes, daylight, weather | Where enemies arrive and loot comes from |
| Dirt | 1–3 | Dirt, roots, worms | Cave-ins | Fast, cheap first tunnels |
| Stone | 4–9 | Stone, fungus caverns | Flooding from aquifers | The main warren |
| Ore | 10–13 | Scrap ore, shinies | Gas pockets | Rich but risky expansion |
| The Deep | 14–15 | Rare shinies | Things That Should Not Be Woken | Late-game temptation |

**Key tiles:** solid (undug, made of a material), floor, open air, stairs, ramp, water, door, trap. "Wall" means only a wall goblins build.

**Digging** is the main verb. The player designates an area and Grunts dig it out tile by tile. Every dug tile drops its material as a hauling job.

**Rooms** are zones painted on dug floor: Sleeping Heap, Grub Pit, Fungus Farm, Brewery, Pile, Shrine, Boss Throne. A room works once it has its one required item, a rule waived until crafting and building exist.

## Economy: six resources

Six resources, no quality tiers, and no chain longer than two steps. Items are counted per pile, not tracked one by one.

| Resource | Where it comes from | What it's for | If you run out |
| --- | --- | --- | --- |
| Grub | Grub Pits, rat traps, raids | Food | Hunger, then fights over scraps |
| Grog | Brewed from Fungus Farm fungus (1 step) | Morale | Goblins get bored and cause chaos |
| Scrap | Ore, raids, dead heroes | Weapons, tools, traps (1 step) | Unarmed brutes, no new traps |
| Stone and wood | Digging, surface trees | Building furniture and walls | Can't build rooms |
| Shinies | Ore band, raids | Loyalty, Boss status, bribes | Boss authority drops |
| Trophies | Dead enemies | Shrines, rituals, bragging | Fewer morale boosts |

**Piles, not stockpiles.** Goblins dump things on the nearest pile. Messy piles attract rats, which eat grub and spread disease. Tidiness is a cost you choose to pay, not a sorting minigame.

**Crafting** happens at one workbench: scrap goes in, a weapon, tool, or trap comes out. The Brewery turns fungus into grog. That is the whole production system for the prototype.

## Goblins: roles and chaos

A warren starts with 7 goblins and grows to roughly 30–80. Each goblin has a role, three needs, two quirks, and a grudge list. The full decision-making brain gets its own design pass later.

| Role | Does | Comedy hook |
| --- | --- | --- |
| Grunt | Digs and hauls | Takes enormous pride in a perfectly square hole |
| Brute | Fights, guards | Solves every problem by hitting it |
| Sneak | Scouts, steals, raids | Steals from the warren when bored |
| Tinkerer | Crafts weapons and traps | Traps occasionally catch their maker |
| Shaman | Rituals, healing, buffs | Rituals work, sometimes not as intended |
| Boss | Gives orders, holds the warren together | Has a throne and a lot of opinions |

**Needs (prototype):** Grub, Grog, Sleep. Each is a 0–100 meter that drains over time.

**Chaos meter.** Unmet needs and grudges raise a goblin's chaos. At high chaos they act out: shirk work, pick fights, steal shinies, or set something on fire.

**The Boss.** Boss authority rises with feasts, shiny handouts, and won fights. It falls with hunger and lost raids. If authority drops low, the strongest Brute challenges the Boss, and the winner takes over with their own quirks.

**Breeding and death.** New goblins appear every few in-game days when grub is plentiful. Deaths are common and every one gets a line in the legends.

## Threats and invasions

The warren is the dungeon. Hero parties come down from the surface to clear you out, so your tunnel layout and traps are the defence game.

| Threat | Arrives | Behaviour | Counter |
| --- | --- | --- | --- |
| Adventurer party (3–5) | Surface entrance, more often as the warren grows richer | Heads for shinies and the Boss | Traps, chokepoints, Brutes |
| Angry villagers | After a raid | Large, weak mob | Doors, numbers |
| Dwarf expedition | Mid-game, through the stone band | Tough, digs its own tunnels | Flooding, collapses |
| Deep thing | When you dig into the Deep | One huge monster | Seal it off, or feed it |
| Internal chaos | Any time | Fights, fires, thefts | Grub, grog, a strong Boss |

**Raids (abstracted).** The player sends a raid party off-map to a target. After a timer, the survivors return with loot and a short story. Raids are not played as maps in this version.

**Raid risk.** Every raid raises surface anger, which brings bigger invasions. That is the core tension: you need loot, and loot brings heroes.

## Legends

The game keeps a running chronicle of notable events, written in a pompous historian's voice with footnotes. It is the main payoff for playing.

**What gets recorded:** deaths, Boss changes, invasions survived or lost, raids, first discoveries (a new band, the Deep), and big chaos events like fires.

**How it's built:** each event fills a text template with names, places, and a cause. Two to four template variants per event type keep it from repeating too quickly.

**Example entry:**

> **The Siege of the Grub Pit.** In the autumn of Year 2, four heroes of Lesser Pumpton came for the shinies. Third-Best Ugg held the stairs alone.¹ The heroes were eventually defeated by a trap Nubbin had forgotten about.
>
> ¹ Ugg was asleep on the stairs. It still counts.

**Persistence.** Goblins remember grudges and great deeds. A goblin with a legend entry gets a title, such as "Ugg the Stairholder", which other goblins react to.

## Prototype slice and fun tests

The first prototype answers one question: is it fun to dig a warren and watch goblins mess it up? Grey boxes only, no art. Build it in this order and play after each step.

**Build order**

- [x] **P1 — Map and digging.** 32×32×3 grid, view one level at a time, designate digging, one goblin digs it out.

  **P1 decisions**

  - **Map:** z0 is walkable Floor (Surface) made of dirt, so digging it yields dirt. z1 and z2 are Solid dirt. The map is flat and identical for every seed; the seed still goes through `SimRandom`. One Grunt starts on z0. There is no starting chamber and no fixed entrance.
  - **Designations:** Dig (Solid → Floor) and Dig stairs (Solid or Floor → Stairs). Two Stairs tiles directly above each other link their levels. Going down two levels means designating stairs on each level. Stairs on z2 lead nowhere further down.
  - **Digging:** the goblin digs from a walkable orthogonal neighbour, or from Stairs directly above or below when digging stairs. Each tile takes a fixed 20 ticks, set per material. Walking takes 3 ticks per tile, orthogonal steps only.
  - **Dug material:** a tile-dug Event carries the material, and nothing else happens. P2 turns that Event into hauling.
  - **Jobs:** the goblin takes the nearest reachable designation by path length, ties broken by lowest tile index. Unreachable designations stay designated, show greyed out, and are checked again whenever a tile is dug. When idle, the goblin wanders: after a pause of 3–6 seconds it walks to a random walkable tile within 4 tiles on its own level, then pauses again. Both the pause and the tile come from `SimRandom`, and a newly reachable designation interrupts the walk.
  - **Controls:** pause, plus 1×, 2× and 4× speed. Keys move the view up and down a z-level, and the whole map fits on screen. Two keys pick the Dig or Dig stairs tool. Click-drag designates a rectangle on the viewed level, and a single click designates one tile. Right-drag cancels; cancelling a tile mid-dig throws away the progress.
  - **View:** current level only, with a depth indicator. Goblins one level above or below show as faint markers. Stairs are marked on both levels they link. Designated tiles are outlined, and tiles being dug show a progress fill.
  - **Text:** no goblin names or status lines. The only text is a key-hint line, written with `goblin-voice`.
  - **Saves:** no save or load in the game. Sim tests replay a seed plus a command log and assert identical state, and round-trip the state through JSON.
  - **Judged on:** fun tests 1 and 4. The other three are answered and logged as a baseline. A no on test 4 pauses new features.

- [x] **P2 — Hauling and piles.** Dug material drops; goblins carry it to a pile. 5 goblins.

  **P2 decisions**

  **Pile capacity implementation status:** implemented. The rules below are what the build does.

  - **Map:** as P1, but z2 is Solid stone. This bends the band table (Stone is z4–9) because the prototype has only three levels. Stone digs in 40 ticks, dirt in 20.
  - **Goblins:** five Grunts start in a fixed cluster on z0 around the map centre. No other Roles yet.
  - **Loose items:** a dug tile, Stairs included, drops one unit of its material on that tile. Dirt is hauled like any other material; whether it is worth anything is decided later. Loose items do not block walking and are not subject to the Pile capacity limit.
  - **Piles:** every tile keeps a count per material. A Pile is a flag painted on Floor tiles with a third Tool. Units on a Pile tile are stored; units anywhere else are loose. Each Pile tile holds up to 30 units of each material, with no combined limit: 30 dirt and 30 stone can share a tile, but a 31st unit of either must go elsewhere. Goblins use the nearest reachable painted Pile tile with room for the material being hauled; they do not expand Piles onto unpainted tiles. The player paints more Piles to expand storage. The Pile is the first Room and needs no item to work.
  - **Painting and removing Piles:** painting over loose items stores them only if every material on that tile has at most 30 units. A tile with more than 30 of any material is skipped entirely, leaving all its items loose; the other eligible tiles in the rectangle are still painted. For example, 30 dirt and 30 stone can be painted, but a tile with 45 dirt and 5 stone is skipped. Painting neither moves nor deletes excess units. Right-drag with the Pile Tool removes Pile tiles, and their stock becomes loose. Dig stairs on a Pile tile clears the Pile once dug.
  - **Hauling:** every loose unit is a Haul Job. Goblins pick from one job list, digs and hauls together: the nearest reachable Job by path length (to the dig tile or the loose item) wins, ties broken by lowest tile index. A goblin carries one unit and drops it on the nearest reachable Pile tile with room for that material, destination ties broken by lowest tile index. Picking up and dropping off take 5 ticks each; carrying does not slow walking. A blocked material does not prevent hauling another material from the same source tile: if dirt storage is full but stone storage has room, stone can be hauled while dirt waits.
  - **Competing deliveries:** there are no advance reservations of Pile capacity. Capacity is checked again when a drop completes. If two goblins approach a tile holding 29 dirt, the first to finish dropping takes the last slot; the other retains its unit and searches again for available storage. Simultaneous completions follow the existing goblin index order. No delivery can exceed 30 units of its material on a Pile tile.
  - **Waiting hauls:** with no reachable Pile that has room for a loose unit's material, that unit stays put and its Haul waits, greyed out. A goblin already carrying a unit searches for another suitable Pile if its destination fills or is removed. If none is available, it waits holding the unit and takes no other Job until it can deliver; it does not drop the unit on the ground or overfill a Pile. Waiting work and carriers are reconsidered when storage availability or reachability changes, including when a Pile is painted or removed, a delivery fills a material's last slot, or a tile is dug.
  - **View:** a loose item is a small square in its material's colour (dirt brown, stone grey), greyed while its Haul waits. Pile tiles get a tinted fill and a count per material, drawn in that material's colour. At 30, that material's count is visibly highlighted; other material counts retain their normal appearance. Fullness is per material, so 30 dirt does not imply that the tile cannot accept stone. A carrying goblin shows its material on its marker, including while waiting for storage.
  - **Text:** no totals panel. The Pile Tool joins the key-hint line, written with `goblin-voice`. *Reversed after P3: see P3 decisions → HUD.*
  - **Saves:** as P1. Simulation saves containing a Pile tile with more than 30 units of any material are rejected, with no migration or grandfathered overflow. Loose item counts above 30 remain valid. There is still no player-facing save or load.
  - **Judged on:** fun tests 1 and 2. The other three are answered and logged as a baseline.

- [x] **P3 — Needs.** Grub, Grog and Sleep meters. Grub Pit, Fungus Farm, Brewery and Sleeping Heap rooms. Goblins eat, drink, sleep.

  **P3 decisions**

  - **Map and goblins:** as P2, but seven Grunts start in the cluster on z0, with 14 Grub and 7 Grog loose on one tile beside them. There is no starting Fungus.
  - **Materials:** Grub, Fungus and Grog are Materials. They drop loose, are hauled like dirt and stone, and obey the Pile limit of 30 units per material.
  - **Needs:** each goblin has Grub, Grog and Sleep meters, each starting at a random 70–100 from `SimRandom`. A goblin craves a Need when its meter falls below 30.
  - **Cravings interrupt:** a goblin craving a Need it can satisfy abandons its Job at once. Dig progress stays on the tile; Tend and Brew progress is lost. Anything it carries drops. This is the one exception to the P2 Waiting hauls rule against dropping a unit. A goblin on a Pile tile with room stores the unit there; on a Pile tile with no room for that material, the unit lands on the nearest walkable tile that can legally take it, ties broken by lowest tile index. Elsewhere the unit lands loose on the goblin's own tile.
  - **Which craving wins:** the lowest meter, ties broken Grub, then Grog, then Sleep. A goblin finishes satisfying one Need before it considers the next. With no reachable supply, a craving does not interrupt: the goblin keeps working and looks again when supply or reachability changes.
  - **Eating and drinking:** a craving goblin walks to the nearest reachable unit of Grub or Grog, on a Pile or loose, and consumes it there with no carrying. It may take a unit another goblin is about to haul; that hauler looks for other work. Nothing reserves a unit: when two goblins head for the last one, the first to finish consumes it and the other searches again.
  - **Sleeping:** a craving goblin walks to the nearest reachable Sleeping Heap tile, which any number of goblins can share, and sleeps until the meter is full. Other Needs keep draining and do not wake it, while the Sleep meter itself does not drain during sleep. With no reachable Heap, it sleeps on the spot at half the recovery rate. Sleep has no supply, so a Craving for Sleep is always satisfiable and is never passed over. The recovery rate follows the tile under the sleeper at each tick, so a Heap removed under it, or painted under it, changes the rate and nothing else.
  - **A Need at 0:** Grub at 0 means Starving: the goblin walks and works at half speed (eating and drinking are not slowed). Grog at 0 has no effect on speed or work; from P4 it raises Chaos like any unmet Need. Sleep at 0 means Collapse: the goblin drops what it carries, by the rule above, and sleeps on the spot until full, even if it was eating, walking to a meal or to a Heap, and nothing wakes it. If it cannot legally put down a unit it carries (every tile it can reach is a Pile full of that material), it keeps the unit and collapses on the first tick a place opens. Nothing dies in P3.
  - **Waiting carriers:** a goblin holding a unit with no Pile to take it drains Grog at double rate: 2 points per 35 ticks while it waits, 1 per 35 otherwise. Its craving eventually makes it drop the unit and go for a drink. With no Grog reachable it stays put, until Chaos takes over in P4. *Superseded in P4: the doubled drain is replaced by a Chaos contribution, below.*
  - **Rooms:** the Grub Pit, Fungus Farm, Brewery and Sleeping Heap each get a Tool and follow the Pile's painting rules: Floor tiles only, right-drag removes, Dig stairs clears. A tile holds one Room, so painting over a different Room skips that tile. All four work as soon as they are painted. The required-item rule returns once crafting and building exist.
  - **Farming:** a Tend Job appears on a Grub Pit or Fungus Farm tile that is untended and holds no unhauled unit of its crop. One Tend on a Grub Pit tile yields 3 Grub, spawned one at a time. One Tend on a Fungus Farm tile yields 1 Fungus after a growth timer. Fungus Farms work only on z1 or deeper. The first Grub of a Tend spawns 200 ticks after the Tend ends. Removing a farm tile discards anything it has not yet spawned. Tend Jobs join the one job list, nearest first, with no priorities.
  - **Brewing:** a Brew Job takes one reachable Fungus, on a Pile or loose, to a free Brewery tile, one brewer per tile, and works it into 1 Grog. The Grog drops loose on that tile and is hauled to the nearest Pile, as is anything a craft station makes. A Brew Job waits, greyed out, while Grog has no Pile with room. An interrupted brewer drops its Fungus.
    - *Clarified in implementation (#36):* the Brew Job sits at the Fungus and joins the one job list nearest first by path to it, ties to the lowest tile index, a Haul beating a Brew on the same tile. It is offered only while a free Brewery tile and a Pile with room for Grog are both in reach of that Fungus, and "waiting" is the Brewery tile drawn grey. A Fungus unit is claimed once, by a brewer or a hauler, and a brewer takes it off the tile when the pick-up ends (5 ticks, as for a Haul), claiming the nearest free Brewery tile then. Grog room is checked before the Fungus is taken and again on every working tick, and is never reserved: a brewer at work while no Pile in reach has room for Grog stops, holding its Fungus and keeping its progress, and carries on from that progress when room appears (a Pile painted, Grog drunk or hauled off, the way opened); no Grog is made while it waits. A brewer's claim on a loose Fungus is a claim on that unit only, so it costs a hauler nothing on any other unit of the tile, and a hauler never takes Fungus a brewer has claimed. A brewer whose Brewery is cleared or repainted carries its Fungus to a Pile, the progress lost; a Craving or Collapse drops it by the usual rule.
  - **Numbers:** kept together in one place in the sim, to be tuned after play.

    | Value | Number |
    | --- | --- |
    | Grub drain | 1 point per 25 ticks |
    | Grog drain | 1 point per 35 ticks |
    | Sleep drain | 1 point per 45 ticks |
    | Eating or drinking | 10 ticks, +50 to the meter |
    | Sleep recovery | 1 point per 5 ticks on a Heap, 1 per 10 on the floor |
    | Tend | 20 ticks |
    | Grub spawn | 1 per 200 ticks |
    | Fungus growth | 300 ticks |
    | Brew | 20 ticks |

  - **View:** each Room has a tile tint and each new Material a colour for its loose squares. A sleeping goblin shows a small "zzz", a craving goblin a pip coloured by its Need (the lower meter's, Grub on a tie, when it craves both), and a Starving goblin a tint. A Pile tile shows two counts, full materials first, then the largest. One hover line shows the meters of the goblin under the cursor, or every count on the Pile tile under it. *The hover line became the Inspector after P3: see HUD.*
  - **Text:** no event log until P6. New Tools join the key-hint line. All new text is written with `goblin-voice`.
  - **Saves:** as P2. New state goes in the JSON, and old saves are not migrated.
  - **Judged on:** fun test 2. The other four are answered and logged as a baseline. If test 2 is still a no, apply Tune before starting P4 (see `docs/playtests/2026-10-31-P2.md`).
  - **HUD (added after the P3 fun check, #46):** the key-hint line becomes a Tool palette: one button per Tool, showing its key, with its joke as the tooltip and a short hint for the picked Tool. A top bar holds Pause, Speed and level buttons and a tick clock. Every key still works, and keys and buttons stay in step. The hover line becomes the Inspector. A Warren summary shows, per material, units stored on Piles and units loose, and counts goblins by state. This reverses P2's "no totals panel": that rule kept attention on the map while the fun was unproven, and P3's five yeses settled it. Grey boxes and plain Godot Controls only, with no art. While paused, the map wears a frame and a "Paused" label outside its edge rather than dimming, because the player plans and designates while paused.
  - **Later, not P3:** a goblin whose Haul gets eaten is annoyed, which can become a Grudge or Chaos in P4. A waiting carrier's boredom can move to Chaos in P4. The "zzz" gets replaced with something better.

- [ ] **P4 — Chaos.** Chaos meter from unmet needs. At least 3 acting-out behaviours (shirk, fight, steal).

  **P4 decisions — approved 2026-11-07**

  - **Purpose:** test a recoverable feedback loop: neglected Needs cause recognisable misbehaviour, misbehaviour disrupts production, and improving the warren lets goblins recover. Judged primarily on fun tests 2 and 3; the other three are answered and logged as a baseline.
  - **Scope:** Needs-driven Chaos with shirk, fight and steal, including waiting-carrier boredom and annoyance when a goblin's Haul gets eaten. Quirks, persistent Grudges, Boss Authority and fire are deferred.
  - **Shirking:** abandon the Job and loaf on the spot for 100 ticks, refusing available Jobs. Needs can interrupt. Unlike ordinary idling, shirking does not wander or accept work.
  - **Fights:** an instigator approaches another available, awake goblin and scuffles for 30 ticks. Both then lose 100 ticks of work, unless interrupted by Needs. There is no health, permanent injury or death. The victim gains 15 Chaos once, when the scuffle starts; only the instigator receives the usual completion relief. Goblins sleeping, already seeking or satisfying Needs, or in the acting-out cooldown are excluded as targets. Choose the nearest eligible goblin on the instigator's own z-level, ties by goblin index. Make contact on the same tile or an orthogonal neighbour within 100 ticks, or abandon the attempt. The victim keeps working until contact, when eligibility is checked again. Each goblin can participate in only one scuffle at a time.
  - **Theft:** take one Grub or Grog from a Pile, carry it to a nearby tile outside a Pile, and consume it privately, even without a Craving. Consumption removes the unit from communal supplies and restores 50 points to that Need, capped at 100, as ordinary consumption does. There is no private inventory or permanent stash and no new Shinies Material. Loose supplies remain for ordinary eating and hauling. Choose the nearest reachable Pile containing either supply, ties by tile index, then Grog before Grub. Pickup takes 5 ticks and private consumption 10. The destination is a reachable non-Pile tile on the source's z-level within four walking steps of the source: choose the farthest eligible tile within that range, ties by tile index. Without a valid destination theft is unavailable. Supplies are not reserved; losing the unit before pickup fails the attempt. Carried supplies are unavailable to others.
  - **Readability:** seven distinct authored names, assigned consistently by goblin index. Hover lists all goblins sharing the tile with their names, Needs, Chaos and current activity. Fight activity identifies the other participant; theft identifies the material. On-map indicators distinguish shirk, fight and steal while retaining Need and carried-material cues. No persistent event history until P6. All player-facing text goes through `goblin-voice`.
  - **Chat bubbles — provisional:** try small illustrative symbols with short, prewritten lines when shirking begins, a scuffle starts, stolen supplies are picked up or consumption cancels a Haul. Display for three seconds of unpaused real time, including at 4× speed. Allow one bubble per goblin and at most three overall; newer bubbles replace older ones. Bubbles are temporary presentation, leave no history and do not affect simulation randomness. Names and lines go through `goblin-voice`. Keep map indicators and hover information available after bubbles disappear. Theo will decide after play whether to retain this treatment, replace it with representative icons or try something else.
  - **Unmet Needs:** each Need below 30 contributes to Chaos, even if supply is reachable. The Need currently being restored by eating, drinking or sleeping is excluded; other low Needs still contribute, including during sleep.
  - **Waiting carriers:** replace P3's doubled Grog drain with a direct Chaos contribution while waiting for storage. Ordinary Grog drain continues. Being thirsty and being stuck are separate pressures.
  - **Lost Hauls:** when another goblin consumes supplies and thereby cancels a goblin's claimed Haul, the displaced hauler immediately gains Chaos. This records no persistent Grudge. Claims are quantities on a tile, not particular units; cancellation caused by painting a Pile or another hauler collecting supplies causes no annoyance.
  - **Starting trouble:** Collapse and existing sleep remain protected. A satisfiable Craving takes priority over starting an acting-out behaviour; Chaos does not divert a goblin already seeking or satisfying a Need. It can interrupt work, wandering or waiting with a carried unit. At 70 Chaos, choose with equal probability among currently eligible behaviours through `SimRandom`. Shirking is always an option, fighting requires an eligible target and theft requires supplies and a destination.
  - **Abandoning work and carrying:** starting an act preserves Dig progress, loses Tend/Brew progress, releases claims and puts down carried material by P3's legal-drop rules. With no legal reachable place for the material, retain it and defer starting the act. Interrupting a thief carrying stolen supplies uses the same legal-drop rules. Chaos never deletes materials or overfills Piles.
  - **Interruptions and completion:** a satisfiable Craving or Collapse can interrupt any awake stage, including shirking, pursuit, scuffling, post-fight recovery and theft. Interrupted acts are abandoned, never resumed. Completion relief requires finishing the shirking time, the scuffle or consumption of the stolen unit; ending early gives no relief.
  - **Cooldown:** after success, interruption or failure, wait 300 ticks before starting another act. Interrupted and failed attempts give no Chaos relief. Both participants in a fight receive the cooldown and cannot be targeted for another fight while it lasts. Chaos can still accumulate during the cooldown.
  - **Recovery:** Chaos is bounded at 0–100 and starts at 0 for every goblin. It fades gradually when no Need is below 30 and the goblin is not a waiting carrier. Completing an acting-out behaviour gives additional relief. One meal does not instantly clear accumulated Chaos.
  - **Initial tuning:** each unmet Need contributes 1 Chaos per 10 ticks; waiting for storage contributes another 1 per 20 ticks; a Haul cancelled by another goblin consuming supplies adds 15 immediately. Recovery removes 1 per 10 ticks. Acting out becomes possible at 70 Chaos. Completing an act removes 40 Chaos and is followed by 300 ticks before another act can start. These are starting values kept together for playtest tuning, not permanent balance.
  - **Shared fight outcome:** a satisfiable Craving or Collapse during the scuffle ends it for both participants, with no completion relief or post-fight recovery. The victim retains the Chaos gained when attacked. A full 30-tick scuffle grants the instigator relief immediately; both participants begin their 100-tick recovery and 300-tick cooldown together. Interrupting recovery does not restart the cooldown. Exclude targets already acting out or unable to legally put down a carried unit. An abandoned pursuit affects only the instigator.
  - **Changed theft plans:** the attempt fails if its source stops being a Pile before pickup, or its chosen destination becomes a Pile or unreachable. Before pickup nothing moves; after pickup the thief legally drops the unit, ending without relief. With no legal drop it retains the unit and waits until safe abandonment is possible. Do not repeatedly choose new theft destinations.
  - **Timing and starvation:** starvation slows pursuit and theft walking. Shirking, scuffling, post-fight recovery, theft pickup and consumption retain their stated durations. Chaos accumulation and cooldowns keep normal sim time. Apply accumulation and recovery on regular sim-wide 10-tick and 20-tick intervals, checking the relevant conditions at each interval.
  - **Starting setup and acceptance:** retain P3's starting map, goblins and supplies. Playtest by deliberately creating shortages or full storage, observing trouble and then fixing the conditions. Demonstrate all three behaviours, reduced trouble under good provisioning and recovery after disruption, without extra starting resources or debug controls. Headless tests cover each behaviour and deterministic replay. Record specific incidents; both fun tests 2 and 3 must be yes before P5, otherwise tune P4.
  - **Fun-check coverage:** every new fun check includes a short checklist of the current milestone's behaviours and retained behaviours from earlier milestones, with observed outcomes. For P4 cover shirk, fight, theft and recovery, plus digging/stairs, hauling/Pile capacity, production and Need interruption/sleep. Check bubble readability at 1× and 4×, pause behaviour and overlapping goblins; record Theo's preference on retaining bubbles or revising their presentation. Use the current cumulative design: P4's waiting-carrier Chaos deliberately replaces P3's doubled Grog drain.
  - **Built so far (#48):** Chaos, the Needs that raise it, waiting-carrier Chaos, lost Hauls, shirking with its cooldown, and the Inspector listing every goblin on a tile with its Chaos, with a lilac ring and band on a shirker. Fights (#49) are built: see below. Bubbles are still to build; theft is built (#50, below), and the choice among the acts is now the draw described there. The sim also raises three transient events for the later bubble slice, `ShirkStarted`, `HaulCancelled` and `ScuffleStarted` (instigator and victim), and keeps no log of them. Two choices the decisions left open: an interval counts a Need as "being restored" only while the goblin is Eating, Drinking or Sleeping, not while it is walking to the meal or the Heap, and a shirking goblin counts as idle in the Warren summary.
  - **Built so far (#49):** fights. Choices the decisions left open: the pursuit, scuffle and recovery clocks (`GoblinFightTicks`) count down on every sim tick, so Starving slows only the pursuer's walking; a stage ends before any goblin's turn on the tick its clock reaches 0, so goblin order never decides whether a scuffle finished, while contact and contention between simultaneous pursuers are resolved in goblin index order (the lower index scuffles, the other's attempt fails with no replacement). The tick of contact is the first of the 30 scuffle ticks, and the tick a scuffle ends is the first of the 100 recovery ticks, as a shirk's first tick is the one it begins on. Contact is checked on arrival as well as at the start of each turn. A fight is the draw `NextInt(2)`, 1 for a fight and 0 for shirking, made only when both are eligible. Recovery names the partner and keeps no further link: interrupting one fighter's recovery leaves the other's running. The Warren summary counts every stage of a fight as idle. The map marks a fight with an orange ring (crossed while scuffling, duller while recovering); the lilac ring and band stay the shirk's.
  - **Built so far (#50):** theft. The acts open to a goblin at 70 Chaos are listed in this order: shirking (always), a fight (while a target is in reach), theft (while a source and a destination exist). One is drawn with equal chance, `NextInt(n)` over the `n` listed, and when only shirking is open nothing is drawn, so the earlier fight draw (`NextInt(2)`, 1 for a fight) is unchanged. Choices the decisions left open: only the nearest reachable Pile holding Grub or Grog is considered, and if no destination exists for it theft is unavailable (the next-nearest Pile is not tried). The supply is fixed at the start (Grog if the Pile holds both) and so is the destination; losing the supply it chose fails the attempt even if the other supply remains. The theft stages are `Sneaking` (walking to the Pile), `Pilfering` (5 ticks; the unit leaves the Pile on the fifth, the only moment stock is removed, and `TheftPickedUp` is raised for the later bubbles), `MakingOff` (walking with the unit in hand), `Scoffing` (10 ticks; consumed on the tenth) and `Stalled`. Before the pickup the plan is checked on every tick (source still a Pile holding the unit, destination still not a Pile) and the destination's reachability on every walking step and at the pickup; after it, the destination is checked while walking and while scoffing. A failure moves nothing before the pickup; after it the unit goes down by the Craving drop rule, and with no legal place the thief becomes `Stalled`: it holds the unit, consumes nothing, gets no relief and no cooldown, and looks for a place every tick, putting the unit down and starting the 300-tick cooldown the tick one opens. A Craving or Collapse takes a thief out of any stage, `Stalled` included, by the same drop rule. Walking is slowed by Starving, pickup, consuming and waiting are not. While scoffing, the Need the unit feeds counts as being restored for Chaos, as it would while eating or drinking. Every stage is acting out, so a thief starts no other act and is no fight target, and the Warren summary counts a thief as idle with its unit among the loose. The map marks a thief with a crimson ring and a crimson mask across the top of its body, the carried unit and Need pip drawing as before. A save has two more arrays (`GoblinTheftDestination`, `GoblinTheftSupply`); an older save without them is rejected, with no migration.
  - **Numbers:** kept together in `ChaosRules`, beside the Needs' own.

    | Value | Number |
    | --- | --- |
    | Unmet Need | +1 Chaos per 10 ticks each |
    | Waiting for storage | +1 Chaos per 20 ticks |
    | Haul cancelled by a meal | +15 Chaos once |
    | Recovery | −1 Chaos per 10 ticks |
    | Acting out begins | 70 Chaos |
    | Shirk | 100 ticks, −40 Chaos on finishing |
    | Cooldown | 300 ticks |
    | Pursuit | up to 100 ticks |
    | Scuffle | 30 ticks, victim +15 Chaos once, instigator −40 Chaos on finishing |
    | Recovery | 100 ticks of no work, after a full scuffle |
- [ ] **P5 — One invasion.** A 3-hero party walks in from the surface. One trap type. Brutes fight back.
- [ ] **P6 — Legends.** Event log with templated entries for deaths, invasion, and chaos.

**Fun tests (answer honestly after each playthrough)**

1. Did I watch the goblins without wanting to speed up time?
2. Did something happen that I didn't plan and want to tell someone about?
3. Did a goblin's death or misbehaviour make me laugh or feel something?
4. Did I want to dig one more level to see what was there?
5. Would I play a second run with a different warren layout?

**Go / no-go:** if 3 or more are a clear yes after P6, the idea has legs. If questions 2 and 3 are no, the emergence is not working, and more content won't fix it.

## Tech plan in Godot

Keep the simulation separate from Godot. The sim is plain code that runs on fixed ticks; Godot only draws it and sends player commands in. This keeps the game engine-agnostic and makes it easy to test.

| Layer | Responsibility | Language |
| --- | --- | --- |
| Sim core | World grid, goblins, needs, jobs, pathfinding, events | C# (plain classes, no Godot types) |
| Command layer | Player orders in (dig, zone, raid), events out | C# |
| Renderer | One TileMapLayer per visible z-level, goblin sprites, redraw only changed chunks | GDScript or C# |
| UI | Job designation, goblin inspector, legends panel | GDScript |

**Decisions for the prototype**

- **World storage:** flat arrays indexed by (x, y, z), split into 16×16 chunks for dirty-flag redraws.
- **Tick rate:** sim at 10 ticks per second at normal speed, decoupled from frame rate. Speed controls multiply ticks.
- **Pathfinding:** plain A\* on the 3D grid is fine for P1–P3. Plan for hierarchical paths between chunks before going past about 50 goblins.
- **Jobs:** a global job list (dig, haul, tend, brew, build) that idle goblins claim. Needs override jobs when low.
- **Saves:** serialise the sim core to JSON from day one. It also makes bugs reproducible.
- **Determinism:** seeded random number generator in the sim, so a seed plus a command log replays a run.

## Risks, scope, and open questions

The biggest risk for a solo colony sim is scope, not tech. The prototype is deliberately tiny so you can kill or commit to the idea in weeks, not months.

| Risk | Why it matters | Mitigation |
| --- | --- | --- |
| Scope creep | Colony sims invite endless systems | Nothing outside the Not-in-scope list until P6 passes the fun tests |
| Emergence feels flat | Simple systems can produce boring stories | Judge by fun tests 2 and 3; tune chaos and quirks before adding content |
| Pathfinding performance | Many agents on a 3D grid | Cap at 30 goblins in the prototype; hierarchical paths later |
| Comedy needs a lot of writing | Repeated jokes go stale fast | Templates with variants; write jokes after systems work |
| Readability of z-levels | Players get lost in layers | Faded lower level, clear stair markers, a depth indicator |
| Comparison to existing games | Players will compare it to DF and Rimworld | Lean hard into goblin identity: theft, chaos, the Boss |

**Open questions**

- [ ] How does the goblin brain choose between needs, jobs, and chaos? (P3 settles needs against jobs; chaos is the next design pass.)
- [ ] Is there a win condition, or is it endless until the warren falls?
- [x] Real-time with pause, or a fixed set of speeds? **Both:** real-time with pause, plus 1×, 2× and 4× speeds. Speed changes how many ticks run per frame, never the tick length.
- [x] How much direct control: designations only, or can you order a specific goblin? **Designations only** for the prototype.
- [ ] Art direction for the visual version: pixel art, hand-drawn, or something else?
