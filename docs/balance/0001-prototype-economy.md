# 0001 – Prototype economy (step 4a, wave bonus)

Provisional until playtests. Simulated on a float32 port of Core:
- It reproduces the 3c pins that the real Core confirmed: the empty route is 9 straight and 9 diagonal steps (distance 8865), an enemy of speed 2 leaves on its 652nd tick, and on the 3c numbers building nothing loses on tick 2931 and the 3c reference order wins on tick 10136 with 10 lives, 45 gold and 20 towers.
- It follows the rules for the port in plan 0007 (1 to 5): each enemy keeps its wave; a tick runs waves, enemies, towers, projectiles, wave bonuses, outcome; with a life left, each unpaid wave whose last enemy has entered and none of whose enemies is on the board pays, in wave order; a fully leaked wave pays, the last wave pays on the winning tick, the losing tick pays nothing. Rule 5 pin: building nothing, wave 1 pays on tick 1672 and wave 2 on tick 2332, and the run is lost on tick 2931.

The gold rules are those of plan 0005: gold is checked before the board, a kill pays on the tick its shot lands, "fired" means a launch, and the refund is `cost * percent / 100` in integers.

## Values

Only the wave bonus is new; every other number is the one of 3c.

| Value | Where | Number | Confidence |
|---|---|---|---|
| Wave bonus | every wave of `Run_Prototype` | 10 on each of the ten waves (100 per run, an eighth of a winning run's income) | medium |
| Starting gold | `Run_Prototype.startingGold` | 115 (buys exactly 2 towers, leaves 35; the 3rd is `NotEnoughGold`) | medium |
| Tower cost | `Tower_Prototype.cost` | 40 | medium |
| Gold per kill | `goldReward` on every enemy asset | 10 | medium |
| Sell refund after the first launch | `Run_Prototype.sellRefundPercent` | 50 (20 gold) | low |
| Lives | `Run_Prototype.lives` | 10 | medium |
| Tower combat stats, enemy speed | `Tower_Prototype`, all enemies | range 2.5, damage 10, 1.5 shots/s, projectile 10; speed 2 | – |

| Wave | Count | Interval s | Delay s | Enemy health | Bonus | Enemy asset |
|---|---|---|---|---|---|---|
| 1 | 3 | 1 | 15 | 45 | 10 | `Enemy_Health45` |
| 2 | 4 | 1 | 8 | 55 | 10 | `Enemy_Health55` |
| 3 | 5 | 1 | 8 | 65 | 10 | `Enemy_Health65` |
| 4 | 6 | 1 | 8 | 75 | 10 | `Enemy_Health75` |
| 5 | 7 | 1 | 8 | 90 | 10 | `Enemy_Health90` |
| 6 | 8 | 1 | 8 | 100 | 10 | `Enemy_Prototype` |
| 7 | 9 | 1 | 8 | 130 | 10 | `Enemy_Health130` |
| 8 | 10 | 1 | 8 | 175 | 10 | `Enemy_Health175` |
| 9 | 11 | 1 | 8 | 230 | 10 | `Enemy_Health230` |
| 10 | 12 | 1 | 8 | 305 | 10 | `Enemy_Health305` |

Every enemy asset has speed 2 and reward 10. Waves 1 to 6 are easy on purpose; the difficulty sits in waves 7 to 10.

**Why only the bonus moves.** A flat 10 is the smallest bonus that removes the cliff: at 20% less gold per kill the sloppy human wins 40 runs in 40 (8 in 40 without it), and at 20% more late health 40 in 40 (3 in 40 without it). Kills still carry the economy: with no gold per kill every strategy loses before 92 s, and a build that kills nothing collects 20 gold before it loses. Bonus 5 leaves both margins short (37 and 35 in 40). The cost is that a sloppy human now loses almost nothing (median 10 lives): see Risks.

## Strategies

Bots act every 0.5 s unless stated.
- **Sensible**: each tower on the free cell off the route that adds the most path in range; this may turn a diagonal lane into an L.
- **Maze**: an expert order found offline by a two-step lookahead on path in range, with route changes allowed.
- **Sloppy human**: acts every 2 s and takes a seeded-random cell among the 6 best sensible ones.

End ticks count `Tick()` calls, including the one that ends the run. Gold includes the bonuses.

| Strategy | Outcome | End | Lives left | Kills | Towers at end | Bonus earned | Gold at 30/60/90/120/150 s | Gold at end | Towers at waves 1–10 start |
|---|---|---|---|---|---|---|---|---|---|
| Builds nothing | Lost | tick 2931, 48.9 s | 0 | 0 | 0 | 20 | 125/–/–/–/– | 135 | 0 0 0 0 |
| Sensible, reinvests at once | Won | tick 9752, 162.5 s | 10 | 75 | 23 | 100 | 35/25/25/35/25 | 45 | 2 3 4 5 7 9 12 15 17 20 |
| Maze, reinvests at once | Won | tick 10064, 167.7 s | 10 | 75 | 23 | 100 | 35/15/25/35/15 | 45 | 2 3 4 5 7 9 12 14 17 20 |
| Sloppy human (40 seeds) | 40 won | 161.2–164.0 s | median 10 (9–10) | – | – | 100 | – | – | – |
| Bad: towers far from the route | Lost | tick 2931, 48.9 s | 0 | 0 | 3 | 20 | 5/–/–/–/– | 15 | 2 2 3 3 |
| Bad: towers at the edge of range (≤ 1.5 cells of path each) | Lost | tick 2931, 48.9 s | 0 | 0 | 3 | 20 | 5/–/–/–/– | 15 | 2 2 3 3 |
| Late: first tower at wave 2 | Won | tick 9794, 163.2 s | 7 | 72 | 22 | 100 | 5/35/35/5/35 | 55 | 0 0 3 5 6 8 11 14 16 19 |
| Late: first tower at wave 3 | Won | tick 9808, 163.5 s | 3 | 68 | 21 | 100 | 125/25/15/35/25 | 55 | 0 0 0 4 5 7 10 13 15 18 |
| Late: first tower at wave 4 | Lost | tick 2931, 48.9 s | 0 | 0 | 3 | 20 | 125/–/–/–/– | 15 | 0 0 0 0 |
| Saver: 2 at start, then buys only in batches of 3 | Won | tick 9758, 162.6 s | 9 | 74 | 23 | 100 | 75/95/25/95/45 | 35 | 2 2 2 5 5 8 11 14 14 17 |
| Sensible, sells its 2 oldest (fired) towers when wave 5 enters and rebuilds them | Won (the sale costs 40 gold) | tick 9818, 163.6 s | 10 | 75 | 22 | 100 | 35/25/5/35/15 | 45 | 2 3 4 5 7 8 11 14 16 18 |

The bonus forgives a slow start: starting at wave 3 and saving in batches now win (both lost before), and starting at wave 4 still loses. A strategy that kills nothing collects only the bonuses of waves 1 and 2.

**Mazing pays, mostly late.** Capacity is the highest health a fixed build kills with no leak in a wave of 10 (the bonus does not change it):

| Towers | 8 | 10 | 12 | 14 | 16 | 20 | 24 |
|---|---|---|---|---|---|---|---|
| Sensible | 140 | 159 | 190 | 230 | 257 | 319 | 390 |
| Maze | 128 | 159 | 198 | 238 | 277 | 359 | 429 |

## Reference build order (wins)

The 3c order, unchanged; only the gold differs. Each action runs after the given number of `Tick()` calls, before the next one. Every action is accepted, the four-cell occupancy rule included.

**Outcome:** Won on the 10136th `Tick()` (168.9 s), with 10 lives, 145 gold left and 20 towers; the last wave's bonus is paid on that tick. The first kill pays 10 on tick 1332.

**Robustness:** with every action after tick 0 shifted 1 to 30 ticks later, no action is refused and the run still wins with 10 lives, 145 gold and 20 towers. It ends on tick 10136 or 10138.

| Tick | Cell | Action | Gold change | Gold after |
|---|---|---|---|---|
| 0 | (0,8) | buy | −40 | 75 |
| 0 | (0,8) | sell, never fired | +40 | 115 |
| 0 | (11,2) | buy | −40 | 75 |
| 0 | (11,3) | buy | −40 | 35 |
| 1350 | (14,2) | buy | −40 | 5 |
| 2100 | (13,2) | buy | −40 | 15 |
| 2760 | (12,0) | buy | −40 | 25 |
| 3690 | (10,1) | buy | −40 | 75 |
| 3690 | (4,7) | buy | −40 | 35 |
| 4350 | (3,7) | buy | −40 | 45 |
| 4620 | (4,6) | buy | −40 | 55 |
| 5190 | (1,6) | buy | −40 | 55 |
| 5400 | (11,2) | sell, fired | +20 | 95 |
| 5400 | (6,8) | buy | −40 | 55 |
| 5730 | (0,6) | buy | −40 | 65 |
| 6270 | (2,5) | buy | −40 | 65 |
| 6540 | (1,3) | buy | −40 | 75 |
| 7200 | (3,4) | buy | −40 | 75 |
| 7590 | (1,2) | buy | −40 | 75 |
| 7890 | (3,2) | buy | −40 | 85 |
| 8550 | (6,5) | buy | −40 | 85 |
| 8790 | (7,7) | buy | −40 | 85 |
| 9180 | (7,5) | buy | −40 | 95 |
| 9810 | (3,3) | buy | −40 | 95 |

**Building nothing:** wave 1 pays 10 on tick 1672 and wave 2 pays 10 on tick 2332, both fully leaked; the run is lost on the 2931st `Tick()` (48.9 s, the third enemy of wave 3) with 135 gold. Wave 3 pays nothing: it is still on the board on the losing tick.

## Sensitivities

Strategy cells show outcome, end time and lives left.

| Change | Sensible | Maze | Sells and rebuilds | Reference order | Sloppy humans (40 seeds) |
|---|---|---|---|---|---|
| Proposal | Won 162.5 s, 10 | Won 167.7 s, 10 | Won 163.6 s, 10 | Won, 10, gold 145 | 40/40 win, median 10 (9–10) |
| **Bonus 0** (the 3c numbers) | Won 165.2 s, 7 | Won 168.6 s, 10 | Won 165.9 s, 6 | Won, 10, gold 45 | 40/40 win, median 8 (6–10) |
| Bonus 5 | Won, 10 | Won, 10 | Won, 10 | Won, 10, gold 95 | 40/40 win, median 10 (9–10) |
| Bonus 15 | Won, 10 | Won, 10 | Won, 10 | Won, 10, gold 195 | 40/40 win, median 10 (9–10) |
| Gold per kill 9 (−10%) | Won, 10 | Won, 10 | Won, 7 | Won, 10, gold 70 | 40/40 win, median 10 (8–10) |
| Gold per kill 8 (−20%) | Won, 5 | Won, 8 | Won, 2 | Lost 160.7 s | 40/40 win, median 5 (3–8) |
| Gold per kill 8, **bonus 0** | Lost 164.9 s | Lost 168.0 s | Lost 156.9 s | Lost 144.4 s | 8/40 win, median 0 (0–3) |
| Gold per kill 0 (bonus only) | Lost 91.4 s | Lost 91.4 s | Lost 88.8 s | Lost 88.4 s | 0/40 win |
| Gold per kill 12 (+20%) | Won, 10 | Won, 10 | Won, 10 | Won, 10, gold 295 | 40/40 win, median 10 (9–10) |
| Health +20% in waves 1–3 (55, 65, 80) | Won, 10 | Won, 10 | Won, 10 | Won, 8 | 40/40 win, median 10 (8–10) |
| Health +10% in waves 7–10 | Won, 10 | Won, 10 | Won, 8 | Won, 6 | 40/40 win, median 10 (6–10) |
| Health +20% in waves 7–10 | Won, 6 | Won, 7 | Won, 3 | Lost 168.6 s | 40/40 win, median 5 (3–7) |
| Health +20% in waves 7–10, **bonus 0** | Lost 164.2 s | Won, 3 | Lost 161.9 s | Lost 142.7 s | 3/40 win, median 0 (0–2) |
| Health +30% in waves 7–10 | Lost 164.9 s | Won, 6 | Lost 162.9 s | Lost 163.3 s | 25/40 win, median 1 (0–4) |
| Health −20% in waves 7–10 | Won, 10 | Won, 10 | Won, 10 | Won, 10 | 40/40 win, median 10 (9–10) |
| Cost 48 (+20%) | Won, 4 | Won, 8 | Won, 1 | Lost 164.3 s | 38/40 win, median 4 (0–8) |
| Cost 32 (−20%; the start buys 3) | Won, 10 | Won, 10 | Won, 10 | Won, 10 | 40/40 win, median 10 |
| Starting gold 92 (−20%) | Won, 10 | Won, 10 | Won, 10 | Won, 8 | 40/40 win, median 10 (7–10) |
| Starting gold 138 (+20%; buys 3) | Won, 10 | Won, 10 | Won, 10 | Won, 10 | 40/40 win, median 10 |
| Refund 40% / 60% | Won, 10 | Won, 10 | Won, 10 | Won, 10, gold 141 / 149 | 40/40 win, median 10 (9–10) |

Building nothing loses on tick 2931 in every row, with the starting gold plus 20 (plus 10 or 30 with bonus 5 or 15).

Breaks first:
1. Health in waves 7 to 10: +20% is still won by every human proxy, with a median of 5 lives; +30% is won 25 times in 40.
2. Tower cost +20%: 38 wins in 40, median 4 lives.

The reference order is a fixed timetable, not a player: it loses under −20% gold or +20% late health because some of its purchases are no longer affordable on their tick.

**Risks.** With the cliff gone, the proposal is easy: a sloppy human loses 0 or 1 life. A run that costs a few lives and still survives +20% late health cannot be had on this map, because leaks start only when the build is short of damage: late health +10% leaves the median at 10, and +20% puts it at 5 but leaves no margin above it. Tune first in play: the late health (waves 7 to 10), then the bonus.

Simulation: `/private/tmp/claude-501/-Users-morbey-Projetos-ElementalTD/c8be6820-3fc0-435a-a9af-5180aea801b2/scratchpad/sim4/` (`verify3c.py` checks the port pins and the 3c runs, `report4a.py` prints every table of this step, `core.py` is the port).
