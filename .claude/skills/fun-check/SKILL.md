---
name: fun-check
description: Prepare and log the fun check for A Thousand Goblins Deep. Load before a milestone playtest (P1–P6), when a milestone is finished, or when Theo says he just played a build.
---

# Fun check

The prototype exists to answer **is it fun?** This skill turns a playtest into a logged verdict.

## Steps

1. Read the current milestone's decisions and acceptance rules in `docs/design/GDD.md`. Prepare a short behaviour checklist, normally 6–10 concrete checks: new milestone behaviours plus regressions covering earlier gameplay loops. Follow the current cumulative design; later decisions supersede earlier ones. For P4, waiting-carrier Chaos replaces P3's doubled Grog drain. Show the checklist before play; if Theo has already played, use it to review what he observed.
2. Record each check as **observed / failed / not seen**, with brief evidence or reproduction notes. Unexercised behaviours are **not seen**. Checklist results supplement the subjective fun questions.
3. Ask Theo the five questions from `docs/design/GDD.md` → *Fun tests*, one at a time. Record each answer as **yes / no / unsure** plus his own words.
4. Ask for the one moment he'd tell a friend about. No moment is a valid answer and a strong signal; record the specific incident when there is one.
5. Write `docs/playtests/YYYY-MM-DD-P<n>.md` using `docs/playtests/TEMPLATE.md`, replacing its sample checklist rows with the checks prepared for this run.
6. Give the verdict. Apply the milestone's acceptance rules first; for P4, both questions 2 and 3 must be yes before advancing to P5, otherwise **Tune**. Use these general rules where the milestone does not override them:
   - **Go**: 3+ clear yes.
   - **Tune**: questions 2 and 3 are no. The emergence is flat; propose one change to chaos, quirks, or needs, not new content.
   - **Rethink**: 2 or fewer yes after P6. Summarise the evidence plainly for a keep-or-kill decision.

## Done when

The playtest file exists, every checklist row has an outcome, every fun question has an answer, and the verdict applies the milestone's acceptance rules and names one next action. Identify failed and unobserved checks separately from the fun verdict.
