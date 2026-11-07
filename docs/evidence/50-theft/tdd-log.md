# #50 theft — TDD log

Each slice: the focused tests were written and run RED against the scaffold (new Task names, two arrays and the event existed as bare data; no behaviour), then the minimum behaviour was added and the same tests run GREEN. Output is trimmed to the pass/fail lines. Some headings were written by the logging script as it ran; where a heading and its note disagree (S3's first "green", S5's "red"), the note is the truth. S5 and S8 were not red-first and say so.


### S1 choosing: source, destination, equal-probability draw — red
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"` (19 tests: 13 failed, 6 passed — the 6 are the ones that expect shirking, which the scaffold already did)
```
  Failed Goblins.Sim.Tests.TheftTests.A_goblin_at_70_Chaos_with_Grub_on_a_Pile_sets_out_to_steal_when_the_draw_picks_theft 
  Failed Goblins.Sim.Tests.TheftTests.A_nearer_destination_is_taken_when_the_corridor_is_shorter_than_four_steps 
  Failed Goblins.Sim.Tests.TheftTests.A_Pile_holding_both_supplies_is_taken_for_Grog_before_Grub 
  Failed Goblins.Sim.Tests.TheftTests.A_Pile_that_cannot_be_walked_to_is_no_source 
  Failed Goblins.Sim.Tests.TheftTests.A_Pile_tile_is_never_the_destination 
  Failed Goblins.Sim.Tests.TheftTests.Equally_near_Piles_go_to_the_lowest_tile_index 
  Failed Goblins.Sim.Tests.TheftTests.Four_steps_are_walked_not_flown_so_a_tile_behind_a_wall_is_too_far 
  Failed Goblins.Sim.Tests.TheftTests.Shirking_a_fight_and_theft_are_equally_likely_when_all_are_eligible 
  Failed Goblins.Sim.Tests.TheftTests.The_destination_is_on_the_level_of_the_source 
  Failed Goblins.Sim.Tests.TheftTests.The_destination_is_the_farthest_tile_within_four_steps_of_the_source_and_ties_go_to_the_lowest_tile_index 
  Failed Goblins.Sim.Tests.TheftTests.Theft_and_shirking_are_equally_likely_when_there_is_no_one_to_fight 
  Failed Goblins.Sim.Tests.TheftTests.The_source_is_the_nearest_Pile_holding_Grub_or_Grog_by_walking_distance 
  Failed Goblins.Sim.Tests.TheftTests.With_shirking_a_fight_and_theft_all_eligible_the_draw_is_one_of_three(draw: 2, expected: Sneaking) 
```

### S1 choosing: source, destination, equal-probability draw — green
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Passed: 19
Total tests: 19
```

### S2 lifecycle: walk, 5-tick pickup, carry, 10-tick consume, completion — red
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Failed: 9
  Failed Goblins.Sim.Tests.TheftTests.A_thief_carries_the_unit_visibly_to_its_destination_at_the_ordinary_pace_and_consumes_it_in_10_ticks 
  Failed Goblins.Sim.Tests.TheftTests.A_thief_walks_to_the_Pile_at_the_ordinary_pace_then_picks_the_unit_up_in_5_ticks_and_only_then_is_it_gone_from_the_Pile 
  Failed Goblins.Sim.Tests.TheftTests.Consuming_a_stolen_Grog_restores_50_Grog_and_nothing_else 
  Failed Goblins.Sim.Tests.TheftTests.Consuming_a_stolen_Grub_restores_50_Grub_capped_at_100_and_touches_no_other_meter(before: 40, expected: 90) 
  Failed Goblins.Sim.Tests.TheftTests.Consuming_a_stolen_Grub_restores_50_Grub_capped_at_100_and_touches_no_other_meter(before: 70, expected: 100) 
  Failed Goblins.Sim.Tests.TheftTests.Finishing_removes_40_Chaos_and_starts_the_300_tick_cooldown_once_with_no_other_reward 
  Failed Goblins.Sim.Tests.TheftTests.Starving_slows_the_thiefs_walking_but_not_the_pickup_or_the_private_consumption 
  Failed Goblins.Sim.Tests.TheftTests.The_pickup_raises_one_transient_event_naming_the_goblin_and_the_supply 
  Failed Goblins.Sim.Tests.TheftTests.The_unit_is_conserved_until_the_instant_it_is_consumed_and_never_appears_twice 
     Passed: 21
Total tests: 30
```

### S2 lifecycle: walk, 5-tick pickup, carry, 10-tick consume, completion — green
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Passed: 30
Total tests: 30
```

### S3 changed plans before the pickup — red
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"` (first run, before `PlanHolds` / `FailTheft` existed; 10 of the 11 new cases failed — the 11th, a hungry goblin eating the last unit, was rewritten from a duplicate and run only after the implementation)
```
     Failed: 10
  Failed A_destination_painted_as_a_Pile_before_the_pickup_fails_the_attempt_and_no_replacement_is_chosen(stage: Pilfering)
  Failed A_destination_painted_as_a_Pile_before_the_pickup_fails_the_attempt_and_no_replacement_is_chosen(stage: Sneaking)
  Failed A_destination_that_can_no_longer_be_reached_fails_the_attempt_before_the_pickup
  Failed A_source_that_stops_being_a_Pile_before_the_pickup_fails_the_attempt_and_leaves_the_stock_where_it_lies(stage: Pilfering)
  Failed A_source_that_stops_being_a_Pile_before_the_pickup_fails_the_attempt_and_leaves_the_stock_where_it_lies(stage: Sneaking)
  Failed Losing_the_Grog_it_chose_fails_the_attempt_though_Grub_remains_on_the_same_Pile
  Failed Losing_the_selected_unit_before_the_pickup_fails_the_attempt_and_moves_nothing(stage: Pilfering)
  Failed Losing_the_selected_unit_before_the_pickup_fails_the_attempt_and_moves_nothing(stage: Sneaking)
  Failed Two_thieves_after_the_last_unit_leave_it_to_the_one_who_picks_it_up_first_and_the_other_fails_with_nothing_moved
  Failed A_goblin_eating_the_last_unit_where_it_lies_leaves_the_thief_to_fail   (replaced; a duplicate of the unit-lost case)
     Passed: 30
Total tests: 40
```

### S3 changed plans before the pickup — first green attempt: 39/40, the failure a set-up mistake in the new two-thief test (goblin 0 also had a fight target, so its draw was one of three); fixed in the test, not the code
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Failed: 1
  Failed Goblins.Sim.Tests.TheftTests.Two_thieves_after_the_last_unit_leave_it_to_the_one_who_picks_it_up_first_and_the_other_fails_with_nothing_moved 
     Passed: 39
Total tests: 40
```

### S3 changed plans before the pickup — green
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Passed: 40
Total tests: 40
```

### S4 changed plans after the pickup: legal drop, delayed legal drop (Stalled), capacity — red
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Failed: 7
  Failed Goblins.Sim.Tests.TheftTests.A_destination_painted_as_a_Pile_after_the_pickup_puts_the_unit_back_on_the_Pile_it_stands_on_for_no_relief 
  Failed Goblins.Sim.Tests.TheftTests.A_destination_that_becomes_unreachable_while_carrying_puts_the_unit_down_legally_and_ends_without_relief 
  Failed Goblins.Sim.Tests.TheftTests.A_spot_that_becomes_a_Pile_under_a_thief_who_is_eating_ends_the_theft_and_stores_the_unit_there_with_no_relief 
  Failed Goblins.Sim.Tests.TheftTests.A_stalled_thief_puts_the_unit_down_the_moment_a_legal_place_opens_and_only_then_starts_its_cooldown 
  Failed Goblins.Sim.Tests.TheftTests.A_stalled_thief_stores_the_unit_when_a_Pile_gains_room_and_never_overfills_it 
  Failed Goblins.Sim.Tests.TheftTests.A_unit_put_down_away_from_a_Pile_lies_loose_for_ordinary_hauling_and_eating 
  Failed Goblins.Sim.Tests.TheftTests.With_no_legal_place_to_put_the_unit_down_a_failed_thief_keeps_it_and_waits_without_consuming_or_relief 
     Passed: 40
Total tests: 47
```

### S4 changed plans after the pickup: legal drop, delayed legal drop (Stalled), capacity — red
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"` (before `FailTheft` dropped a carried unit and `Stall` existed)
```
     Failed: 7
  Failed A_destination_painted_as_a_Pile_after_the_pickup_puts_the_unit_back_on_the_Pile_it_stands_on_for_no_relief
  Failed A_destination_that_becomes_unreachable_while_carrying_puts_the_unit_down_legally_and_ends_without_relief
  Failed A_spot_that_becomes_a_Pile_under_a_thief_who_is_eating_ends_the_theft_and_stores_the_unit_there_with_no_relief
  Failed A_stalled_thief_puts_the_unit_down_the_moment_a_legal_place_opens_and_only_then_starts_its_cooldown
  Failed A_stalled_thief_stores_the_unit_when_a_Pile_gains_room_and_never_overfills_it
  Failed A_unit_put_down_away_from_a_Pile_lies_loose_for_ordinary_hauling_and_eating
  Failed With_no_legal_place_to_put_the_unit_down_a_failed_thief_keeps_it_and_waits_without_consuming_or_relief
     Passed: 40
Total tests: 47
```

### S4 changed plans after the pickup — green
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Passed: 47
Total tests: 47
```

### S5 Need interruption at every stage (set-ups corrected after a first run with test-side mistakes) — PASSED, not red
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Passed: 70
Total tests: 70
```

### S5 Need interruption at every stage — passed on first run (disclosed)
The interruption cooldown for theft stages (`BeginNeedTask` starting the cooldown when it ends a theft Task) was written together with the S1 start-of-theft code, before any test pinned it. The S5 tests therefore passed on their first run: 70 of 70 (new tests: Craving × 5 stages, Collapse × 5 stages, last-tick Sleep/Grub/Grog, never-resumes, unsatisfiable Craving, Stalled + Craving, Craving-before-theft, protected sleep, no second act / not a fight target × 5 stages). To show they are not vacuous, a mutation check follows: the one line is removed and the tests must go red.

#### Mutation check — remove `|| IsTheftTask(before)` from `BeginNeedTask`

### S5 mutation: no cooldown when a Need interrupts a theft — red
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Failed: 29
  Failed Goblins.Sim.Tests.TheftTests.A_destination_painted_as_a_Pile_after_the_pickup_puts_the_unit_back_on_the_Pile_it_stands_on_for_no_relief 
  Failed Goblins.Sim.Tests.TheftTests.A_destination_painted_as_a_Pile_before_the_pickup_fails_the_attempt_and_no_replacement_is_chosen(stage: Pilfering) 
  Failed Goblins.Sim.Tests.TheftTests.A_destination_painted_as_a_Pile_before_the_pickup_fails_the_attempt_and_no_replacement_is_chosen(stage: Sneaking) 
  Failed Goblins.Sim.Tests.TheftTests.A_destination_that_becomes_unreachable_while_carrying_puts_the_unit_down_legally_and_ends_without_relief 
  Failed Goblins.Sim.Tests.TheftTests.A_destination_that_can_no_longer_be_reached_fails_the_attempt_before_the_pickup 
  Failed Goblins.Sim.Tests.TheftTests.A_hungry_goblin_who_eats_the_last_unit_on_the_Pile_first_leaves_the_thief_to_fail_at_the_pickup 
  Failed Goblins.Sim.Tests.TheftTests.A_Need_that_arises_on_the_last_tick_of_private_consumption_interrupts_it_with_no_relief(need: Grog, lastTick: 70) 
  Failed Goblins.Sim.Tests.TheftTests.A_Need_that_arises_on_the_last_tick_of_private_consumption_interrupts_it_with_no_relief(need: Grub, lastTick: 50) 
  Failed Goblins.Sim.Tests.TheftTests.A_Need_that_arises_on_the_last_tick_of_private_consumption_interrupts_it_with_no_relief(need: Sleep, lastTick: 45) 
  Failed Goblins.Sim.Tests.TheftTests.A_satisfiable_Craving_ends_the_theft_at_any_stage_for_no_relief_with_the_unit_put_down_and_the_cooldown_started(stage: MakingOff) 
  Failed Goblins.Sim.Tests.TheftTests.A_satisfiable_Craving_ends_the_theft_at_any_stage_for_no_relief_with_the_unit_put_down_and_the_cooldown_started(stage: Pilfering) 
  Failed Goblins.Sim.Tests.TheftTests.A_satisfiable_Craving_ends_the_theft_at_any_stage_for_no_relief_with_the_unit_put_down_and_the_cooldown_started(stage: Scoffing) 
  Failed Goblins.Sim.Tests.TheftTests.A_satisfiable_Craving_ends_the_theft_at_any_stage_for_no_relief_with_the_unit_put_down_and_the_cooldown_started(stage: Sneaking) 
  Failed Goblins.Sim.Tests.TheftTests.A_satisfiable_Craving_ends_the_theft_at_any_stage_for_no_relief_with_the_unit_put_down_and_the_cooldown_started(stage: Stalled) 
  Failed Goblins.Sim.Tests.TheftTests.A_source_that_stops_being_a_Pile_before_the_pickup_fails_the_attempt_and_leaves_the_stock_where_it_lies(stage: Pilfering) 
  Failed Goblins.Sim.Tests.TheftTests.A_source_that_stops_being_a_Pile_before_the_pickup_fails_the_attempt_and_leaves_the_stock_where_it_lies(stage: Sneaking) 
  Failed Goblins.Sim.Tests.TheftTests.A_spot_that_becomes_a_Pile_under_a_thief_who_is_eating_ends_the_theft_and_stores_the_unit_there_with_no_relief 
  Failed Goblins.Sim.Tests.TheftTests.A_stalled_thief_puts_the_unit_down_the_moment_a_legal_place_opens_and_only_then_starts_its_cooldown 
  Failed Goblins.Sim.Tests.TheftTests.A_stalled_thief_stores_the_unit_when_a_Pile_gains_room_and_never_overfills_it 
  Failed Goblins.Sim.Tests.TheftTests.A_stalled_thief_with_a_Craving_stays_put_until_a_place_opens_then_puts_the_unit_down_and_goes_to_meet_it 
  Failed Goblins.Sim.Tests.TheftTests.A_unit_put_down_away_from_a_Pile_lies_loose_for_ordinary_hauling_and_eating 
  Failed Goblins.Sim.Tests.TheftTests.Collapse_ends_the_theft_at_any_stage_for_no_relief(stage: MakingOff) 
  Failed Goblins.Sim.Tests.TheftTests.Collapse_ends_the_theft_at_any_stage_for_no_relief(stage: Pilfering) 
  Failed Goblins.Sim.Tests.TheftTests.Collapse_ends_the_theft_at_any_stage_for_no_relief(stage: Scoffing) 
  Failed Goblins.Sim.Tests.TheftTests.Collapse_ends_the_theft_at_any_stage_for_no_relief(stage: Sneaking) 
  Failed Goblins.Sim.Tests.TheftTests.Collapse_ends_the_theft_at_any_stage_for_no_relief(stage: Stalled) 
  Failed Goblins.Sim.Tests.TheftTests.Losing_the_selected_unit_before_the_pickup_fails_the_attempt_and_moves_nothing(stage: Pilfering) 
  Failed Goblins.Sim.Tests.TheftTests.Losing_the_selected_unit_before_the_pickup_fails_the_attempt_and_moves_nothing(stage: Sneaking) 
  Failed Goblins.Sim.Tests.TheftTests.Two_thieves_after_the_last_unit_leave_it_to_the_one_who_picks_it_up_first_and_the_other_fails_with_nothing_moved 
     Passed: 41
Total tests: 70
```
(29 of 70 fail with the line removed — S5 plus every S3/S4 case, since `EndTheft` reuses it. Line restored:)

### S5 mutation reverted — green
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Passed: 70
Total tests: 70
```

### S6a Chaos does not count the Need being privately restored — red
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Failed: 1
  Failed Goblins.Sim.Tests.TheftTests.The_Need_a_thief_is_privately_restoring_adds_no_Chaos_while_it_is_consumed_but_any_other_low_Need_does 
     Passed: 70
Total tests: 71
```

### S6a Chaos does not count the Need being privately restored — green
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Passed: 71
Total tests: 71
```

### S6b theft in the whole-state save: continue at every stage, invalid saves rejected — red
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Failed: 8
  Failed Goblins.Sim.Tests.TheftTests.A_save_taken_mid_pickup_finishes_the_pickup_once_with_the_event_after_loading 
  Failed Goblins.Sim.Tests.TheftTests.A_save_with_a_MakingOff_goblin_that_breaks_the_rules_of_the_stage_is_rejected 
  Failed Goblins.Sim.Tests.TheftTests.A_save_with_a_Pilfering_goblin_that_breaks_the_rules_of_the_stage_is_rejected 
  Failed Goblins.Sim.Tests.TheftTests.A_save_with_a_Scoffing_goblin_that_breaks_the_rules_of_the_stage_is_rejected 
  Failed Goblins.Sim.Tests.TheftTests.A_save_with_a_Sneaking_goblin_that_breaks_the_rules_of_the_stage_is_rejected 
  Failed Goblins.Sim.Tests.TheftTests.A_save_with_a_Stalled_goblin_that_breaks_the_rules_of_the_stage_is_rejected 
  Failed Goblins.Sim.Tests.TheftTests.A_save_with_a_theft_plan_on_a_goblin_that_is_not_stealing_is_rejected 
  Failed Goblins.Sim.Tests.TheftTests.A_save_without_the_theft_arrays_or_with_the_wrong_number_of_values_is_rejected 
     Passed: 76
Total tests: 84
```

### S6b theft in the whole-state save: continue at every stage, invalid saves rejected — green
`dotnet test sim/tests --filter "FullyQualifiedName~TheftTests"`
```
     Passed: 84
Total tests: 84
```

### S7 architecture scan covers the theft arrays — red
`dotnet test sim/tests --filter "FullyQualifiedName~ArchitectureTests"`
```
     Failed: 3
  Failed Goblins.Sim.Tests.ArchitectureTests.The_scan_flags_a_stray_write_outside_GoblinTasks(line: "s.GoblinTheftDestination[g] = -1;") [< 1 ms]
  Failed Goblins.Sim.Tests.ArchitectureTests.The_scan_flags_a_stray_write_outside_GoblinTasks(line: "s.GoblinTheftSupply[g] = (byte)Material.Grub;") [< 1 ms]
  Failed Goblins.Sim.Tests.ArchitectureTests.The_scan_flags_a_stray_write_outside_GoblinTasks(line: "s.GoblinTheftSupply = new byte[30];") 
     Passed: 21
Total tests: 24
```

### S7 architecture scan covers the theft arrays — green
`dotnet test sim/tests --filter "FullyQualifiedName~ArchitectureTests"`
```
     Passed: 24
Total tests: 24
```

### S8 replay with shirk + fight + theft — green
`dotnet test sim/tests --filter "FullyQualifiedName~The_same_seed_and_commands_replay"`
```
     Passed: 2
Total tests: 2
```

### S8 replay with shirk + fight + theft — characterization, passed on first run (disclosed)
`The_same_seed_and_commands_replay_the_same_with_shirking_fights_and_theft_all_happening_and_a_mid_run_save_agrees` adds no behaviour; it pins that two runs of one seed + command log (3000 ticks, 7 goblins near 70 Chaos, supplies on a Pile) agree to the last byte, that the run really contains ≥1 `ShirkStarted`, `ScuffleStarted` and `TheftPickedUp`, that another seed differs, and that a save taken at tick 1200 continues to the same bytes.

### B1 bridge: Inspector reading, map cue, Warren summary — red
The bridge checks were written first (`SelectionInputChecks` theft block: every stage names the supply, `Stealing` flag, thief counted idle, unit counted loose; `CarryDrawChecks` `ThiefLayoutCheck` + `ThiefMarkCheck`: ring + mask in the thief colour, carried unit and Need pip still visible, colour told apart from shirk/fight). Against the unchanged bridge the Godot C# build fails on the missing members:
```
$ dotnet build game
tests/CarryDrawChecks.cs(413,25): error CS0117: 'WarrenView' does not contain a definition for 'ThiefMaskRect'
tests/CarryDrawChecks.cs(418,32): error CS0117: 'WarrenView' does not contain a definition for 'ThiefColor'
tests/SelectionInputChecks.cs(662,21): error CS1061: 'GoblinReading' does not contain a definition for 'Stealing'
```

### B1 bridge — green
After `GoblinReading.Stealing`, the theft `TaskWords`/mood lines, `WarrenView.ThiefColor`/`ThiefMaskRect` + ring and mask drawing, the Warren summary counting thieves idle, and a Colour key row:
```
$ godot-mono --headless --path game res://tests/SelectionInputChecks.tscn
Selection input checks passed (42 scenarios, count labels, pile counts).        (was 41)
$ godot-mono --headless --path game res://tests/InspectorLayoutChecks.tscn
Inspector layout checks passed (2 viewports, seven goblins on one Heap; ...)
$ godot-mono --path game res://tests/CarryDrawChecks.tscn                       (real renderer)
Carry draw checks passed (layout, colours, count order, 13 pixel scenarios).    (was 12)
```
