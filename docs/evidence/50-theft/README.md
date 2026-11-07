# #50 theft: evidence

**Not a human playtest.** Everything here comes from tests and from a scripted run of the real `Main` scene; nobody has played this build. The fun check waits for the end of P4.

- `tdd-log.md` — red/green output per slice, including two disclosed slices (S5, S8) that were not red-first, and a mutation check showing the S5 tests are not vacuous.
- `check.log` — `bash tools/check.sh` at the end of the work: 894 sim tests (806 before), 42 selection scenarios (41), 13 pixel scenarios (12), inspector layout checks, headless boot with no console errors.
- `TheftDemo.cs.txt` — the throwaway driver (not committed as a scene). It runs `Main` in a viewport with only ordinary player commands (a Pile over the starting supplies, a Grub Pit, a dig, stairs down, a Fungus Farm, a Brewery and a Pile below), no extra supplies and no debug controls, fast-forwards with `Advance` until a goblin steals of its own accord, pauses at each stage, puts the pointer on the thief with an engine-local mouse-motion event, and saves the viewport. Seed 1794081660; the account is `rawlog.txt`, the console `console-run3.txt`.

| Stage | Image |
|---|---|
| Old Bogwash (73 Chaos) at the Pile, Pocketing a Grub (it was already on the Pile, so no Sneaking stage in this run) | `2-pilfering.png` |
| Making off, Grub unit visible in the crimson ring and mask, Pile stock 28 | `3-makingoff.png`, `3-makingoff-zoom.png` |
| Scoffing the stolen Grub four tiles away, off the Pile | `4-scoffing.png`, `4-scoffing-zoom.png` |
| Done: Chaos 71 to 30, cooldown 300, Grub meter 100 | `5-idle.png` |
| 4000 ticks later the warren carries on | `6-later-warren.png` |

Ordinary life around the theft (same run, counted from task transitions): by the theft's end 18 meals, 11 drinks, 134 hauls, 32 brews, 60 tends; over the next 4000 ticks, +24 meals, +17 drinks, +149 hauls, +44 brews, +66 tends. Grub went 60 to 59 at the pickup (carried counted), not twice.

**Limits.** The Sneaking stage was not captured in the final run (an earlier 60,000-tick run logged Sneaking; its screenshots were overwritten when the driver was fixed to follow the thief's level). Stalled is covered only by tests: it needs every reachable tile to be a full Pile and does not arise in ordinary play. The game was launched through the Godot MCP server (`run_project`): boot ok, no errors in `get_debug_output`.
