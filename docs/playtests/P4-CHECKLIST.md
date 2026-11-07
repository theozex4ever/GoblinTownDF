# P4 playtest checklist

**Prepared for Theo's real fun check. Nothing here has been played by a human.** Every outcome below is **not seen** until Theo plays and says otherwise; scripted runs under `docs/evidence/` are not playtests. This is not a dated playtest record: after play, load the `fun-check` skill, which writes `docs/playtests/YYYY-MM-DD-P4.md` from [`TEMPLATE.md`](TEMPLATE.md) with the outcomes, the five fun answers and the verdict.

P4 advances to P5 only when fun questions 2 and 3 are both yes; otherwise tune P4. P3's starting map, goblins and supplies are unchanged, and there are no debug controls: create trouble by neglecting the warren, then fix it as a player would.

Record each row as **observed / failed / not seen**, with a line of evidence (tick or goblin name, what you saw). Unexercised rows stay **not seen**.

| # | Check | How to look | Outcome | Evidence |
| --- | --- | --- | --- | --- |
| 1 | **Shirking** refuses available work and is identifiable by name and activity | Give no orders at first, press <kbd>4</kbd>, wait for a lilac ring and band. Hover: name, Chaos at 70 or more, "Shirking". Give a dig order and see whether the shirker leaves it alone for its 100 ticks. | not seen | |
| 2 | **A fight** names both goblins, disrupts work and does not repeat at once | Watch for an orange ring and cross. Hover each: the pursuer and the victim both name the other. Their work stops for about 130 ticks; neither starts another act for 300. | not seen | |
| 3 | **Theft** visibly carries one unit from a Pile to private consumption | Needs a Pile holding Grub or Grog. A crimson ring and mask, the unit's colour on the body, a stop a few tiles from the Pile, one unit gone from the count. | not seen | |
| 4 | **Shortage or full storage, then the fix**: trouble drops and the warren recovers | Starve it (no Grub Pit, no Brewery, no Heap) or paint a one-tile Pile and dig a lot. Watch Chaos climb; then paint Pile, Grub Pit, Brewery, Heap and see Chaos fall and misbehaviour stop. | not seen | |
| 5 | **Digging, linked stairs and cancelled work** still work | Dig a room, dig stairs down and the matching stairs below, cancel a dig with right-drag mid-work. | not seen | |
| 6 | **Hauling, Pile capacity, competing deliveries, dropped materials** still work | A Pile fills at 30 per material; extra stays loose and greys; goblins carrying dirt wait when no Pile has room and drop it for a Craving. | not seen | |
| 7 | **Grub production, underground Fungus and brewing** still work | Grub Pit yields Grub; a Fungus Farm below the Surface grows Fungus; a Brewery turns it into Grog. | not seen | |
| 8 | **Cravings, sleep and Collapse** still work; waiting-carrier Chaos replaces P3's doubled Grog drain | A Craving sends a goblin to eat, drink or sleep. A goblin stuck holding a unit has its Chaos rise; its Grog drains at the ordinary rate. | not seen | |
| 9 | **Chat bubbles**: readability at 1× and 4×, pause, overlap, level changes | See below. | not seen | |

## Row 9: bubbles

Bubbles are a provisional experiment. They appear over a goblin for three seconds when a shirk begins, a scuffle starts, a thief picks up its unit, or another goblin's meal eats its Haul.

- **1× and 4×:** does a bubble last about the same time at both? Can you read it before it goes?
- **Pause:** pause while one is up. It should hold, and carry on when you unpause.
- **Overlap:** with several goblins close together, are the bubbles still readable and attached to the right goblin? Never more than three, one per goblin.
- **Levels:** change level while one is up; it should not appear over the wrong place.
- **After they go:** the name, fight partner, theft material, Need pips and carried squares in the Inspector and on the map are all still there.
- **Not in the way:** you can still drag, dig and paint under a bubble.

**Theo's preference to record (his own words):** keep bubbles as they are, replace them with representative icons, or try something else? Lost-Haul bubbles may be the most frequent in the starting setup, which is worth saying if they feel like noise.

## Then

Ask the five fun questions one at a time, ask for the one moment worth telling a friend about, and let the `fun-check` skill write the record and the verdict. Do not fill in answers for Theo.
