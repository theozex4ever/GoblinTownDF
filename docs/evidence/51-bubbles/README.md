# #51 provisional chat bubbles: evidence

**Not a human playtest.** Everything here is tests plus a scripted run of the real `Main` scene; nobody has played this build. The P4 fun check is still to do: see `docs/playtests/P4-CHECKLIST.md`.

Base: `74f1e994cc96e9f585a90793aa43b350449134b0` (merge of #56). No sim change: 902 sim tests before and after.

## What was run

- `BubbleDemo.cs.txt`: the throwaway driver (not committed as a scene). It runs `Main` in a 1280×720 `SubViewport` in a native window (X11/Vulkan on the developer machine) using only ordinary orders (a Pile over the starting supplies, a Grub Pit, a dig, stairs down, a Fungus Farm, a Brewery and a Pile below), with no preset Chaos, no extra supplies and no debug controls. Key and mouse events were handed to `Main` engine-locally (`_UnhandledKeyInput`, `PushInput`); nothing reached the host desktop. Seed 1794091265 (`rawlog.txt` line 1).
- Phase 1 plays forward with `Main._Process(0.1)` until each kind of bubble has really occurred, pausing (Space) at each to save the viewport, again after two seconds of wall clock paused, and once to go down a level and back.
- Phase 2 hands `Main` back to the engine's own frames and times a fresh bubble against the wall clock at 1×, at 4× and across a pause.
- `rawlog.txt`: the account; `console-run.txt`: the console, no ERROR lines.

## Timing (wall clock, from `rawlog.txt`)

| Case | Result |
|---|---|
| 1× | gone after 2994 ms (remaining −0.003 s at removal); 30 ticks ran, 10.0 per second |
| 4× | gone after 3000 ms; 120 ticks ran, 40.0 per second |
| 1× with a pause at 1005 ms unpaused | remaining 1.982 s when paused and 1.982 s after 3.00 s paused; gone after 6013 ms wall, 2954 ms unpaused |
| Pause at each first sighting (four kinds) | remaining 3.000 s before and after 2 s paused; tick unchanged |

Two 4× attempts ended early because the bubble's goblin took the stairs off the viewed level (logged as such, then retried): by design a bubble leaves with its goblin's level.

## Images

`1-<kind>.png` (full viewport) and `1-<kind>-zoom.png` (3× crop of the bubble and its goblin) show each incident type as it first occurred; `…-paused-2s-later` shows the same frame after two seconds paused. `2-other-level-down.png` is the level below with the bubble gone; `2-back-on-level.png` is back up (a bubble does not return). `3-*` are the timing run (`t100ms`, `t1500ms`, `t2800ms`, `after-expiry`, and the pause frames). The shirk, scuffle and theft zooms show the goblin's ring, band, mask, pips and carried unit still drawn beside the bubble.

## Observations to carry to the playtest

In the run of 22,082 ticks, the bubbles by kind were Shirk 1, Scuffle 1, Theft 1, Lost Haul 15. Lost-Haul bubbles are common in the starting setup and may read as noise; Theo's call.

## Limits

Seed is the wall-clock seed `Main` picks, so the run is not reproducible; it is a demonstration. Overlap of several bubbles is shown in a few frames (a scuffle with an older Lost-Haul bubble on show) and in the pixel checks, not in a staged crowd. The stack of checks, and what each proves:

- `BubbleChecks` (headless, 11 scenarios): event to bubble, authored copy, lifetime, replacement and the limits, off-level, placement geometry, timing through `Main._Process` at 1×/4×/paused, level changes, a real incident, and a run through `Main` against a bare run of the same seed with identical saved state and RNG.
- `CarryDrawChecks` (needs a renderer, 17 pixel scenarios, 4 new): symbol, rim, paper and text pixels for each kind at corners and edges, three bubbles on one tile, cues kept and paper gone after expiry, off-level.
- `SelectionInputChecks` (43 scenarios, 1 new): drags and right-drags under three bubbles, the Inspector listing every goblin on a shared tile, unchanged after expiry.
