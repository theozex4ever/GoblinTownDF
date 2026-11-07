# #49 fights: scripted demonstration

Produced by a **scripted** run of the real `Main` scene (`FightDemo.cs.txt`, a throwaway scene that was not committed), not by a human playtest. It runs `Main` in a viewport at 4× with no orders, no extra supplies and no debug controls, waits for a fight to arise from Chaos, pauses at each stage, puts the pointer on the fighter with an engine-local mouse-motion event, and saves the viewport. Seed 1794078431; the fight is `rawlog.txt`.

| Stage | Image |
|---|---|
| Nubbin Splatt (70 Chaos) pursues Kevin; Kevin is still working | `1-pursuing.png` |
| Contact at tick 3687: scuffle, orange cross on both, victim +15 | `2-scuffling.png`, `2b-victim.png` |
| Scuffle ran 30 ticks: instigator 71 to 35 Chaos, both Recovering, cooldown 300 | `3-recovering.png` |
| Recovery ended 100 ticks later: both Idle, cooldown 200 | `4-after-recovery.png` |

A human has not yet played this. The fun check waits for the end of P4.
