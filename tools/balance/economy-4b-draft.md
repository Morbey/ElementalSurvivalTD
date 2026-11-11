# 0001 – Prototype economy (step 4b, Light and Shadow)

Draft: replaces `docs/balance/0001-prototype-economy.md` when step 4b is built. Provisional until playtests. Simulated on a float32 port of Core:
- It reproduces the 3c pins that the real Core confirmed (empty route 8865, a 652-tick crossing; on the 3c numbers building nothing loses on tick 2931 and the 3c reference order wins on tick 10136 with 10 lives, 45 gold and 20 towers) and follows the rules for the port of plan 0007, 1 to 10.
- Rule 5 pin: building nothing, wave 1 pays its bonus on tick 1672 and wave 2 on tick 2332, and the run is lost on tick 2931. Rule 10 pin: a Shadow tower takes a Light enemy of 45 health to 25, 5 and then kills it on the third hit; a Light tower leaves it at 45 and has fired.

The gold rules are those of plan 0005: gold is checked before the board, a kill pays on the tick its shot lands, "fired" means a launch (also at an immune enemy), and the refund is `cost * percent / 100` in integers.

No neutral content is left, so every damaging hit is x2: a tower deals 20 to the other element and 0 to its own. Health is sized for that: against its wave only the towers of the other element do damage, at twice the 3c damage.

## Values

Changed from step 4a: the elements, both towers, the chart, the bonus per wave and the health of waves 3 to 10.

| Value | Where | Number | Confidence |
|---|---|---|---|
| Chart | `ElementChart.asset` | Light against Shadow x2, Shadow against Light x2; own element 0 by rule | high (Eduardo's rule) |
| Towers | `Tower_Light`, `Tower_Shadow` | cost 40, range 2.5, damage 10, 1.5 shots/s, projectile 10; they differ only in element | medium |
| Wave bonus | every wave of `Run_Prototype` | 40, then 20 rising by 5 a wave to 60 (400 per run, about a third of a winning run's income) | low |
| Starting gold | `Run_Prototype.startingGold` | 115 (buys exactly 2 towers, leaves 35; the 3rd is `NotEnoughGold`) | medium |
| Gold per kill | `goldReward` on every enemy asset | 10 | medium |
| Sell refund after the first launch | `Run_Prototype.sellRefundPercent` | 50 (20 gold) | low |
| Lives | `Run_Prototype.lives` | 10 | medium |
| Enemy speed | all enemies | 2 | – |

| Wave | Element | Count | Interval s | Delay s | Enemy health | Bonus | Enemy asset |
|---|---|---|---|---|---|---|---|
| 1 | Shadow | 3 | 1 | 15 | 45 | 40 | `Enemy_Shadow45` |
| 2 | Light | 4 | 1 | 8 | 55 | 20 | `Enemy_Light55` |
| 3 | Light | 5 | 1 | 8 | 70 | 25 | `Enemy_Light70` |
| 4 | Shadow | 6 | 1 | 8 | 85 | 30 | `Enemy_Shadow85` |
| 5 | Light | 7 | 1 | 8 | 105 | 35 | `Enemy_Light105` |
| 6 | Shadow | 8 | 1 | 8 | 135 | 40 | `Enemy_Shadow135` |
| 7 | Shadow | 9 | 1 | 8 | 180 | 45 | `Enemy_Shadow180` |
| 8 | Light | 10 | 1 | 8 | 245 | 50 | `Enemy_Light245` |
| 9 | Shadow | 11 | 1 | 8 | 345 | 55 | `Enemy_Shadow345` |
| 10 | Shadow | 12 | 1 | 8 | 460 | 60 | `Enemy_Shadow460` |

Every enemy asset has speed 2 and reward 10. Assets: the ten above; today's eleven go, renamed where the plan allows (`Enemy_Health45` to `Enemy_Shadow45`, `Enemy_Health55` to `Enemy_Light55`; the others change health too), and `Enemy_Prototype` and `Tower_Prototype` are gone.

**The element order** `S L L S L S S L S S` has runs of at most two, so no element sits idle for more than about 45 s (Shadow towers from wave 9 to the end). It is not an alternation: three announcements say "same element again" (waves 3, 7, 10), so gold spent on the other element is wasted. The hard end is lopsided, four Shadow waves against one Light wave in waves 6 to 10, so a fixed half-and-half build is short of Light towers when the health peaks. Wave 8 is the one Light wave between Shadow waves: its towers are dead weight afterwards, and how much to commit to it is the run's real decision. Strict alternation is answered by half-and-half almost as well as by reading (see Sensitivities).

**The bonus.** The 40 of wave 1 pays for the first tower against wave 2, the first change of element: with 20 there, the reading bots lose the run at 20% less or more starting gold, because a counter tower arrives one wave late and waves 2 and 3 leak. From wave 2 on, the bonus grows with the waves, because the late waves are where a leak is fatal. The flat 10 of step 4a is not enough here: with it the sloppy human wins 5 runs in 40, because a wave of the wrong element leaks whole and kills pay nothing.

## Strategies

Bots act every 0.5 s unless stated. Cells:
- **Sensible**: each tower on the free cell off the route that adds the most path in range.
- **Maze**: an expert order of 32 cells found offline by a two-step lookahead on path in range, with route changes allowed.
- **Sloppy human**: acts every 2 s and takes a seeded-random cell among the 6 best sensible ones.

Elements:
- **Reads**: buys the tower that damages the element with the most enemy health per damaging tower, counting the enemies on the board and the announced wave; on a tie, the announced wave. (Buying the opposite of the announced wave gives the same purchases on this table.)
- **Half and half**: ignores the announcement and buys whichever element it has fewer of.
- **Random**: a seeded coin per purchase.

End ticks count `Tick()` calls, including the one that ends the run. Gold includes the bonuses. Wasted shots are launches at an immune enemy, as a share of all launches; none of them had a damageable enemy in range, so skipping the own element would have saved none.

| Strategy | Outcome | End | Lives left | Kills | Towers Light/Shadow at end | Bonus | Gold at 30/60/90/120/150 s | Gold at end | Wasted shots |
|---|---|---|---|---|---|---|---|---|---|
| Builds nothing | Lost | tick 2931, 48.9 s | 0 | 0 | 0/0 | 60 | 155/–/–/–/– | 175 | – |
| Reads, sensible | Won | tick 9708, 161.8 s | 10 | 75 | 19/10 | 400 | 25/0/35/30/25 | 105 | 42% |
| Reads, maze | Won | tick 10119, 168.7 s | 10 | 75 | 18/11 | 400 | 25/30/35/0/15 | 105 | 44% |
| Reads, sells 2 Light and rebuilds Shadow when wave 8 enters | Won | tick 9838, 164.0 s | 10 | 75 | 17/11 | 400 | 25/0/35/30/25 | 105 | 44% |
| Reads, sells 2 Shadow and rebuilds Light when wave 9 enters | Won | tick 9688, 161.5 s | 10 | 75 | 20/8 | 400 | 25/0/35/30/25 | 105 | 42% |
| Light only | Lost | tick 4399, 73.3 s | 0 | 9 | 8/0 | 115 | 25/30/–/–/– | 0 | 80% |
| Shadow only | Lost | tick 5239, 87.3 s | 0 | 16 | 0/10 | 150 | 35/10/–/–/– | 25 | 74% |
| Half and half, Light first | Won | tick 9912, 165.2 s | 9 | 74 | 15/14 | 400 | 25/30/35/30/25 | 95 | 50% |
| Half and half, Shadow first | Won | tick 9912, 165.2 s | 8 | 73 | 14/15 | 400 | 25/10/35/30/25 | 85 | 50% |
| Half and half, maze (either first) | Won | 171.8 / 173.4 s | 10 | 75 | 15/14, 14/15 | 400 | – | 105 | 47%, 51% |
| Random elements, sensible (40 seeds) | 20 won | 73.3–165.2 s | median 2 (0–10) | – | – | – | – | – | 51% |
| Sloppy human, reads (40 seeds) | 40 won | 160.2–162.3 s | median 10 (6–10) | – | – | 400 | – | – | 42% |
| Sloppy human, half and half (40 seeds) | 38 won | 163.1–165.5 s | median 6 (0–10) | – | – | – | – | – | 49% |
| Sloppy human, random elements (40 seeds) | 16 won | 72.7–165.5 s | median 0 (0–10) | – | – | – | – | – | 52% |
| Late: first tower at wave 2, reads | Won | tick 9721, 162.0 s | 2 | 67 | 17/10 | 400 | 35/20/35/20/35 | 105 | 41% |
| Late: first tower at wave 3, reads | Lost | tick 3026, 50.4 s | 0 | 1 | 3/1 | 60 | 155/–/–/–/– | 25 | 77% |
| Saver: 2 at start, then batches of 3, reads | Won | tick 9674, 161.2 s | 5 | 70 | 18/8 | 400 | 105/0/35/90/15 | 175 | 40% |

Both elements are on the board in 3% of the ticks that have an enemy (sloppy human reading), because a wave dies near the start of the route before the next one arrives.

**Capacity** is the highest health a fixed build of towers of the other element kills with no leak in a wave of 10:

| Towers | 4 | 6 | 8 | 10 | 12 | 16 | 20 |
|---|---|---|---|---|---|---|---|
| Sensible | 117 | 198 | 277 | 319 | 378 | 519 | 640 |
| Maze | 117 | 198 | 257 | 319 | 398 | 558 | 718 |

## Reference build order (wins)

Each action runs after the given number of `Tick()` calls, before the next one, with the tower selected before the click. Every action is accepted, the four-cell occupancy rule included.

**Outcome:** Won on the 10165th `Tick()` (169.4 s), with 10 lives, 105 gold left and 28 towers (16 Light, 12 Shadow); the last wave's bonus is paid on that tick. The first kill pays 10 on tick 1288. 44% of its launches are wasted on immune enemies, none with a damageable enemy in range.

**Robustness:** with every action after tick 0 shifted 1 to 30 ticks later, no action is refused and the run still wins with 10 lives, 105 gold and 16/12 towers. It ends on tick 10147 or 10165.

| Tick | Cell | Tower | Action | Gold change | Gold after |
|---|---|---|---|---|---|
| 0 | (0,8) | `Tower_Light` | buy | −40 | 75 |
| 0 | (0,8) | `Tower_Light` | sell, never fired | +40 | 115 |
| 0 | (11,2) | `Tower_Light` | buy | −40 | 75 |
| 0 | (11,3) | `Tower_Light` | buy | −40 | 35 |
| 1290 | (14,2) | `Tower_Shadow` | buy | −40 | 5 |
| 1470 | (13,2) | `Tower_Shadow` | buy | −40 | 25 |
| 2130 | (12,0) | `Tower_Shadow` | buy | −40 | 5 |
| 2220 | (10,1) | `Tower_Light` | buy | −40 | 5 |
| 3360 | (4,6) | `Tower_Shadow` | buy | −40 | 50 |
| 3390 | (4,7) | `Tower_Shadow` | buy | −40 | 20 |
| 3630 | (3,7) | `Tower_Shadow` | buy | −40 | 0 |
| 3780 | (6,8) | `Tower_Light` | buy | −40 | 10 |
| 3960 | (0,6) | `Tower_Light` | buy | −40 | 0 |
| 4290 | (1,6) | `Tower_Light` | buy | −40 | 35 |
| 4650 | (3,4) | `Tower_Light` | buy | −40 | 5 |
| 5190 | (2,5) | `Tower_Light` | buy | −40 | 5 |
| 5970 | (1,3) | `Tower_Shadow` | buy | −40 | 85 |
| 5970 | (1,2) | `Tower_Shadow` | buy | −40 | 45 |
| 6030 | (3,3) | `Tower_Shadow` | buy | −40 | 15 |
| 6360 | (3,2) | `Tower_Shadow` | buy | −40 | 50 |
| 6360 | (6,5) | `Tower_Shadow` | buy | −40 | 10 |
| 6360 | (11,2) | `Tower_Light` | sell, fired | +20 | 30 |
| 6360 | (11,3) | `Tower_Light` | sell, fired | +20 | 50 |
| 6360 | (7,7) | `Tower_Shadow` | buy | −40 | 10 |
| 6810 | (7,5) | `Tower_Light` | buy | −40 | 0 |
| 7200 | (8,3) | `Tower_Light` | buy | −40 | 0 |
| 7410 | (9,2) | `Tower_Light` | buy | −40 | 40 |
| 7410 | (8,5) | `Tower_Light` | buy | −40 | 0 |
| 8340 | (7,3) | `Tower_Light` | buy | −40 | 0 |
| 8700 | (7,1) | `Tower_Light` | buy | −40 | 0 |
| 8940 | (6,1) | `Tower_Light` | buy | −40 | 55 |
| 8940 | (9,1) | `Tower_Light` | buy | −40 | 15 |
| 9510 | (0,3) | `Tower_Light` | buy | −40 | 5 |
| 9900 | (3,1) | `Tower_Light` | buy | −40 | 5 |

On tick 6360, as wave 8 (Light) enters, it sells its two oldest Light towers and spends the refund on Shadow.

**Building nothing:** wave 1 pays 40 on tick 1672 and wave 2 pays 20 on tick 2332, both fully leaked; the run is lost on the 2931st `Tick()` (48.9 s) with 175 gold.

## Sensitivities

Strategy cells show the outcome and lives left; human cells show wins in 40, then the median lives (range).

| Change | Reads, sensible | Reads, maze | Reads, swaps at wave 8 | Reference order | Half and half, Light / Shadow first | Sloppy humans reading | Sloppy half and half | Random elements |
|---|---|---|---|---|---|---|---|---|
| Proposal | Won, 10 | Won, 10 | Won, 10 | Won, 10 | Won 9 / Won 8 | 40, 10 (6–10) | 38, 6 (0–10) | 20, 2 (0–10) |
| **Bonus 0** | Lost | Lost | Lost | Lost | Lost / Lost | 0 | 0 | 0 |
| Bonus of step 4a (10 per wave) | Won, 1 | Won, 4 | Lost | Lost | Lost / Lost | 5, 0 (0–8) | 0 | 0 |
| Gold per kill 9 (−10%) | Won, 10 | Won, 10 | Won, 10 | Lost | Won 4 / Won 2 | 40, 10 (4–10) | 25, 1 (0–7) | 14, 0 (0–10) |
| Gold per kill 8 (−20%) | Won, 5 | Won, 5 | Won, 10 | Lost | Lost / Lost | 40, 8 (3–10) | 10, 0 (0–4) | 7, 0 (0–3) |
| Gold per kill 8, **bonus 0** | Lost | Lost | Lost | Lost | Lost / Lost | 0 | 0 | 0 |
| Gold per kill 0 (bonus only) | Lost | Lost | Lost | Lost | Lost / Lost | 0 | 0 | 0 |
| Gold per kill 12 (+20%) | Won, 10 | Won, 10 | Won, 10 | Won, 10 | Won 10 / Won 10 | 40, 10 (5–10) | 40, 10 (4–10) | 25, 6 (0–10) |
| Health +20% in waves 1–3 | Won, 10 | Won, 3 | Won, 10 | Won, 8 | Won 9 / Won 8 | 40, 10 (1–10) | 38, 6 (0–10) | 17, 0 (0–10) |
| Health +10% in waves 7–10 | Won, 10 | Won, 10 | Won, 9 | Lost | Won 3 / Won 2 | 40, 8 (3–10) | 23, 1 (0–4) | 10, 0 (0–8) |
| Health +20% in waves 7–10 | Won, 10 | Won, 9 | Lost | Lost | Lost / Lost | 40, 5 (3–10) | 1, 0 (0–2) | 2, 0 (0–2) |
| Health +20% in waves 7–10, **bonus 0** | Lost | Lost | Lost | Lost | Lost / Lost | 0 | 0 | 0 |
| Health −20% in waves 7–10 | Won, 10 | Won, 10 | Won, 10 | Won, 10 | Won 10 / Won 10 | 40, 10 | 40, 10 (8–10) | 34, 10 (0–10) |
| Cost 48 (+20%) | Won, 5 | Won, 3 | Won, 6 | Lost | Lost / Lost | 37, 4 (0–10) | 0 | 1, 0 (0–2) |
| Cost 32 (−20%; the start buys 3) | Won, 10 | Won, 10 | Won, 10 | Won, 10 | Won 10 / Won 10 | 40, 10 | 40, 10 (8–10) | 35, 10 (0–10) |
| Starting gold 92 (−20%) | Won, 5 | Won, 6 | Won, 10 | Won, 8 | Won 8 / Won 6 | 40, 9 (4–10) | 39, 4 (0–9) | 15, 0 (0–10) |
| Starting gold 138 (+20%; buys 3) | Won, 10 | Won, 9 | Won, 10 | Won, 10 | Won 10 / Won 8 | 40, 10 (4–10) | 40, 6 (1–10) | 20, 1 (0–10) |
| Refund 40% / 60% | Won, 10 | Won, 10 | Won, 10 | Won, 2 / 10 | Won 9 / Won 8 | 40, 10 (6–10) | 38, 6 (0–10) | 20, 2 (0–10) |
| Strict alternation, Shadow first | Won, 10 | Won, 10 | Won, 4 | Lost | Won 8 / Won 9 | 40, 8 (3–10) | 39, 6 (0–10) | 25, 3 (0–10) |
| Strict alternation, Light first | Won, 10 | Won, 10 | Won, 4 | Lost | Won 9 / Won 8 | 40, 8 (3–10) | 40, 5 (1–9) | 27, 2 (0–10) |
| Mirrored order (Light and Shadow swapped) | Won, 10 | Won, 10 | Won, 10 | Lost | Won 8 / Won 9 | 40, 10 (6–10) | 39, 6 (0–10) | 27, 4 (0–10) |
| Delay 12 s before a wave of the other element | Won, 10 | Won, 10 | Won, 10 | Won, 6 | Won 9 / Won 8 | 40, 10 | 39, 6 (0–10) | 20, 1 (0–10) |
| Delay 5 s between all waves | Won, 4 | Won, 10 | Won, 10 | Lost | Won 6 / Won 9 | 40, 10 (3–10) | 36, 5 (0–9) | 20, 1 (0–10) |

Building nothing loses on tick 2931 in every row but the two delay rows (3172 and 2572). The reference order is a fixed timetable, not a player: it loses wherever a purchase is no longer affordable on its tick, or, mirrored, where its towers have the wrong element.

**Reading pays under pressure.** At the proposal a sloppy human who reads keeps a median of 10 lives, one who buys half and half 6, one who buys at random wins half the runs; building one element always loses. At 20% less gold per kill or 20% more late health the reader still wins all 40 runs (median 8 and 5), while half and half wins 10 and 1. Under strict alternation reading loses 2 lives of median and half and half keeps up (median 5 to 6), so the chosen order is what makes the announcement matter.

Breaks first:
1. Tower cost +20%: the reader wins 37 in 40 with a median of 4 lives; half and half never wins.
2. Health in waves 7 to 10: +20% leaves the reader a median of 5 lives; the swap at wave 8 then no longer saves enough.

## Open design points (plan 0007)

**(a) Wasted shots and the gap between waves.** A reading build wastes 42% of its launches (the sloppy human; 44% for the reference), half and half 49%, random 51%. Almost none are recoverable: in every proposal run no tower ever launched at an immune enemy while one it could damage was in its range, and both elements share the board in 3% of the busy ticks, not "most of the time", because a wave dies near the spawn before the next arrives 8 s later. A 12 s delay before a change of element takes that to 0% and changes no outcome; a 5 s delay takes it to 13% and still changes little. **No longer gap is needed.** Skipping the own element, the future talent, would be worth almost nothing on these waves; its value comes with mixed waves.

**(b) One wave announced versus more.** With these numbers the lookup is enough: the sensible and maze readers win with 10 lives. Bots that plan on more of the future do worse: aiming for counters in proportion to the next 2 waves' health drops the sloppy human from a median of 10 to 6 lives (40/40 to 40/40 wins; at 20% less gold, 40 to 39 wins and median 8 to 5); planning on 3 waves wins 9 in 40, and knowing the whole order (a learned order) wins none, because those bots tilt early and starve the wave in front. That is a limit of the bots more than a property of the game, but no simulated use of more information helped. **Recommendation: keep one wave announced and a fixed order** (it is already the same every run, so it can be learned for free). The decision content of this slice is how much to commit to a lone wave such as wave 8: the swap there turns 5 lost lives into none at 20% less gold. A real decision comes with mixed waves and damage numbers, not with a longer announcement.

**Risks.** The proposal is easy for a reader (median 10 lives); a run that costs a reader a few lives and still survives +20% late health could not be found, as in step 4a. The early game is the fragile part: the first change of element, at wave 2, decides runs if the counter tower comes late, which is why wave 1 pays 40. Tune first in play: the late health (waves 7 to 10), then the wave 1 bonus, then the order. Every number is low to medium confidence.

Simulation: `tools/balance/` (`verify3c.py` checks the 3c pins, `core.py` is the port, `proposal.py` holds these numbers, `report4b.py main|sens|ahead` prints every table, `refrepair.py 8 2` builds and checks the reference order, `capacity4.py` the capacity table).
