# 0001 – Prototype economy (slice 3c, eight-direction movement)

Provisional until playtests. Simulated on a float32 port of Core:
- **Before 3c**, its four-direction version reproduced the 3a pins (wave entry ticks, last entry on tick 9120) and the 3b runs, which the real Core then matched (building nothing lost on tick 3089; the 3b reference won on tick 9577).
- **With the movement of plan 0006**, it reproduces 3c-AC4: the empty route of 3c-AC1 is 9 straight and 9 diagonal steps, distance 8865 (21.728 cells), and an enemy of speed 2 leaves on its 652nd tick. It also gives 9442 at the spawn with a tower on (5,7), passes the 3c-AC1 to AC3 cases of the plan, and matches the tech-lead's `core8.py` tick by tick on 12 seeded random builds.
- The real 3c Core confirms the spawn distance 8865, the 652-tick crossing and, on the 3b numbers, the loss on tick 2931 when building nothing. The runs below have not yet been replayed on it.

The gold rules are those of plan 0005: gold is checked before the board, a kill pays on the tick its shot lands, "fired" means a launch, and the refund is `cost * percent / 100` in integers.

## Values

| Value | Where | Number | Confidence |
|---|---|---|---|
| Starting gold | `Run_Prototype.startingGold` | 115 (buys exactly 2 towers, leaves 35; the 3rd is `NotEnoughGold`) | medium |
| Tower cost | `Tower_Prototype.cost` | 40 | medium |
| Gold per kill | `goldReward` on every enemy asset | 10 (4 kills per tower) | medium |
| Sell refund after the first launch | `Run_Prototype.sellRefundPercent` | 50 (20 gold) | low |
| Lives | `Run_Prototype.lives` | 10 | medium |
| Tower combat stats, enemy speed | `Tower_Prototype`, all enemies | range 2.5, damage 10, 1.5 shots/s, projectile 10; speed 2 (2c-AC9 holds) | – |

| Wave | Count | Interval s | Delay s | Enemy health | Enemy asset |
|---|---|---|---|---|---|
| 1 | 3 | 1 | 15 | 45 | `Enemy_Health45` |
| 2 | 4 | 1 | 8 | 55 | `Enemy_Health55` |
| 3 | 5 | 1 | 8 | 65 | `Enemy_Health65` |
| 4 | 6 | 1 | 8 | 75 | `Enemy_Health75` |
| 5 | 7 | 1 | 8 | 90 | `Enemy_Health90` |
| 6 | 8 | 1 | 8 | 100 | `Enemy_Prototype` |
| 7 | 9 | 1 | 8 | 130 | `Enemy_Health130` |
| 8 | 10 | 1 | 8 | 175 | `Enemy_Health175` |
| 9 | 11 | 1 | 8 | 230 | `Enemy_Health230` |
| 10 | 12 | 1 | 8 | 305 | `Enemy_Health305` |

Every enemy asset has speed 2 and reward 10. Waves 1 to 6 are easy on purpose: the gold that starts the build cannot fail. The difficulty sits in waves 7 to 10, where a leak no longer starves the waves after it. Confidence in the curve: low to medium.

## Strategies

Bots act every 0.5 s unless stated.
- **Sensible**: each tower on the free cell off the route that adds the most path in range; this may turn a diagonal lane into an L.
- **Maze**: an expert order found offline by a two-step lookahead on path in range, with route changes allowed.
- **Sloppy human**: acts every 2 s and takes a seeded-random cell among the 6 best sensible ones.

End ticks count `Tick()` calls, including the one that ends the run.

| Strategy | Outcome | End | Lives left | Kills | Towers at end | Gold at 30/60/90/120/150 s | Towers at waves 1–10 start | Route at end, cells |
|---|---|---|---|---|---|---|---|---|
| Builds nothing | Lost | tick 2931, 48.9 s | 0 | 0 | 0 | 115/–/–/–/– | 0 0 0 0 | 21.73 |
| Sensible, reinvests at once | Won | tick 9912, 165.2 s | 7 | 72 | 20 | 25/35/35/35/5 | 2 3 4 5 6 8 10 13 15 17 | 26.41 |
| Maze, reinvests at once | Won | tick 10114, 168.6 s | 10 | 75 | 21 | 25/25/25/15/5 | 2 3 4 5 6 8 10 12 14 17 | 39.83 |
| Sloppy human (40 seeds) | 40 won | 163.7–165.5 s | median 8 (6–10) | – | – | – | – | – |
| Bad: 2 towers far from the route | Lost | tick 2931, 48.9 s | 0 | 0 | 2 | 35/–/–/–/– | 2 2 2 2 | 21.73 |
| Bad: towers at the edge of range (≤ 1.5 cells of path each) | Lost | tick 2931, 48.9 s | 0 | 0 | 2 | 35/–/–/–/– | 2 2 2 2 | 21.73 |
| Late: first tower at wave 2 | Won | 164.9 s | 2 | 67 | 19 | 35/5/35/35/15 | 0 0 3 4 6 7 9 12 14 17 | 25.83 |
| Late: first tower at wave 3 | Lost | 163.2 s | 0 | 63 | 18 | 115/35/35/35/15 | 0 0 0 3 4 6 8 10 13 16 | 26.41 |
| Saver: 2 at start, then buys only in batches of 3 | Lost | 162.9 s | 0 | 63 | 17 | 65/35/65/95/25 | 2 2 2 2 5 5 8 11 11 14 | 25.83 |
| Sensible, sells its 2 oldest (fired) towers when wave 5 enters and rebuilds them | Won (the sale costs 40 gold) | 165.9 s | 6 | 71 | 19 | 25/35/25/25/5 | 2 3 4 5 6 7 9 11 14 16 | 27.83 |

**Mazing pays, mostly late.** Capacity is the highest health a fixed build kills with no leak in a wave of 10:

| Towers | 8 | 10 | 12 | 14 | 16 | 20 | 24 |
|---|---|---|---|---|---|---|---|
| Sensible | 140 | 159 | 190 | 230 | 257 | 319 | 390 |
| Maze | 128 | 159 | 198 | 238 | 277 | 359 | 429 |

The maze build wins with 10 lives where the sensible one keeps 7, and with 20% more health in waves 7 to 10 it still wins while the sensible one loses.

## Reference build order (wins)

Each action runs after the given number of `Tick()` calls, before the next one. Every action is accepted, the four-cell occupancy rule included.

**Outcome:** Won on the 10136th `Tick()` (168.9 s), with 10 lives, 45 gold left and 20 towers. The first kill pays 10 on tick 1332.

**Robustness:** with every action after tick 0 shifted 1 to 30 ticks later, no action is refused and the run still wins with 10 lives, 45 gold and 20 towers. It ends on tick 10136 or 10138.

| Tick | Cell | Action | Gold change | Gold after |
|---|---|---|---|---|
| 0 | (0,8) | buy | −40 | 75 |
| 0 | (0,8) | sell, never fired | +40 | 115 |
| 0 | (11,2) | buy | −40 | 75 |
| 0 | (11,3) | buy | −40 | 35 |
| 1350 | (14,2) | buy | −40 | 5 |
| 2100 | (13,2) | buy | −40 | 5 |
| 2760 | (12,0) | buy | −40 | 5 |
| 3690 | (10,1) | buy | −40 | 45 |
| 3690 | (4,7) | buy | −40 | 5 |
| 4350 | (3,7) | buy | −40 | 5 |
| 4620 | (4,6) | buy | −40 | 5 |
| 5190 | (1,6) | buy | −40 | 5 |
| 5400 | (11,2) | sell, fired | +20 | 45 |
| 5400 | (6,8) | buy | −40 | 5 |
| 5730 | (0,6) | buy | −40 | 5 |
| 6270 | (2,5) | buy | −40 | 5 |
| 6540 | (1,3) | buy | −40 | 5 |
| 7200 | (3,4) | buy | −40 | 5 |
| 7590 | (1,2) | buy | −40 | 5 |
| 7890 | (3,2) | buy | −40 | 5 |
| 8550 | (6,5) | buy | −40 | 5 |
| 8790 | (7,7) | buy | −40 | 5 |
| 9180 | (7,5) | buy | −40 | 5 |
| 9810 | (3,3) | buy | −40 | 5 |

Building nothing: Lost on the 2931st `Tick()` (48.9 s, the third enemy of wave 3), with 115 gold unspent.

## Sensitivities

Strategy cells show outcome, end time and lives left.

| Change | Sensible | Maze | Sells and rebuilds | Sloppy humans (40 seeds) |
|---|---|---|---|---|
| Proposal | Won 165.2 s, 7 | Won 168.6 s, 10 | Won 165.9 s, 6 | 40/40 win, median 8 (6–10) |
| Gold per kill 9 (−10%) | Won, 5 | Won, 4 | Lost 164.9 s | 40/40 win, median 4 (2–7) |
| Gold per kill 8 (−20%) | Lost 164.9 s | Lost 168.0 s | Lost 156.9 s | 8/40 win, median 0 (0–3) |
| Gold per kill 12 (+20%) | Won, 10 | Won, 10 | Won, 10 | 40/40 win, median 10 |
| Health +20% in waves 1–3 (55, 65, 80) | Won, 7 | Won, 10 | Won, 6 | 40/40 win, median 8 (2–10) |
| Health +10% in waves 7–10 | Won, 5 | Won, 8 | Won, 1 | 38/40 win, median 3 (0–5) |
| Health +20% in waves 7–10 | Lost 164.2 s | Won, 3 | Lost 161.9 s | 3/40 win, median 0 (0–2) |
| Health −20% in waves 7–10 | Won, 10 | Won, 10 | Won, 10 | 40/40 win, median 10 |
| Cost 48 (+20%) | Lost 163.9 s | Lost 168.0 s | Lost 158.0 s | 3/40 win, median 0 (0–2) |
| Cost 32 (−20%; the start buys 3) | Won, 10 | Won, 10 | Won, 10 | 40/40 win, median 10 |
| Starting gold 92 (−20%) | Won, 9 | Won, 8 | Won, 3 | 38/40 win, median 6 (0–9) |
| Starting gold 138 (+20%; buys 3) | Won, 10 | Won, 10 | Won, 7 | 40/40 win, median 10 (7–10) |
| Refund 40% / 60% | Won, 7 | Won, 10 | Won, 6 / 5 (the sale costs 48 / 32) | 40/40 win, median 8 |

Building nothing loses on tick 2931 in every row.

Breaks first:
1. 20% less gold per kill, or 20% more tower cost: humans win 3 to 8 runs in 40. At 10% less gold they still win every run, with a median of 4 lives.
2. Health in waves 7 to 10: +10% is still a win, +20% is not.

The first waves no longer break the run: 20% more health in waves 1 to 3, or 20% less starting gold, still wins. The refund changes the cost of a move but no outcome.

Simulation: `/private/tmp/claude-501/-Users-morbey-Projetos-ElementalTD/c8be6820-3fc0-435a-a9af-5180aea801b2/scratchpad/sim3c/` (`verify3c.py` checks the port pins, `report.py` prints every table, `refrepair.py 7 1` builds and checks the reference order, `core.py` is the port).
